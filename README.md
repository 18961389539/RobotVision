# RobotVision — 机器人引导视觉系统

基于 C# / .NET 8 / OpenCvSharp / YoloDotNet 的机器人引导视觉应用。
流程：TCP 接收配方名 → 相机取图 → 内参去畸变 → 模型推理 → 外参变换 → 返回 (x, y, 角度)。

## 项目结构

```
RobotVision.sln
├── src/
│   ├── RobotVision.Core/               # 领域层：接口、模型、配方、角度几何（无外部依赖耦合）
│   │   ├── Abstractions/               #   ICamera / IAngleStrategy
│   │   ├── Geometry/                   #   AngleGeometry（可单测的纯函数）
│   │   ├── Models/                     #   PixelPose / RobotPose / VisionResult / 标定档案
│   │   └── Recipe/                     #   RecipeConfig / RecipeLoader
│   ├── RobotVision.Infrastructure/     # 基础设施层：管理器与策略实现
│   │   ├── Cameras/                    #   CameraManager / FileCamera（回放相机）
│   │   ├── Lighting/                   #   LightingManager / NoopLightController（无操作光源）
│   │   ├── Calibration/                #   CalibrationManager / 棋盘格内参 / 九点外参标定器
│   │   ├── Communication/              #   TcpServerManager（行协议、超时、连接管理）
│   │   └── Inference/                  #   ModelManager / Mat↔SKBitmap 转换 / 三种角度策略
│   ├── RobotVision.Hosting/            # 共享组装层：AddRobotVision 统一 DI、VisionService、
│   │                                  #   文件日志（控制台宿主与 WPF 宿主行为一致）
│   ├── RobotVision.App/                # 控制台宿主（无头生产部署）
│   └── RobotVision.UI/                 # WPF 宿主（实时画面叠加、手动触发调试）
├── tools/
│   └── RobotVision.CalibTool/          # 命令行标定工具（内参 + 外参）
├── tests/
│   └── RobotVision.Tests/              # 单元测试（几何/标定/协议/队列/模型并发/日志/失败留存）
├── recipes/                            # 配方 JSON（A01 关键点示例 / A02 双模型示例）
├── models/                             # ONNX 模型（自行放置，不入库）
├── logs/                               # 运行日志（按天滚动，appsettings 可配置，不入库）
└── data/
    ├── replay/                         # 回放图片目录（联调用）
    ├── calibration/                    # 标定结果（App 启动自动加载）
    └── failures/                       # 失败现场留存（PNG+JSON 成对，滚动清理，不入库）
```

## 构建与测试

```powershell
dotnet build RobotVision.sln
dotnet test RobotVision.sln
```

## 运行

```powershell
# 开发：从仓库根目录运行（相对目录按"exe 目录优先、工作目录回退"解析）
dotnet run --project src/RobotVision.App
```

**目录解析规则**：`recipes/`、`models/`、`data/`、相机回放目录等相对路径，
优先按 exe 所在目录解析（部署布局：exe 旁放全套资产）；
exe 目录不存在时回退到当前工作目录（开发布局：`dotnet run` 从仓库根启动）。
Windows 服务/计划任务启动时 CWD 是 `System32`，部署时务必把资产放在 exe 旁。

配置见 `src/RobotVision.App/appsettings.json`：TCP 监听地址/端口、超时、
排队深度（`maxQueueDepth`，默认 4）、目录、相机列表。
真实相机（海康/Basler 等）实现 `ICamera` 后在 `Program.cs` 的相机注册处接入。

**文件日志**（默认开启）：Serilog 滚动文件，按天滚动（`logs/robotvision-yyyyMMdd.log`）、
自动清理超期文件（默认保留 30 天）。条目含毫秒时间戳、级别、来源类别与完整异常堆栈——
工控机无头部署时这是唯一的现场留痕。appsettings 的 `FileLogging` 段可开关、改目录与保留天数。

**失败现场图像留存**（默认开启）：取图成功后的任何失败（1007 未检出 / 1005 模型异常 /
1099 内部错误）把**模型实际看到的去畸变图**落盘，连同 JSON 元数据（配方、错误码、
错误消息、耗时、分辨率）。文件名 `{时间戳}_{配方}_{错误码}.png`，按数量滚动清理
（默认保留 200 张，含元数据；`RetainedCount ≤ 0` 不清理）。取图前的失败
（1001/1002/1003/1004）无现场可留。留存是尽力而为：I/O 异常只记日志，绝不影响产线管线。
排障时拿 PNG 看当时画面，拿 JSON 对应日志时间线。

