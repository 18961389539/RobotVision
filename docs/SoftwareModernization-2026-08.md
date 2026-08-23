# RobotVision 软件现代化建议

> 分析日期：2026-08-23　|　基于当前仓库（net8.0，8 个项目，约 1.5 万行 C#）
> 适用范围：本文仅给建议，不含代码改动。每项均标注优先级、理由与落地步骤。

---

## 0. 现状速评

**总体判断：代码质量明显高于同规模工业视觉项目，现代化的短板不在"代码写法"，而在"工程化基建"。**

### 已做得好的（建议保持）

| 维度 | 现状 |
|---|---|
| 分层架构 | Core / Infrastructure / Hosting / App / UI 五层，依赖方向正确（Core 不依赖任何外部包，仅 OpenCvSharp） |
| 现代 C# 用法 | record、主构造函数、Nullable、ImplicitUsings、`Lazy<T>` 单次物化、`ConcurrentDictionary`、`Volatile.Read`、`Mat.Data` 直写 |
| 并发模型 | 相机粒度串行 / 模型粒度串行 / 并发槽位 + 防僵尸超时设计（1008/1009/1010 语义完备） |
| 可靠性工程 | 失败现场留存（PNG+JSON 成对）、错误码契约、异常详情只进日志不上协议线、路径穿越防护 |
| 测试 | 约 4000 行 xUnit（几何/标定/协议/队列/日志/失败留存） |
| 文档 | README 详尽，含协议表、错误码表、部署布局说明 |

### 主要差距（本文要解决的）

| 差距 | 影响 |
|---|---|
| **无版本控制**（仓库无 `.git`） | 无法追溯、回滚、协作、做 CI；产线事故无法定位"哪个版本在跑" |
| **.NET 8 即将 EOL**（2026-11-10） | 不到 3 个月后无安全补丁；工控系统暴露面最不能接受 |
| **无 CI/CD** | 构建/测试全靠本地手动，交付物不可复现 |
| **依赖版本陈旧且不一致** | ONNX Runtime 1.23.0（最新 1.29.0）；`Microsoft.Extensions.*` 8.0.1/8.0.2 混用 |
| **无静态分析门禁** | 无 `.editorconfig`、无 analyzers、无 `TreatWarningsAsErrors` |
| **配置无校验** | `Get<AppConfig>()` 手动绑定，配置错误运行期才暴露 |
| **部署靠手动 copy** | 无自包含发布脚本、无服务化包装、无回滚策略 |

---

## 1. 现代化路线图（优先级一览）

```
P0 ─ 现在做（2~3 个月内）────────────────────────────────────
 1. 初始化 Git 版本控制（一切的前提）
 2. 升级 .NET 8 → .NET 10 LTS（EOL 倒计时）
 3. 建立 CI/CD 流水线（build + test + publish）
 4. 依赖统一与升级（CPM 中央包管理 + ONNX Runtime 升级）
 5. 引入静态分析门禁（.editorconfig + analyzers）

P1 ─ 1~2 个季度内 ──────────────────────────────────────────
 6. 配置改为 Options pattern + 启动校验
 7. 可观测性补强（指标暴露，可选 OpenTelemetry）
 8. 部署现代化（自包含发布 + 服务化 + 版本化回滚）
 9. 大文件拆分（TcpServerManager 602 行 / CamerasViewModel 888 行等）

P2 ─ 中期（半年内）─────────────────────────────────────────
10. 模型资产管理（manifest + 版本绑定）
11. 测试补强（TCP 端到端集成测试、数据驱动标定测试）
12. 推理加速评估（OpenVINO / DirectML，节拍紧张时）

P3 ─ 前瞻（按需）───────────────────────────────────────────
13. ADR 决策记录、UI 跨平台评估（Avalonia）、TCP 加密（跨网部署时）
```

---

## 2. P0 —— 现在做

### 2.1 初始化 Git 版本控制

**理由**：仓库根目录无 `.git`。没有版本控制，就没有可追溯性、回滚能力和 CI/CD 的前提。对产线系统，"现场跑的版本 = 哪个 commit" 是安全事故排查的第一问题。

