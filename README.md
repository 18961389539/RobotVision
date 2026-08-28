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
│   │   └── Inference/                  #   ModelManager（IInferenceEngine 抽象）/ 策略与工厂注册表
│   ├── RobotVision.Hosting/            # 共享组装层：AddRobotVision 统一 DI、VisionService
│   │                                  #   （编排）/ PipelineScheduler（并发队列）/ VisionMetrics（统计）、
│   │                                  #   文件日志（滚动文件 + UI 日志sink）
│   └── RobotVision.Wpf/                # WPF 宿主（TCP 服务 + 实时画面叠加、手动触发调试）
├── tools/
│   ├── RobotVision.CalibTool/          # 命令行标定工具（内参 + 外参）
│   └── RobotVision.InferenceBench/     # CPU vs OpenVINO 推理对比（工控机上跑）
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

### 测试矩阵

| 项目 | 类型 | 覆盖范围 |
|---|---|---|
| `tests/RobotVision.Tests` | 单元测试（xunit + FsCheck 属性测试） | 角度几何、配方加载/校验、标定（内参/外参/旋转中心/多项式）、TCP 协议解析、相机（File/Virtual/Basler/GigE）、光源、模型并发、失败留存、队列/超时语义、日志解析；属性测试覆盖 NaN/Infinity/退化几何/协议往返 |
| `tests/RobotVision.Wpf.Tests` | UI 层测试（ViewModel，不依赖 UI 线程） | 主监控页（相机/配方下拉、日志过滤、触发失败横幅）、配方页（列表/搜索/新建/复制/脏标记）、设置页（脏标记/保存/热重启/恢复默认）、标定页（档案映射/质量文本）、日志页（文件列表/加载/级别与关键词过滤/清空）、失败现场画廊（加载/筛选/按钮文案） |
| `tests/ImageViewerControl.Tests` | UI 控件层测试 | 撤销/重做管理器（容量上限/异常入栈/PropertyChanged）、ROI 几何服务（环形钳制/多边形闭合/包围盒）、ROI 命令（状态替换/标签）、ROI 持久化（序列化往返/未知类型跳过/文件 IO）、插件注册表（内置发现/重复注册/反注册/绘制工具排序） |
| `tests/RobotVision.IntegrationTests` | 端到端集成测试 | 真实 TCP socket ↔ 服务（PING/STATUS/触发/错误码/白名单/序列号/路径穿越）、VisionService 全链路（1001~1016 语义、OnArm 位姿校验、快照订阅）、DI 容器解析与 RuntimeSync 热应用、并发压力（多连接/排队/死锁免疫）、FlaUI UI 自动化冒烟（`RV_UI_TEST=1` 启用） |
| `benchmarks/RobotVision.Benchmarks` | BenchmarkDotNet 基准 | 角度几何、协议解析/应答格式化、标定变换/旋转补偿、ROI 序列化/反序列化。运行：`dotnet run -c Release --project benchmarks/RobotVision.Benchmarks`（可加 `--filter *AngleGeometry*` 缩小范围） |
| `tools/RobotVision.InferenceBench` | 推理 EP 对比（独立进程） | 现场 ONNX + 现场图：CPU vs OpenVINO CPU/GPU 的 p50/p95/p99，以及两枪串行/双会话并行/混池齐活。**不要**和上面 BenchmarkDotNet 混用。 |

**环境门控测试**（默认跳过，避免 CI/无硬件环境误报）：
- 真实相机硬件冒烟：`RV_HARDWARE_TEST=1`（见 `HardwareCameraSmokeTests`）
- WPF 宿主 UI 自动化：`RV_UI_TEST=1`（须先 `dotnet build`，桌面会话中运行）

**真实模型链路**：集成测试复用仓库 `models/a01_kpt.onnx`（39MB）+ `data/replay/people.jpg`，
覆盖取图 → 去畸变 → 真实推理 → 位姿变换 → 应答的完整链路；模型缺失时相关用例自动降级为校验失败路径。

**覆盖率**：`dotnet test tests/<项目> --collect:"XPlat Code Coverage"` 生成 Cobertura 报告
（coverlet.collector 已内置），可用 ReportGenerator 转 HTML 查看。

## 运行

```powershell
# 开发：从仓库根目录运行（相对目录按"exe 目录优先、工作目录回退"解析）
dotnet run --project src/RobotVision.Wpf
```