**请求排队与并发语义**：并发槽位模型（`maxQueueDepth` 个任务同时执行，排队 + 执行总数
不超过该值，队列上限**含正在执行的任务**）。不再全局单锁：
取图按相机粒度串行（同相机 I/O 串行、不同相机可并行），推理由模型会话内信号量
按模型串行（同模型串行、不同模型可并行），外参/组装为纯计算。排队阶段的请求可被
超时取消（立即返回 1010 排队超时并放弃排队）；进入推理后不可中断（ONNX 无法取消），
调用方超时返回 1008 处理超时，任务在后台跑完并丢弃结果，槽位随即释放——不会出现
僵尸任务霸占管线导致后续请求连锁超时。排队深度超过 `maxQueueDepth` 的请求立即返回 1009。

### 推理链路验证记录（2026-08-22）

使用 YoloDotNet 官方仓库的 `yolov11s-pose.onnx`（38MB，SHA1 与上游 git blob 一致）+
`people.jpg` 测试图完成端到端验证：`TRIGGER,A01 → OK,A01,8,x,y,角度×8,耗时ms`，
关键点连线几何与原始检测输出吻合（鼻尖/左眼中点即输出坐标，连线角即输出角度）。
首次请求 726ms（含模型加载与预热），后续稳定在 122~134ms（CPU 推理）。
当前 `data/calibration/cam_file.intrinsic.json` 为零畸变占位档案（1280×853），
仅用于链路验证；现场部署时须用真实标定替换。

## TCP 协议（UTF-8 编码的 ASCII 子集，`\n` 结尾）

| 请求 | 应答 | 说明 |
|---|---|---|
| `PING` | `PONG` | 心跳（仅证明连接存活，不反映管线忙闲） |
| `STATUS` | `OK,ready\|busy,队列深度,队列上限,最近耗时ms` | 管线状态查询（PLC 触发前预判） |
| `TRIGGER,配方名` | `OK,配方名,目标数,x,y,角度[,...],耗时ms` | 触发一次完整流程 |
| 出错 | `ERR,错误码,消息` | 见下表 |

**错误消息契约**：业务错误（1001~1008 等）返回 Sanitize 后的可读消息（逗号/换行已消毒）；
`1099 内部错误`固定返回 `ERR,1099,INTERNAL_ERROR`——异常原文（可能含路径/堆栈）只进日志，
绝不上协议线；`UNKNOWN_COMMAND` / `MISSING_RECIPE` 为固定 ASCII 模板。

**超时语义与重试策略**：`1010 排队超时` = 请求在排队阶段超时放弃，管线未受影响，可立即重试；
`1008 处理超时` = 推理已开始但调用方超时，任务在后台跑完释放槽位（防僵尸设计），
立即重试可能再次排队超时。客户端收到 1008 后建议：先发 `STATUS` 确认返回 `ready`
（或等待 ≥ 2×TimeoutMs）再重发 `TRIGGER`。

**目标数字段**：应答第 3 段固定为检出目标数，PLC 无需先数字段即可定位位姿三元组；
也为将来"0 目标返回空 OK（count=0）"预留了非破坏性扩展（当前 0 目标仍返回 ERR 1007）。
注意这是对旧版 `OK,配方名,x,y,...` 的**破坏性协议变更**，PLC 解析侧需同步更新。

错误码：1000 未知命令 / 1001 配方不存在（含无效配方）/ 1002 相机未注册 / 1003 取图失败 /
1004 未标定（含 stationId 缺失且未开调试直通）/ 1005 模型不可用 / 1006 光源控制器未注册 /
1007 未检出目标 / 1008 处理超时（已进入推理，后台跑完释放）/ 1009 排队超限（Busy）/
1010 排队超时（未进入推理，已放弃排队）/ 1011 相机初始化失败（pylon 运行库缺失/设备打开失败）/
1099 内部错误。

配方名只允许字母、数字、下划线、中划线——`TRIGGER,..\xxx` 之类的路径穿越探测
一律按 1001 拒绝。启动时预加载全部配方并校验（cameraId/models/阈值/keypoint 索引等），
无效配方记入启动日志且不可触发。

## 相机模块

统一接口 `ICamera`（File 回放 / Basler pylon / Virtual 虚拟三种实现），返回
`CameraFrame`（图像 + 采集时刻 UTC），`Grab(CancellationToken)` 支持取消——
取图前的阶段（光源稳定延时/取图阻塞）响应超时取消，进入推理后不可中断。

- **懒连接**：Basler 相机构造只校验 pylon 运行库，`Open/Start` 推迟到首次取图。
  启动时相机未上电/网络未就绪**不阻断服务**，首次触发自动连接，无需重启；
- **自动重连**：单帧采集失败（连接中断类）后同请求内重连一次
  （Close→Open→重发曝光/增益→Start→再取一帧），仍失败才返回 1003；断线后自愈；