**做法**：
1. `git init`，复用现有 `.gitignore`（bin/obj/logs/data/models 已覆盖）；
2. 首次提交前确认 `data/calibration/` 是否入库（README 说"按需自行纳入版本管理"——建议**标定结果入库**，因为它是产线正确性的关键资产，且是 JSON 文本适合 diff）；
3. 建立分支策略：`main`（只合可发布版本）+ 功能分支 + `release/` 标签；
4. 用 **MinVer**（基于 tag 自动生成版本号）或 GitVersion，让 `FileVersionInfo` 与 commit 一一对应——日志里能直接看到版本号。

### 2.2 升级 .NET 8 → .NET 10 LTS

**理由（紧迫）**：.NET 8 与 .NET 9 均于 **2026-11-10 结束支持**，之后无安全补丁。.NET 10 是 LTS（2025-11 发布，支持至 2028-11-14）。工控系统长驻产线、暴露于现场网络，且通常不会频繁升级，跳到 LTS 是最优解。

**做法**：
1. 所有 csproj 的 `TargetFramework` 改 `net8.0` → `net10.0`（UI 项目改 `net10.0-windows`）；
2. 用 .NET Upgrade Assistant 或 `dotnet try-convert` 过一遍；
3. **重点验证兼容性**（本项目的三个高风险点）：
   - `YoloDotNet 4.2.0` / `YoloDotNet.ExecutionProvider.Cpu`：确认其目标框架与 ONNX Runtime 依赖在 net10 下可运行；
   - `Basler.Pylon.NET.x64`（net40 老程序集，NU1701 兼容模式）：net10 下托管加载行为需实测；
   - `OpenCvSharp4 4.10` 原生库：与 .NET 版本无关，风险低；
4. 测试全套跑绿后再切 CI。

### 2.3 建立 CI/CD 流水线

**理由**：当前 `dotnet build/test` 全手动。没有流水线，就没有"可复现的构建产物"，发布物不可审计。

**做法**（按现有基建选型，最低成本方案）：
- **GitHub Actions**（仓库在公网可托管时）或 **Azure DevOps / 内网 Jenkins**（代码不便出内网时）；
- 流水线四步：
  1. `dotnet build -c Release`（含 2.5 的静态分析）；
  2. `dotnet test`（门禁：失败即红）；
  3. `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`（App 与 CalibTool 各一份）；
  4. 产物归档（带版本号 zip），UI 版本（net10.0-windows）单独构建。
- 版本号由 MinVer 从 tag 生成，`Version` 写入程序集与日志。
- 可加 nightly 构建 + 30 天保留，产线回归用。

### 2.4 依赖统一与升级

**现状问题**：
- `Microsoft.Extensions.Hosting 8.0.1`（App）与 `8.0.2`（Hosting 内 `DependencyInjection.Abstractions`）混用；
- ONNX Runtime `1.23.0` → 最新稳定 **1.29.0**（2026-08 发布），落后 6 个主版本，含安全修复与推理性能改进；
- 无中央版本管理，新包版本靠手填。

**做法**：
1. 根目录新建 **`Directory.Packages.props`**（Central Package Management）统一版本，项目里只写 `PackageReference` 不带版本；
2. `Microsoft.Extensions.*` 全部统一到同一个小版本；
3. ONNX Runtime 升级到 1.29.x，**回归验证** `YoloDotNet` 兼容性（其底层依赖 ONNX Runtime，需确认 4.2.0 支持 1.29）；
4. 其他可顺带更新的：`CommunityToolkit.Mvvm 8.3.2` → 8.4+、`Serilog 4.0.2` → 最新、`WPF-UI 3.1.1` → 最新稳定（WPF-UI 已进入 4.x，UI 控件部分如不涉及大改可暂缓）。
5. 启用 `dotnet list package --vulnerable`（NuGet 审计）作为 CI 检查项，第一时间发现已知漏洞包。

### 2.5 引入静态分析门禁

**理由**：仓库无根 `.editorconfig`、无 analyzers 配置，编译警告无约束，坏味道会悄悄积累（当前 888 行的 ViewModel 就是先例）。

**做法**：
1. 根目录 `.editorconfig`（统一缩进/命名风格）+ `Directory.Build.props` 全局开启 `<AnalysisLevel>latest-recommended</AnalysisLevel>`；
2. 视团队偏好追加 Roslynator / StyleCop（**谨慎**：StyleCop 风格化规则多、噪音大，建议先用微软默认 recommended 规则集跑一轮，存量告警清零后再开 `TreatWarningsAsErrors`）；
3. CI 里 `-warnaserror`，让分析器成为门禁。