**目录解析规则**：`recipes/`、`models/`、`data/`、相机回放目录等相对路径，
优先按 exe 所在目录解析（部署布局：exe 旁放全套资产）；
exe 目录不存在时回退到当前工作目录（开发布局：`dotnet run` 从仓库根启动）。
Windows 服务/计划任务启动时 CWD 是 `System32`，部署时务必把资产放在 exe 旁。

配置见 `src/RobotVision.Wpf/appsettings.json`：TCP 监听地址/端口、超时、
排队深度（`maxQueueDepth`，默认 4）、目录、相机列表、推理
（`Inference:Provider` 默认 `OpenVinoGpu`、`Inference:MaxSessions` 默认 8）。
真实相机（海康/Basler 等）实现 `ICamera` + `ICameraFactory` 后调用
`CameraTypeRegistry.Default.Register(...)` 一行注册接入。

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
`people.jpg` 测试图完成端到端验证：`A01 → OK,x,y,角度×8,A01,8,耗时ms`，
关键点连线几何与原始检测输出吻合（鼻尖/左眼中点即输出坐标，连线角即输出角度）。
首次请求 726ms（含模型加载与预热），后续稳定在 122~134ms（CPU 推理）。
当前 `data/calibration/cam_file.intrinsic.json` 为零畸变占位档案（1280×853），
仅用于链路验证；现场部署时须用真实标定替换。

## TCP 协议（ASCII；请求有无换行均可，应答以 `\n` 结尾）

完整协议规范（含 PLC 集成指引、时序与处置流程）见 `docs/PLC-TRIGGER-Protocol.md`。

| 请求 | 应答 | 说明 |
|---|---|---|
| `PING` | `PONG` | 心跳（仅证明连接存活，不反映管线忙闲） |
| `STATUS` | `OK,ready\|busy,队列深度,队列上限,最近耗时ms,连续失败,联锁0/1` | 管线状态查询（后两段为过程能力扩展，旧 PLC 可读前 5 段） |
| `CLEARINHIBIT` 或 `CLEARINHIBIT,键`（键=配方名或序列号） | `OK,CLEARED` | 解除连续失败联锁（1018） |
| `配方名` 或 `序列号`（`3` / `#3`） | `OK,x,y,角度[,...],配方名,目标数,耗时ms` | 触发一次完整流程（不带位姿） |
| `键,X,Y,RZ`（键=配方名或序列号） | 同上 | 带拍照位姿触发（末端相机工位必须；
与 OnArm 外参档案比对，不一致返回 1012） |
| 出错 | `ERR,错误码,消息` | 见下表 |

**错误消息契约**：业务错误（1001~1008 等）返回 Sanitize 后的可读消息（逗号/换行已消毒）；
`1099 内部错误`固定返回 `ERR,1099,INTERNAL_ERROR`——异常原文（可能含路径/堆栈）只进日志，
绝不上协议线；`UNKNOWN_COMMAND` / `MISSING_RECIPE` 为固定 ASCII 模板。

**超时语义与重试策略**：`1010 排队超时` = 请求在排队阶段超时放弃，管线未受影响，可立即重试；
`1008 处理超时` = 推理已开始但调用方超时，任务在后台跑完释放槽位（防僵尸设计），
立即重试可能再次排队超时。客户端收到 1008 后建议：先发 `STATUS` 确认返回 `ready`
（或等待 ≥ 2×TimeoutMs）再重发触发行。

**目标数字段**：坐标三元组紧跟 `OK`；配方名与目标数在耗时前（倒数第 2 段为 N）。
PLC 可顺序读坐标，或用尾部 N 校验；也为将来"0 目标返回空 OK（count=0）"预留扩展（当前 0 目标仍返回 ERR 1007）。

