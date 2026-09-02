# 部署与升级手册

本文说明 RobotVision 在工控机上的目录布局、配置分层、首次启动行为与版本升级/回滚。

> PLC 通信见 [PLC-TRIGGER-Protocol.md](PLC-TRIGGER-Protocol.md)。错误码见 [ERROR-CODES.md](ERROR-CODES.md)。

## 系统要求

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10/11 x64（产线推荐 LTSC） |
| 运行时 | 自包含发布包**无需**单独安装 .NET；框架依赖包需 .NET 10 Runtime |
| 推理 | Intel CPU；核显可选（OpenVINO GPU，默认 `Inference:Provider = OpenVinoGpu`） |
| 网络 | 相机网口 + PLC 网口；防火墙放行 `TcpPort`（默认 9999） |
| 显示 | 当前版本为 **WPF 桌面应用**；需登录桌面会话（无独立 Windows 服务形态） |

## 获取安装包

- **CI 产物**：推送到 `main` 后 GitHub Actions 上传 `RobotVision-win-x64` Artifact（自包含、多文件目录）。
- **正式发布**：打 `v*` 标签（如 `v1.0.0`）后 Release 页下载 `RobotVision-win-x64.zip`。
- **本地构建**：

```powershell
dotnet publish src/RobotVision.Wpf/RobotVision.Wpf.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -o publish-out
```

> 产线推荐多文件目录发布（单文件易被杀软误报）。

## 目录布局

### 便携部署（推荐小规模/调试）

将发布目录与资产放在同一文件夹，例如：

```
RobotVision/
├── RobotVision.Wpf.exe
├── appsettings.json          # 随包只读模板
├── models/                   # ONNX（不入库，现场自行放置）
│   └── a01_kpt.onnx
├── recipes/                  # 可选：示例配方（首次可拷入 DataRoot）
└── data/                     # 可选：旧版布局，首次启动可迁移
```

在用户 `appsettings` 中将 `DataRoot` 设为 `"."` 可把工位数据锚定在 exe 旁（便携模式）。

### 标准部署（推荐产线）

默认工位数据在 **ProgramData**，避免写入 Program Files：

| 路径 | 内容 |
|---|---|
| `%ProgramData%\RobotVision\appsettings.json` | **可写**用户配置（UI 保存目标） |
| `%ProgramData%\RobotVision\Data\` | 工位数据根（`DataRoot` 默认） |
| `%ProgramData%\RobotVision\Data\recipes\` | 配方 JSON |
| `%ProgramData%\RobotVision\Data\calibration\` | 标定档案 |
| `%ProgramData%\RobotVision\Data\failures\` | 失败现场图 |
| `%ProgramData%\RobotVision\Data\results\` | 结果 jsonl / SQLite |
| `%ProgramData%\RobotVision\Data\metrics\` | 过程能力 TSV + health.json |
| `%ProgramData%\RobotVision\Data\logs\` | Serilog 滚动日志（若 `FileLogging:Folder` 为相对路径） |
| `<exe>\models\` | 模型目录（**不**随 DataRoot 绑定，默认 exe 旁） |

exe 旁 `appsettings.json` 为**只读模板**；首次启动会自动复制到 `%ProgramData%\RobotVision\appsettings.json`（若不存在）。

## 配置分层

加载顺序（后者覆盖前者）：

1. `<exe>\appsettings.json` — 随包默认
2. `%ProgramData%\RobotVision\appsettings.json` — 用户/机器配置
3. `<exe>\appsettings.Development.json` — 可选本地覆盖（不入库）

关键字段见 `src/RobotVision.Wpf/appsettings.json` 与 README「运行」一节。

## 环境变量

| 变量 | 作用 |
|---|---|
| `ROBOTVISION_USER_DATA` | 覆盖 `%ProgramData%\RobotVision` 根目录 |
| `ROBOTVISION_SETTINGS` | 覆盖可写 `appsettings.json` 路径 |
| `ROBOTVISION_DATA_ROOT` | 覆盖 `DataRoot`（优先于配置文件） |

示例（便携 + 自定义数据盘）：

```powershell
$env:ROBOTVISION_DATA_ROOT = "D:\RobotVisionData"
.\RobotVision.Wpf.exe
```

## 首次启动

1. `EnsureUserSettings()`：创建用户目录，复制随包 `appsettings.json`。
2. `NormalizeAppConfig()`：空 `DataRoot` → `%ProgramData%\RobotVision\Data`。
3. `DataRootBinder.Apply()`：将相对路径（配方、标定、日志等）绑到 DataRoot。
4. `CopyLegacyIfEmpty()`：若 DataRoot 下目录为空，从 exe 旁 `recipes/`、`data/` 等迁移旧布局文件。
5. 预加载配方并校验；无效配方记入启动日志且不可触发。

**注意**：以 Windows 服务/计划任务启动时，当前工作目录常为 `System32`——务必使用上述 DataRoot 机制，不要把相对路径依赖 CWD。

## 运行

```powershell
# 开发（仓库根）
dotnet run --project src/RobotVision.Wpf