---

## 3. P1 —— 1~2 个季度内

### 3.1 配置改为 Options pattern + 启动校验

**现状**：`Program.cs` / `App.xaml.cs` 均用 `builder.Configuration.Get<AppConfig>()` 手动绑定，无校验——配置写错（如 `GrabTimeoutMs >= TimeoutMs`、端口冲突、路径非法）要运行期才炸。

**做法**：
1. `AddOptions<AppConfig>().Bind(cfg).ValidateDataAnnotations().ValidateOnStart()`；
2. 关键跨字段约束写 `IValidateOptions<AppConfig>`（README 已手工实现了不少这类校验，把它们从"启动告警"提升为"启动失败"级别的强约束，可选保留告警语义）；
3. 两个宿主（App/UI）共享同一校验逻辑（放 Hosting 层）。

### 3.2 可观测性补强

**现状**：Serilog 结构化日志（很好）+ VisionService 自建 128 次滚动健康窗口 + STATUS 命令。缺的是**长期趋势数据**——产线良率/节拍漂移没有历史曲线。

**做法（轻量优先，别上重武器）**：
1. 把现有健康窗口/`RecipeStatsSnapshot` 追加一个 **TSV/JSON 追加日志**（按配方：成功率、P95、节拍），一天一个文件，现场可查趋势；
2. 若未来要上产线看板：加 Prometheus `/metrics` 端点（`prometheus-net`）或 OpenTelemetry 指标，暴露 `vision_requests_total / vision_success_rate / vision_p95_ms / queue_depth`；
3. 保留 STATUS 协议不动（PLC 侧契约稳定优先）。

### 3.3 部署现代化

**现状**：README 提到"Windows 服务/计划任务启动时 CWD 是 System32，部署时务必把资产放 exe 旁"——说明现场是手动 copy + 计划任务/服务，无版本化、无回滚。

**做法**：
1. **自包含单文件发布**（`--self-contained -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`）：工控机免装 .NET 运行时，ONNX/OpenCvSharp 原生库打进单文件（原生库较大的话评估提取模式）；
2. **服务化包装**：`sc.exe` 或 WinSW/NSSM 将 App 注册为 Windows 服务（崩溃自动拉起、开机自启）；
3. **版本化发布目录**：`RobotVision\v2026.08.11\...` + `current` 符号链接切换，回滚 = 切链接（产线回滚要 5 分钟内可完成）；
4. 发布产物 md5 清单随包，现场核对完整性。

### 3.4 大文件拆分

**现状**（代码行数前几）：
- `RobotVision.UI/CamerasViewModel.cs` 888 行
- `RobotVision.UI/RecipeViewModel.cs` 654 行
- `Infrastructure/Communication/TcpServerManager.cs` 602 行（监听/连接注册表/行协议/应答/超时/白名单全在一个类）
- `Hosting/VisionService.cs` 446 行

**做法**：
1. `TcpServerManager` 按职责拆：`TcpListenerHost`（生命周期/连接管理）+ `LineProtocolCodec`（解析/序列化/消毒）+ `SessionRegistry`（客户端跟踪/白名单/限流）——**纯重构，协议行为不变**，用现有协议单测兜底；
2. ViewModel 用 partial class 按区域拆分（项目里 `ImageViewer` 已大量使用该模式，团队熟练）；
3. 拆分前后跑全套测试 + 一次真实 PLC 联调回归。

---

## 4. P2 —— 中期（半年内）

### 4.1 模型资产管理

**现状**：`models/*.onnx` 不入库（合理，二进制大文件）。但 README 只写"自行放置"——**模型版本与软件版本没有绑定关系**，产线升级软件时模型对不对全靠人肉。

**做法**：
1. 仓库内维护 `models/manifest.json`：模型名 + sha256 + 版本 + 用途 + 训练数据日期（A01 示例可立即补上，README 里已有 yolov11s-pose.onnx 的 SHA1 先例）；
2. 模型文件放共享存储（内网文件服务器/OSS/私有 NuGet 源），CI 发布时校验 sha256 并随包生成清单；
3. 启动时按 manifest 校验模型哈希，不一致则拒绝加载对应配方（或告警）——防止现场拷错模型。

