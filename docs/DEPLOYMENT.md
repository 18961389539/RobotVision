# 部署与升级手册

适用对象:产线工控机部署 / 现场升级 / 数据备份。
配套产物:GitHub Release 中的 `RobotVision-win-x64.zip`(自包含,免装 .NET 8)。

---

## 1. 部署形态与工控机要求

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10/11 x64(工控机建议 Windows 10 LTSC) |
| 运行时 | 无需安装 .NET 8(自包含包已内置) |
| 相机驱动 | GigE 相机:网卡 IP 与相机同网段,放行 UDP 3956;Basler:安装 pylon 运行库 |
| 推理 | Intel 核显（OpenVINO GPU）。GPU 不可用时自动回退 OpenVINO CPU（日志有警告）。也可把 `Inference:Provider` 改为 `OpenVinoCpu` |
| 磁盘 | 至少 2GB(含失败现场图/指标留存) |

## 2. 目录结构(解压后)

```
RobotVision/
├── RobotVision.Wpf.exe      主程序
├── appsettings.json         运行配置(相机/PLC/阈值)
├── recipes/                 配方文件(*.json)—— 现场数据,需备份
├── models/                  模型文件(*.onnx)—— 不入库,手动放置
├── data/
│   ├── calibration/         标定档案(内参/外参/旋转中心)—— 现场数据,需备份
│   ├── captures/            采集留档
│   ├── failures/            失败现场图(滚动保留,默认 200 张)
│   ├── results/             结果日志(JSON Lines 按天,成功/失败留档,默认保留 30 天)
│   ├── captures/            成功产品现场图(开关 CaptureSuccess.Enabled,默认关)
│   └── metrics/             过程健康指标(联锁依据,默认保留 90 天)
└── logs/                    Serilog 日志
```

> **models/ 需手动放置**:`.onnx` 不入 Git 仓库。从开发机 `models/` 目录拷贝,或按部署说明从内部共享盘获取。

## 3. 首次部署步骤

1. 从 GitHub Releases 下载 `RobotVision-win-x64.zip`(需与配方/标定兼容的版本)
2. 解压到目标目录(如 `D:\RobotVision`),路径**不要含中文/空格**优先
3. 放置模型文件:`models/a01_kpt.onnx`、`OSFP-SEG.onnx` 等
4. 编辑 `appsettings.json`:
   - `TcpPort`:与 PLC 约定的端口(默认 9999)
   - `Cameras`:按实际相机改 `Id` / `Type`(GigE/Basler/File/Virtual)/ `DeviceId`
   - `PoseCheck`:OnArm 工位的位姿容差(默认 X/Y 0.5mm、RZ 0.5°)
5. 拷贝现场数据到 `recipes/` 与 `data/calibration/`(新机或产线迁移时)
6. 启动 `RobotVision.Wpf.exe`,界面确认:相机在线、配方可加载、标定档案无告警
7. 用冒烟脚本验证:`powershell -File tools/smoke-test.ps1 -Commands @("PING","STATUS","<配方名>")`

## 4. 升级步骤(旧版 → 新版)

> 核心原则:**数据目录不动,只换程序文件**。

1. 备份(见第 5 节)
2. 停掉运行中的 `RobotVision.Wpf.exe`(确认进程已退出)
3. 把新包解压到**临时目录**,仅复制以下内容覆盖旧目录:
   - 新版的 `RobotVision.Wpf.exe` 与程序集文件(`*.dll` 等,即发布产物)
   - **不要覆盖**:`appsettings.json`、`recipes/`、`data/`、`models/`、`logs/`
4. 启动新版,验证:
   - `STATUS` 返回 `OK,ready,...`
   - 触发配方:成功返回 `OK,坐标...` 或按预期错误码
   - 界面无标定/资产校验告警
5. 若现场异常:用第 3 步的备份恢复旧版程序文件即可回滚

> 小版本配置兼容:配方/标定档案向后兼容;若新版改了协议或配置结构,升级手册会随 Release 说明标注。

## 5. 备份清单(现场数据)

| 内容 | 位置 | 说明 |
|---|---|---|
| 配方 | `recipes/*.json` | 每次改配方后备份 |
| 标定档案 | `data/calibration/*` | 内参/外参/旋转中心/多项式/比例,换机必须迁移 |
| 运行配置 | `appsettings.json` | 相机/PLC/阈值 |
| 模型 | `models/*.onnx` | 版本需与配方钉扎哈希一致(见 1017) |
| 失败现场 | `data/failures/` | 排障取证,可定期归档 |

建议:每周或每次发版前,将上述目录打包为 `robotvision-backup-<日期>.zip` 存放。

## 6. 常见运维操作

| 操作 | 方式 |
|---|---|
| 查看运行状态 | TCP `STATUS` → `OK,ready|busy,队列深度,上限,最近耗时ms` |
| 手动触发 | 界面「手动触发」或 TCP 配方名（如 `A01`；旧 `TRIGGER,A01` 会 1013） |
| 解除过程联锁 | 界面「解除联锁」或 TCP `CLEARINHIBIT`(1018 触发后) |
| 查看日志 | `logs/` 下 Serilog 文件(按天滚动) |
| 定位失败原因 | 先查 `data/failures/` 最近现场图,再对 [错误码总表](./ERROR-CODES.md) |

## 7. 版本与固件对应

- 每个 Release 对应一个 Git tag(如 `v1.0.0`),tag 即产线固件版本号
- 现场记录:工位台账登记「版本号 + 部署日期 + 配方清单」
- 升级前先看 Release 说明:新增/修复/破坏性变更

---

## 附录:appsettings.json 关键项速查

| 键 | 默认 | 说明 |
|---|---|---|
| `IpAddress` / `TcpPort` | 0.0.0.0 / 9999 | TCP 监听 |
| `TimeoutMs` | 90000 | 单次触发超时 |
| `MaxQueueDepth` / `MaxConcurrent` | 4 / 2 | 排队上限 / 管线工位槽（同模型推理仍串行） |
| `Inference:Provider` | OpenVinoGpu | OpenVinoGpu 或 OpenVinoCpu；GPU 失败且模型有效时回退 CPU |
| `RecipesFolder` / `ModelsFolder` | recipes / models | 相对 exe 目录 |
| `CalibrationFolder` | data/calibration | 标定档案目录 |
| `FailureImage.Enabled/RetainedCount` | true / 200 | 失败现场留存 |
| `PoseCheck.Enabled/XyToleranceMm/RzToleranceDeg` | true / 0.5 / 0.5 | OnArm 位姿校验容差 |
| `AssetIntegrity.Enabled` | true | 模型/标定哈希校验(1017) |
| `ProcessHealth.ConsecutiveFailLimit/InhibitOnLimit` | 5 / true | 连续失败联锁(1018) |
| `ResultLog.Enabled/Folder/RetainedDays` | true / data/results / 30 | 成功/失败结果留档(JSON Lines) |
| `CaptureSuccess.Enabled/Folder/MaxWidth` | false / data/captures / 0 | OK 产品存图开关;MaxWidth>0 缩图 |
| `Cameras[].Type` | — | GigE / Basler / File / Virtual |