错误码：1000 未知命令 / 1001 配方不存在（配方名错/文件缺）/ 1002 相机未注册 / 1003 取图失败 /
1004 未标定（含 stationId 缺失）/ 1005 模型不可用 / 1006 光源控制器未注册 /
1007 未检出目标 / 1008 处理超时（已进入推理，后台跑完释放）/ 1009 排队超限（Busy）/
1010 排队超时（未进入推理，已放弃排队）/ 1011 相机初始化失败（pylon 运行库缺失/设备打开失败）/
1012 拍照位姿不一致（OnArm 工位，TRIGGER 上报位姿与外参标定位姿超容差，容差见 PoseCheck 段）/
1013 TRIGGER 参数格式错误（段数/数值非法）/ 1014 OnArm 未上报拍照位姿 /
1015 配方被停用 / 1016 配方参数或引用校验失败 /
1017 资产哈希不一致（配方钉扎的模型/标定 SHA-256 与当前文件不符）/
1018 连续失败联锁（同配方过程失败达到阈值，排除后 `CLEARINHIBIT` 或界面解除）/
1019 精修失败（分割到了但角度/头尾不过门，与 1007 漏检区分）/
1099 内部错误（含配方文件 IO 故障）。

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

## 推理 EP 对比（CPU vs OpenVINO）

必须在**目标工控机**上、用现场 ONNX 和现场分辨率/ROI 图跑。不要和 `RobotVision.Benchmarks`（几何/协议微基准）混用。
YoloDotNet 每个进程只能加载一种 Execution Provider，本工具把 OpenVINO 放到独立 `ov-worker` 进程。

```powershell
dotnet run -c Release --project tools/RobotVision.InferenceBench -- `
  --model models\xxx.onnx --image data\replay\a.bmp
# 可选：--roi 0.1,0.1,0.5,0.5  --task seg  --model2 models\b.onnx  --phase A
```

屏幕会打印 Phase A 的 p50/p95/p99、相对 CPU 的框/分数偏差，以及 Phase B 两枪齐活时间，并给出「换哪个 EP / 要不要第二会话 / 是否丢掉混池」的建议。
产线默认已按工控机对比结果切到 `OpenVinoGpu`（单会话）。本工具仍可用来复测。

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
标定结果保存到 `data/calibration/`，程序启动时自动加载。

**一致性铁律**：外参/旋转中心标定必须使用去畸变后的图像坐标（标定图先经过内参去畸变处理，
或在去畸变后的图像上取像素点），否则像素→机器人变换会引入系统性偏差。
外参/旋转中心档案记录标定时分辨率：换相机/改分辨率后与当前内参不一致时拒绝使用
（返回 1004，需重新标定），杜绝旧像素坐标系静默错位。标定档案均为原子落盘
（临时文件替换），写一半崩溃不会损坏档案。

### 旋转中心补偿（9+3 标定）

吸嘴等工具与第 4 轴旋转轴不共轴时，机器人旋转后工具尖端会偏离检测位置
（偏差 `d = 2r·sin(Δθ/2)`，r 为偏心距）。标定流程：

1. 第 4 轴带标记（针尖/特征点）转到 5~9 个等间隔角度（如每 45°），逐角度记录标记的像素坐标到 csv；
2. `CalibTool rotation` 拟合轨迹圆得到轴心像素坐标（≥5 点附带椭圆长短轴比质检）；
3. 配方设置 `"rotationCompensation": "EccentricTool"` 开启补偿。

运行时输出（机器人坐标系内，含工具零位偏角 δ）：
位置 `P' = C + R(δ−θ)·(P − C)`，第 4 轴角 `φ = θ − δ`。
机器人先移动到输出位置，再旋转第 4 轴到 φ，工具尖端恰好落在零件检测位置。
δ=0（工具零位与 X 轴对齐）时退化为经典形式。

**工具零位偏角 δ**（旋转中心档案 `ToolOffsetDeg`）：第 4 轴零位时工具指向相对
X 轴的偏角（吸嘴安装偏角/工具坐标系残差）。未补偿时位置误差 `2r·sin(δ/2)`
（r=30mm、δ=3° → 1.6mm）。**可从带角度的标定点自动实测**（标记绕轴心方位角 βᵢ −
第 4 轴角 φᵢ 的圆均值）：向导点表填 ≥2 个第4轴角并计算后点「填入实测偏角」；
CalibTool 用 `--tool-offset auto`。离散度 >5° 提示标记噪声大；若实测值与预期差约
180°，说明标记取在工具另一端，手动 ±180 修正。输出角度范围 (-180,180]，
[0,360) 表示的机器人由 PLC 换算。