### 4.2 测试补强

**现状**：4047 行单测覆盖良好，但**协议层是单元测试**（TcpServerManagerTests 目录未见，若有则以模拟为主），没有"真实 socket 端到端"验证。

**做法**：
1. **TCP 端到端集成测试**：起真实 `TcpServerManager` 于随机端口，用 `TcpClient` 发 `PING/STATUS/TRIGGER/错误命令`，断言协议行与错误码契约（FileCamera + 零畸变档案即可跑通全链路，README 的 2026-08-22 验证记录是现成素材）；
2. 标定算法补 **golden test**：固定输入 → 固定期望输出（浮点断言容差），防止重构悄悄改变标定结果；
3. xUnit 2.9 可平滑评估 v3（非必须）；
4. 排队/超时并发测试已有（VisionServiceQueueTests 237 行），保持。

### 4.3 推理加速评估（节拍紧张时）

**现状**：CPU 推理 122~134ms（yolov11s-pose，1280×853）。README 已留 `ExecutionProvider` 扩展点。

**做法**：若产线节拍需要 <100ms：
1. 优先评估 **OpenVINO**（工控机多为 Intel CPU，无需独显，性价比最高）——包替换 `YoloDotNet.ExecutionProvider.OpenVino`；
2. 其次 **DirectML**（无需 NVIDIA，Windows 全兼容）；
3. 评估时**锁定 CPU 方案为基线**，用同一模型跑 A/B 对比，别凭感觉换。

---

## 5. P3 —— 前瞻（按需）

| 项 | 说明 |
|---|---|
| ADR 决策记录 | 把 README 中已沉淀的关键决策（协议目标数字段变更、并发模型、失败留存语义）形式化为 `docs/adr/`，将来改协议/并发时有据可依 |
| UI 跨平台评估 | WPF 对工控 Windows 场景是正确的长期选择；仅当未来 HMI 站要跑 Linux 才评估 Avalonia（迁移成本高，勿轻动） |
| TCP 加密/认证 | 当前内网明文是工控常态；仅当跨网络部署或安全审计要求时，加 TLS + SharedSecret 握手（注意保持 PLC 侧兼容或协议版本协商） |
| 协议版本协商 | 协议已历一次破坏性变更（目标数字段）。建议应答头加 `VER,1` 或 `?` 命令查询版本，为将来演进留后路 |

---

## 6. 建议执行顺序（8 周冲刺版）

```
第 1 周：git init + 首次提交 + .gitignore 确认 + MinVer 接入
第 2 周：Directory.Packages.props + 依赖统一 + ONNX Runtime 1.29 升级回归
第 3 周：.NET 10 升级 + 三高风险点验证（YoloDotNet/Basler/OpenCvSharp）+ 测试全绿
第 4 周：CI/CD 流水线（build+test+publish win-x64 自包含）
第 5 周：静态分析门禁（.editorconfig + analyzers + warnaserror）
第 6 周：Options pattern + 启动校验
第 7 周：部署脚本（单文件发布 + 服务化 + 版本目录 + 回滚脚本）
第 8 周：TCP 端到端集成测试 + 整体回归 + 产线联调验证
```

**执行原则**：P0 每项都是独立可交付的小里程碑，做完一项就有一项收益；P1/P2 可并行推进；所有重构类改动（3.4 / 4.2）必须跑全套测试 + 一次真实 PLC 联调后再上产线。

---

## 7. 风险与注意

1. **升级窗口**：.NET 10 升级与依赖升级务必在**产线停机窗口**验证，建议先在台架复现 README 的端到端链路（TRIGGER → OK）再放行；
2. **Basler pylon 兼容性**：net40 老程序集 + net10 运行时是本项目最高风险点，优先实测，不行则保持 net8 发布 + 只升依赖（但仍要赶在 EOL 前解决安全补丁问题）；
3. **ONNX Runtime 升级**：YoloDotNet 4.2.0 的依赖约束是硬约束，升级前先查其 csproj 允许的 ORT 版本范围；
4. **别过度工程化**：本系统并发量低（槽位数 4）、协议简单（行协议），Pipelines/TLS/对象池等高级手段当前不必要，列入 P3 观察即可。