# 产线（发布目录）
.\RobotVision.Wpf.exe
```

- 单实例：同一机器重复启动会静默退出（防双开抢 TCP 端口）。
- TCP 服务随宿主启动（`TcpHostedService`）；通信页可停止/重启监听。

## 防火墙与网络

```powershell
New-NetFirewallRule -DisplayName "RobotVision TCP" `
  -Direction Inbound -Protocol TCP -LocalPort 9999 -Action Allow
```

相机 GigE：确保网卡与相机同网段，必要时关闭该网卡节能；Basler 需安装 pylon 运行库。

## 升级步骤

1. **备份**（升级前必做）：
   - `%ProgramData%\RobotVision\appsettings.json`
   - `%ProgramData%\RobotVision\Data\` 整个目录（含配方、标定、metrics）
   - `models\` 与现场 ONNX
2. **停止**应用（含通信页 TCP 服务）。
3. **解压**新版本到安装目录（覆盖 exe 与随包 DLL；**不要**删除 DataRoot）。
4. **对比**新版 `appsettings.json` 是否有新增配置段，按需合并到用户 `appsettings.json`。
5. **启动**应用，检查：
   - 启动日志无配方校验错误
   - 通信页 TCP 监听正常
   - `PING` / `STATUS` / 试触发一条配方
6. 若使用**资产钉扎**（1017）：升级后模型/标定文件未变则无需操作；若替换 ONNX 须重新钉扎或恢复旧文件。

版本号 = Git 标签 `vX.Y.Z`（见 [CHANGELOG.md](../CHANGELOG.md)）。

## 回滚

1. 停止应用。
2. 用备份的旧版发布包覆盖安装目录。
3. 恢复升级前的 `appsettings.json` 与 `Data\`（若升级中改过配置或数据）。
4. 启动并做 TRIGGER 冒烟。

> 结果库 SQLite（`results.db`）向前兼容；跨大版本回滚若 schema 变更，分析页可能需重建库（jsonl 留档仍在）。

## 日志与排障

| 来源 | 路径 |
|---|---|
| 文件日志 | `Data\logs\robotvision-yyyyMMdd.log`（默认） |
| 失败现场 | `Data\failures\` |
| 结果留档 | `Data\results\*.jsonl` + SQLite |
| 过程能力 | `Data\metrics\health.json`、按日 TSV |
| 工艺助手审计 | `Data\chat-audit\` |

工控机无头时，**文件日志**是主要留痕；确保 `FileLogging:Enabled = true` 且磁盘空间充足。

## 推理后端切换

修改用户 `appsettings.json`：

```json
"Inference": {
  "Provider": "OpenVinoGpu",
  "MaxSessions": 8
}
```

须在**目标工控机**上用 `tools/RobotVision.InferenceBench` 对比 CPU/GPU 后再定产线值。修改后保存设置即可热应用（无需重装）。

## 破坏性变更

发布时 [CHANGELOG.md](../CHANGELOG.md) 的「破坏性变更」节会列出不兼容项；升级前请通读对应版本说明。

## 相关文档

- [PLC-TRIGGER-Protocol.md](PLC-TRIGGER-Protocol.md)
- [ERROR-CODES.md](ERROR-CODES.md)
- [README.md](../README.md) — 构建、标定、配方示例