**旋转方向自检**（强烈建议）：标定时逐点记录第 4 轴角度（csv 第三列
`像素x,像素y,第4轴角`，或向导点表"第4轴角"列，≥3 个，**录入顺序任意**），标定完成后
自动比对"标记点绕轴心旋转方向"与"第 4 轴角度方向"是否一致——第 4 轴正方向与图像
旋转方向相反的机器人（各品牌 RZ 正方向不一）在此被拦截，而不是带病投产后按
`2r·sin` 放大误差。自检需该工位已有外参档案（点与轴心同过外参映射后比对）。

前提：第 4 轴角度正方向与机器人 XY 系旋转方向一致（右手系逆时针为正），
不一致的机器人请在示教侧取反角度。

注意：角度跨度不足（如 0°/5°/10°）会被拒绝——近距角度使圆拟合病态；
拟合半径 <10px 也会被拒绝（偏心量可忽略无需补偿，且小圆轴心对噪声极敏感）。
标定点分布检查：九点外参要求最大三角形 ≥ 图像面积 1%（挤在角落的局部拟合
对视场其他位置是灾难性外推）；外参留一交叉验证最大误差 >1.0（机器人单位）
计入质量警告（疑似抄错点）。档案目录中同 Id 双档案（手工重命名产生）按
文件名排序先者生效、后者报错，行为确定。

### 多项式标定（单图模式，替代"内参+外参"）

`{工位}.polynomial.json`：一张棋盘格图 → 像素坐标映射的多项式模型
（二阶 6 系数/轴，默认；三阶 10 系数，畸变较大时）。一个模型整体吸收畸变/透视/安装角/像素当量
（VisionPro 式），该工位**推理直接用原图**（跳过内参去畸变与外参仿射）。

两种输出坐标空间（档案 `CoordinateSpace`）：

- **Image（棋盘毫米系，免示教）**：目标坐标 = 网格索引×格距（原点=棋盘首角点，轴=棋盘行列方向）。
  **零示教**：棋盘放平在工件平面 → 拍一张 → 计算 → 保存。适合"只要像素→毫米"（上位机自行换算/
  纯测量）：径向畸变/透视/安装旋转/各向异性/比例系统误差全部吸收，且输出 RMS 残差自检。
  优于单一 mm/px 标量比例（后者假设全图比例均匀，镜头角落 1~3% 漂移不可见）。
  位姿校验/平移合成不适用（无机器人系概念）。
- **Robot（机器人系）**：一张棋盘格图 + 2 个同行参考角点（机器人带针尖对准示教）锚定机器人系，
  输出可直接给机器人。标定流程（向导 Polynomial 模式或 `CalibTool polynomial`）：
  拍一张棋盘格图 → 自动检测全部内角点 → 图上点选同一行的 2 个参考角点（自动吸附亚像素精度）
  → 抄录其机器人坐标 → 棋盘朝向（±90° 行方向歧义）由两种候选拟合的 RMS 自动判定。

适用前提：小畸变镜头、单一工作平面、统一高度（棋盘须放平在与被测面同高的平面）。

检查与防呆：参考点间距与方格边长交叉校验（差 >5% 拒绝）、网格点数 ≥ 系数+4、
推理图像分辨率必须与档案一致、OnArm 位姿校验/合成同外参工位。

**OnArm + ComposeMode=Translate（平移合成）**：相机只有平移、姿态不变时，位置映射
自动加 `(当前TCP − 示教TCP)`——换拍照点**无需重标**，TRIGGER 上报的位姿从"拦截器"
变"合成器"（RZ 仍须一致，超容差 1012）。配 `--compose translate --tcp-x --tcp-y --rz`。
仅 Robot 坐标空间适用。CalibTool 免示教：`polynomial --space image`（无需 --ref）。

旋转中心补偿与多项式工位完全兼容（工具不同心照常补偿）：轴心像素坐标直接经多项式映射
到机器人系，方向自检/偏角实测同样支持。外参工位与多项式工位可并存（每工位一种），多项式优先。

### 比例标定（手动录入，无标定板工位的回退）

`{工位}.scale.json`：像素 → 图像平面毫米的线性比例（mm/px），**不需要标定板**——
现场用卡尺/量块/产品特征/机器人示教测算后，在标定档案页「比例」Tab 手动录入
（X/Y 各一组，或用换算助手"物长 mm ÷ 图上 px"自动算出）。