- **调光接口 `IExposureControl`**：曝光/增益运行时调光按接口查询而非强转具体品牌——
  海康等其他品牌实现该接口后，UI 调光卡片自动可用；
- **超时预算校验**：`GrabTimeoutMs` 必须小于全局 `TimeoutMs`（启动告警 + UI 保存拦截），
  否则取图超时表现为 1008 而非 1003；`GrabTimeoutMs` 为 0 或负数同样拒绝；
- **帧率控制**：File 回放相机支持 `IntervalMs`（ms，0 = 不限速），联调节拍更接近产线；
  虚拟相机同参模拟曝光延时；
- **错误语义**：构造/参数非法抛 1011 初始化失败（区别于 1003 取图失败）；
  相机 Id 重复注册只告警并跳过（不静默覆盖）；`ToMat` 直写 `Mat.Data` 省一次拷贝。

## 光源（照明）模块

产线环境光不可控，定位/测量对光照稳定敏感——光源把"环境光波动"变成"可控照明"。
模块与相机同构：接口抽象 + 虚拟实现 + 配置驱动，未接硬件时零成本可跑。

- **`ILightController`**：`Apply(LightingConfig)` 点亮 + `TurnOff()` 熄灯，幂等；
- **`NoopLightController`**：无操作虚拟实现（appsettings `Type: "None"`），
  配方已配照明但现场未接线时的调试兜底，同 FileCamera 定位；
- **时序**：取图前 `点亮 → 稳定延时 → 取图`，延时计入超时预算；
  作用域模式保证取图完成后按 `turnOffAfterGrab` 熄灯，异常路径同样释放；
- **错误语义**：配方引用未注册控制器在加载时拦截；运行时兜底返回 1006
  （取图前的失败，无现场图可留）。

接入真实光源控制器（奥普特/康耐视等，串口/Modbus/TCP）：
1. 实现 `ILightController`（参照 `NoopLightController` 与 `BaslerCamera` 的接入模式）；
2. 实现 `ILightControllerFactory` 并调用 `LightControllerTypeRegistry.Default.Register(...)`
   一行注册（与相机 `CameraTypeRegistry` 同构）——服务注册、UI 类型下拉、
   运行时注册自动生效，无需改核心与 DI 代码；
3. appsettings `LightControllers` 增加条目，`Type` 填工厂 `TypeName`，配方 `lightControllerId` 指向它。

## 标定

```powershell
# 内参（棋盘格，15~25 张，覆盖四角、姿态多样）
dotnet run --project tools/RobotVision.CalibTool -- intrinsic `
  --camera cam_file --folder <棋盘图片目录> --cols 9 --rows 6 --square 5.0

# 外参（九点法，机器人带针走 9 点，记录像素/机器人坐标对）
dotnet run --project tools/RobotVision.CalibTool -- extrinsic `
  --camera cam_file --station st1 --file pairs.csv

# 旋转中心（偏心工具补偿，即"九点+3 旋转点"的 3 点部分）
dotnet run --project tools/RobotVision.CalibTool -- rotation `
  --camera cam_file --station st1 --file points.csv
```

验收参考：内参 RMS ≤ 0.3px 优秀 / ≤ 0.5px 可用；外参最大残差 ≤ 0.1（机器人单位）；
旋转中心半径残差 RMS ≤ 0.3px 优秀 / ≤ 0.5px 可用，长短轴比 ≤ 1.2。
标定结果保存到 `data/calibration/`，App 启动时自动加载。

**一致性铁律**：外参/旋转中心标定必须使用去畸变后的图像坐标（标定图先经过内参去畸变处理，
或在去畸变后的图像上取像素点），否则像素→机器人变换会引入系统性偏差。

### 旋转中心补偿（9+3 标定）

吸嘴等工具与第 4 轴旋转轴不共轴时，机器人旋转后工具尖端会偏离检测位置
（偏差 `d = 2r·sin(Δθ/2)`，r 为偏心距）。标定流程：

1. 第 4 轴带标记（针尖/特征点）转到 5~9 个等间隔角度（如每 45°），逐角度记录标记的像素坐标到 csv；
2. `CalibTool rotation` 拟合轨迹圆得到轴心像素坐标（≥5 点附带椭圆长短轴比质检）；
3. 配方设置 `"rotationCompensation": "EccentricTool"` 开启补偿。

运行时输出 `P' = C + R(−θ)·(P − C)`（机器人坐标系内）：机器人先移动到输出位置，
再旋转第 4 轴到输出角度，工具尖端恰好落在零件检测位置。
前提：第 4 轴角度正方向与机器人 XY 系旋转方向一致（右手系逆时针为正），
不一致的机器人请在示教侧取反角度。

