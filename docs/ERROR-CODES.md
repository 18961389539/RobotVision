# 错误码总表

TCP 失败应答格式：`ERR,<错误码>,<消息>`。成功为 `OK,...`（见 [PLC-TRIGGER-Protocol.md](PLC-TRIGGER-Protocol.md)）。

源码定义：`src/RobotVision.Core/Models/VisionResult.cs` → `VisionErrorCode`。

## 一览

| 码 | 枚举名 | 含义 | 典型处置 |
|---|---|---|---|
| 1000 | UnknownCommand | 未知命令 | 检查报文；非 PING/STATUS/CLEARINHIBIT 的非法行 |
| 1001 | UnknownRecipe | 配方不存在 | 核对配方名/序列号；检查 `recipes/` 下是否有对应 JSON |
| 1002 | CameraNotRegistered | 相机未注册 | 在相机页注册或修正配方 `cameraId` |
| 1003 | CameraGrabFailed | 取图失败 | 查网线/曝光/相机电源；查看失败现场图与日志 |
| 1004 | NotCalibrated | 未标定 | 完成内参+外参/多项式/比例标定；检查 `stationId` |
| 1005 | ModelNotAvailable | 模型不可用 | 确认 `models/` 下 ONNX 存在；检查推理 EP |
| 1006 | LightNotRegistered | 光源控制器未注册 | 注册光源或去掉配方中的 `lightControllerId` |
| 1007 | NoTargetFound | 未检出目标 | 查光照/阈值/来料；对比失败现场 PNG |
| 1008 | Timeout | 处理超时 | 已进入推理；见协议文档重试策略 |
| 1009 | Busy | 排队超限 | 降低触发频率；增大 `MaxQueueDepth` 或 `MaxConcurrent` |
| 1010 | QueueTimeout | 排队超时 | 未进入推理；可立即重试 |
| 1011 | CameraInitFailed | 相机初始化失败 | pylon 运行库/驱动/序列号；区别于 1003 |
| 1012 | PoseMismatch | 拍照位姿不一致 | OnArm 工位核对 TCP 位姿与标定示教位姿 |
| 1013 | InvalidTriggerArgument | TRIGGER 格式错误 | 段数须为 1 或 4；X/Y/RZ 须为有限数字 |
| 1014 | PoseRequired | 未上报拍照位姿 | OnArm 工位必须使用 `键,X,Y,RZ` 四段格式 |
| 1015 | RecipeDisabled | 配方已停用 | 在配方页启用（`Enabled=true`） |
| 1016 | InvalidRecipeConfig | 配方参数无效 | 打开配方页修正 cameraId/模型/阈值等 |
| 1017 | AssetMismatch | 资产哈希不一致 | 模型或标定被替换；重新「钉死当前哈希」或恢复文件 |
| 1018 | ProcessUnhealthy | 连续失败联锁 | `CLEARINHIBIT` 或界面解除；排除根因后再产 |
| 1019 | RefineFailed | 精修失败 | 分割有目标但角度/头尾不过门；查精修参数与照明 |
| 1020 | LightCommandFailed | 光源指令失败 | Id 正确但硬件未点亮；查接线/协议/电源 |
| 1099 | InternalError | 内部错误 | 协议固定 `INTERNAL_ERROR`；查 `logs/` 详细堆栈 |

## 协议固定消息模板

以下消息为固定 ASCII 模板（不经业务 Sanitize）：

| 场景 | 应答示例 |
|---|---|
| 内部错误 | `ERR,1099,INTERNAL_ERROR` |
| 未知命令 | `ERR,1000,UNKNOWN_COMMAND` |
| 空触发行 | `ERR,1001,MISSING_RECIPE` |
| TRIGGER 段数错误 | `ERR,1013,TRIGGER_ARGUMENT_COUNT` |
| TRIGGER 数值非法 | `ERR,1013,INVALID_POSE_NUMBER` |

## 失败现场留存

取图成功后的失败（1007、1005、1019、1099 等）会将**去畸变后的现场图**写入 `failures/`（PNG + JSON 元数据）。

取图前失败（1001–1004、1006、1011–1014、1017–1018 等）**无现场图**可留。

配置：`FailureImage` 段（`Enabled`、`Folder`、`RetainedCount`）。

## 连续失败联锁（1018）

计入连续过程失败（达到阈值后可能联锁）的错误码：

- 1003 取图失败
- 1005 模型不可用
- 1007 未检出
- 1008 处理超时
- 1011 相机初始化失败
- 1019 精修失败
- 1020 光源指令失败
- 1099 内部错误

**不计入**（避免误锁）：

- 1001 配方不存在、1004 未标定、1012 位姿不一致、1015 停用、1016 配置无效、1017 资产哈希、1018 联锁本身
- 1009 Busy、1010 排队超时

定义见 `src/RobotVision.Core/Models/ProcessFailureCodes.cs`。

## 按阶段速查

| 阶段 | 可能错误码 |
|---|---|
| 入队前 | 1009、1010、1018 |
| 配方/配置 | 1001、1015、1016、1017 |
| 位姿校验 | 1012、1013、1014 |
| 光源 | 1006、1020 |
| 取图 | 1002、1003、1011 |
| 标定/坐标 | 1004 |
| 推理 | 1005、1007、1019、1008 |
| 其它 | 1099 |

## 相关文档

- [PLC-TRIGGER-Protocol.md](PLC-TRIGGER-Protocol.md) — 超时重试、STATUS、CLEARINHIBIT
- [DEPLOYMENT.md](DEPLOYMENT.md) — 日志目录、失败图路径、升级回滚