- **管线分发优先级：多项式 > 外参 > 比例**。工位无外参/多项式档案时，管线自动走比例映射：
  推理直接用原图（跳过内参去畸变，比例以原图为测量基准），TCP 输出**图像平面毫米**
  （原点=图像左上角，X 向右 Y 向下，与 UI 预览同向）；位姿校验/平移合成不适用（无机器人系锚定）。
  工位同时有外参/多项式档案时管线优先机器人系坐标，比例仅用于测量显示（mm 标注），加载时告警提醒。
- **角度语义**：等比比例（X=Y）下角度不变；各向异性（|kx/ky−1|>2%）时方向向量按
  (kx,ky) 缩放重算，且档案标记"近似"并告警（疑似旋转/透视/畸变，建议改用多项式标定）。
- **防呆**：比例须为正数；档案记录分辨率时推理图像必须一致（换分辨率 mm/px 整体失效，1004）；
  覆盖旧档差异 >10% 弹窗确认。
- **精度边界**：线性比例不建模畸变/透视，视场边缘误差受镜头畸变限制（广角可达 2~5%）。
  精度要求高或视场大时仍应使用棋盘格标定（内参+外参或多项式 Image 空间）。

### 相机安装模式（固定机架 / 装在末端）

外参档案记录安装模式（`MountType`，默认 `Fixed`）：

- **Fixed（固定机架，eye-to-hand）**：外参一次标定全工位有效（现状行为）；
- **OnArm（装在末端随动，eye-in-hand）**：档案**仅在标定时记录的拍照位姿下有效**
  （`TeachTcpX/TeachTcpY/TeachRzDeg`）。标定取图与生产拍照必须用同一位姿
  （TCP 与第 4 轴角都一致）；换拍照点/改拍照 RZ 必须重标该工位外参。
  每个拍照位姿一组档案（stationId 按工位+拍照点命名，如 `st1_pick1`）。

九点标定流程对两种模式完全相同（OnArm 时带针走 9 点的拍照位姿 = 生产拍照位姿）。
标定向导选择安装模式并记录位姿；CalibTool 用 `--mount onarm --tcp-x --tcp-y --rz`。

**标定平面 Z**（`CalibrationPlaneZ`，可选）：九点外参是单平面仿射，零件高度差
引入透视误差（≈ 视场偏移 × Δh / 工作距离；WD=200mm、Δh=10mm、偏离光轴 80mm
→ 约 4mm）。高度差大的产线按料厚分层标定多组档案（每料厚一组 stationId）。

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

`ModelManager` 按 **(模型路径, 文件版本, 推理任务)** 缓存 Yolo 实例，加载后空跑一帧预热：

- **单次物化**：缓存值为 `Lazy<T>`，并发 `Open` 同一模型只有一个加载真正执行——
  输家的包装对象未物化即被丢弃，不会泄漏 ONNX 会话（数百 MB 级）；
- **失败不缓存**：模型文件缺失/预热异常的加载不留缓存，后补模型文件后可立即重试；
- **预热失败也清理**：Yolo 已创建但预热抛错时立即 Dispose，不挂半初始化会话；
- **同模型推理串行**：Yolo 实例非线程安全，`ModelSession.Run` 内部信号量串行化；
- **任务参与缓存键**：同一文件被不同任务打开时行为确定（各自的预热各自成败），
  而非"谁先加载谁的任务赢"（注意：多任务各占一份会话内存，UI 会提示双份缓存）；
- **文件版本参与缓存键**（mtime+大小）：替换 `.onnx` 后下次推理自动加载新版本
  并清理旧会话——不依赖人工"先卸载再推理"，杜绝旧模型静默服务；
- **安全卸载**：卸载/裁剪/退出先置卸载标记、再等待在途推理离开临界区、最后释放
  引擎（幂等）——绝不 Dispose 正在推理的会话；已失效会话的后续 Run 返回
  ModelNotAvailable（可重试，重试自动加载新会话）；
- **LRU 上限可配**：`Inference:MaxSessions`（默认 8，0 = 不限），超限回收最久未用会话。


## 配方示例（recipes/A01.json）

```json
{
  "cameraId": "cam_file",
  "stationId": "st1",
  "angleMode": "KeyPointLine",
  "models": [ "a01_kpt.onnx" ],
  "confidence": 0.5,
  "iou": 0.7,
  "keypoint": { "indexA": 0, "indexB": 1, "minConfidence": 0.3 },
  "rotationCompensation": "None"
}
```