注意：角度跨度不足（如 0°/5°/10°）会被拒绝——近距角度使圆拟合病态；
标记点几乎不动说明工具同心，无需补偿。

## 三种角度模式（recipe.angleMode）

| 模式 | 模型 | 角度定义 | 特点 |
|---|---|---|---|
| `MaskMinAreaRect` | 1 个分割模型 | 掩码最小外接矩形长边方向 [0,180) | 有 180° 歧义；长宽比 <1.5 时不建议 |
| `DualCenterLine` | 2 个检测模型 | 两目标中心连线 A→B | 方向唯一；基线越长角度越稳 |
| `KeyPointLine` | 1 个关键点模型 (kpt_shape=[2,2]) | 两关键点连线 | 位置+方向一体；注意关键点为整型像素 |

接入新角度模式（模板匹配/OCR/测量等）：
1. 实现 `IAngleStrategy`（参照三种内置策略的接入模式）；
2. 实现 `IAngleStrategyFactory`（提供 `Mode`/`Label`/`Create`）并调用
   `AngleStrategyTypeRegistry.Default.Register(...)` 一行注册——与相机/光源注册表同构，
   服务注册、UI 角度模式下拉、配方校验自动生效，无需改核心与 DI 代码；
3. 配方 `angleMode` 填工厂的 `Mode` 枚举值。

## 模型会话与并发

`ModelManager` 按 **(模型路径, 推理任务)** 缓存 Yolo 实例，加载后空跑一帧预热：

- **单次物化**：缓存值为 `Lazy<T>`，并发 `Open` 同一模型只有一个加载真正执行——
  输家的包装对象未物化即被丢弃，不会泄漏 ONNX 会话（数百 MB 级）；
- **失败不缓存**：模型文件缺失/预热异常的加载不留缓存，后补模型文件后可立即重试；
- **预热失败也清理**：Yolo 已创建但预热抛错时立即 Dispose，不挂半初始化会话；
- **同模型推理串行**：Yolo 实例非线程安全，`ModelSession.Run` 内部信号量串行化；
- **任务参与缓存键**：同一文件被不同任务打开时行为确定（各自的预热各自成败），
  而非"谁先加载谁的任务赢"。


## 配方示例（recipes/A01.json）

```json
{
  "cameraId": "cam_file",
  "stationId": "",
  "debugPassthrough": true,
  "angleMode": "KeyPointLine",
  "models": [ "a01_kpt.onnx" ],
  "confidence": 0.5,
  "iou": 0.7,
  "keypointIndexA": 0,
  "keypointIndexB": 1,
  "keypointMinConfidence": 0.3,
  "rotationCompensation": "None"
}
```

**光源（可选）**：缺省不亮灯，行为与旧版完全一致。需要照明时在 appsettings
注册光源控制器，配方成对指定 `lightControllerId` + `lighting`：

```json
{
  "cameraId": "cam_basler",
  "stationId": "st1",
  "lightControllerId": "light_ring",
  "lighting": {
    "channels": [ { "channel": 1, "brightness": 200 } ],
    "stabilizeDelayMs": 10,
    "turnOffAfterGrab": true
  }
}
```

时序：TRIGGER → 点亮光源 → 等待 `stabilizeDelayMs` → 取图 → 推理 → 熄灯。
`turnOffAfterGrab: false` 用于常亮场景（光源与产线节拍联动），减少每帧开关灯抖动。
延时计入单次 TRIGGER 超时预算。

**`stationId` 是安全项**：缺省且未显式设置 `"debugPassthrough": true` 时直接返回
1004 错误，绝不静默回退为像素坐标——像素坐标的数值量级与机器人坐标相近，
被机器人当成真坐标使用会造成撞机风险。台架调试需要像素直通时，
必须同时写 `"stationId": ""` 和 `"debugPassthrough": true`。
`rotationCompensation` 默认 `None`；工具与第 4 轴偏心时设为 `EccentricTool`
（需先做旋转中心标定，见上节）。

## 更换推理后端

当前使用 CPU（`YoloDotNet.ExecutionProvider.Cpu`）。更换 GPU 时：
1. NuGet 包替换为 `YoloDotNet.ExecutionProvider.Cuda` / `DirectML` / `OpenVino`；
2. `ModelManager.Load` 中的 `CpuExecutionProvider` 换成对应 Provider 类；
3. 其余代码不变。

## 并发与线程安全

- `VisionService` 通过信号量串行化整个管线，并发 TRIGGER 排队执行；
- 每个模型的 Yolo 实例通过 `ModelSession` 内部信号量串行化（Yolo 非线程安全）；
- 模型加载后空跑一帧预热，避免首次请求耗时突增。