策略专属参数收敛为能力子对象：`keypoint`（KeyPointLine）/ `dualModel`（DualCenterLine，
含 `pairingMaxDistancePx`）/ `segmentation`（MaskMinAreaRect，含 `pixelConfidence`）。
**旧版平铺字段（`keypointIndexA` 等）仍可读取**——setter-only 兼容属性在加载时自动迁移到
子对象，存量配方文件无需手工改写；保存时统一输出子对象格式。

**输出补偿**（`outputOffset`，可选）：标定与偏心工具补偿之后，对每个目标叠加 ΔX/ΔY/ΔRz（mm/°）。
首件微调用，缺省全 0。绝对值超过 100mm / 180° 会在保存时拒绝。

**资产钉扎**（`modelSha256` / `stationSha256`，可选）：配方页「钉死当前哈希」写入 SHA-256。
钉扎后替换同名 ONNX 或覆盖标定档案，TRIGGER 返回 **1017**。九点外参工位的工位指纹包含去畸变内参；质量字段与标定时间不参与哈希。未钉扎的旧配方行为不变。
`AssetIntegrity:RequireManifest=true` 时还要求 `models/manifest.json` 中有对应条目。

**过程能力**：成功/失败按日追加 `data/metrics/yyyyMMdd.tsv`，累计写入 `health.json`（重启不丢）。
同配方连续过程失败（1003/1005/1007/1008 等）达到 `ProcessHealth:ConsecutiveFailLimit`（默认 5）
后 TRIGGER 返回 **1018**（入队前拒绝，不占队列、不计入失败次数）。通信页「解除联锁」或 TCP `CLEARINHIBIT` / `CLEARINHIBIT,配方名` / `CLEARINHIBIT,#3`。
配置类错误（1001/1004/1012/1017）不计入连续失败。

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

**`stationId` 是安全项**：缺省或未做外参/多项式/比例标定时，检出目标后返回
1004 错误，绝不静默回退为像素坐标——像素坐标的数值量级与机器人坐标相近，
被机器人当成真坐标使用会造成撞机风险。无标定板工位可用比例标定输出图像平面毫米坐标。
`rotationCompensation` 默认 `None`；工具与第 4 轴偏心时设为 `EccentricTool`
（需先做旋转中心标定，见上节）。

## 更换推理后端

推理引擎已抽象为 `IInferenceEngine`（检测/分割/关键点三个入口，与 YoloDotNet 签名一致），
`ModelManager` 通过 `IInferenceEngineFactory` 创建引擎——**策略层、VisionService、UI 均不感知具体框架**。
默认实现 `YoloDotNetEngineFactory` 使用 **OpenVINO 核显**（`YoloDotNet.ExecutionProvider.OpenVino`），
由 appsettings 的 `Inference:Provider` 配置（默认 `OpenVinoGpu`）。
YoloDotNet **每个进程只能引用一种 Execution Provider 包**，因此仓库已从 CPU EP 换成 OpenVINO，不能再在同一进程里混编 Microsoft CPU EP。

可用值：

- `OpenVinoGpu` / `Gpu` / `OpenVino`：Intel 核显（FP16），现场对比单枪更快；创建失败则回退 OpenVINO CPU 并在日志打警告；
- `OpenVinoCpu` / `Cpu`：直接 OpenVINO CPU（无核显或对照）。

同一模型仍是一份会话 + 一把锁，**不要**做 CPU+GPU 混池。两路 TRIGGER 重叠是否开第二会话，用 `tools/RobotVision.InferenceBench` 在工控机上测过再定。

换整个推理框架（如 ONNX Runtime 直连）＝实现 `IInferenceEngine` + `IInferenceEngineFactory` 替换注册，
同样零改动策略层。`ModelSession.Run(Func&lt;IInferenceEngine,T&gt;)` 内信号量串行化语义保留（引擎实例非线程安全）。

## 并发与线程安全

- `VisionService` 通过信号量串行化整个管线，并发 TRIGGER 排队执行；
- 每个模型的 Yolo 实例通过 `ModelSession` 内部信号量串行化（Yolo 非线程安全）；
- 模型加载后空跑一帧预热，避免首次请求耗时突增。
