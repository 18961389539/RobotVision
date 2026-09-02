# RobotVision 技术债与路线图（活文档）

> 全仓 0 个 TODO/FIXME，此前技术债靠会话记忆维持（易失）。本文档是**唯一权威登记表**：
> 新发现的技术债一律补在这里，做完的项标记完成 commit，不靠口头/记忆传递。

更新惯例：每完成一项 → 在对应行标注 `✅ <commit>`；新负债 → 追加行（含位置/规模/建议）。

---

## 已完成（2026-09-02 三轮评审修复，均已推送 main）

| Commit | 内容 |
|---|---|
| `f4b4e0b` | 评审 P0×2 + P1-2~6：全局异常兜底(AppDomain+Unobserved)、配方失败禁覆盖、预览位图双缓冲复用、Unloaded 误触发防护、四页脏检查、引用检查 fail-safe、IsFinite 防线 5 处、7 个防线测试 |
| `6f18b60` | 评审 P1-1/P1-7 + P2-6/P2-10：WebView2 泄漏修复、Chat 护栏补洞(set_camera 全量走护栏、clear_inhibit 去硬编码词)、帧计数先存后计、过期注释、5 个护栏测试 |
| `8ad24bb` | 评审 P2-5 + LabelCache：8 处静默 catch 补留痕(LoggerMessage)、MatLabelDrawer 缓存限容 256 |

对应评审报告：`docs/review/2026-09-02-Wpf-CodeReview.md`。

---

## 待办

### P0（优先）

| # | 项 | 状态 |
|---|---|---|
| 1 | **.NET 8 → 10 LTS 升级**。.NET 8 EOL = **2026-11-10**（约 70 天）。同时处理：NuGet 镜像 302、`DOTNET_ROLL_FORWARD=LatestMajor` 测试运行、`net8.0`/`net10.0` 双 TFM 过渡。P2-1/2/3 性能项建议打包进本次升级。 | 未开始 |
| 2 | **ImageViewer God Object**：`Controls/ImageViewer.*.cs` 100 个 partial / 13771 行（占 ImageViewerControl 43%）。按 Controller 边界拆独立类（非新 partial）。用户未选，暂缓。 | 暂缓 |

### P1（安全/健壮性）

| # | 项 | 位置 | 状态 |
|---|---|---|---|
| 3 | **Core 层统一 IsFinite 校验**（纵深防御）。评审建议 Core 层统一后 UI 层可简化；当前 WPF 层已单点拦截（`f4b4e0b`），Core/Infrastructure 仍有 62 处分散 `IsFinite/IsNaN`，未统一入口。 | Core/Infrastructure | 未开始 |
| 4 | **ChatDangerousActionGuard 剩余风险**：`targets.Count == 0 → 放行` 通用规则——manage_recipe 漏填 name 时只要意图词即放行（工具实现层拒绝空 name，风险当前可控）。以及 IntentKeywords 单字词（"改"/"停"）误命中。 | `Hosting/Chat/ChatDangerousActionGuard.cs:49-50` | 未开始 |
| 5 | **13 个环境性测试失败**：HardwareCameraSmoke×4（需 pylon/GigE 真机）、Live/BakeOff/Bench×8（需现场采集数据）、ApplicationPaths×1（TempDir 清理竞态）。CI 需 mock/跳过策略，或标注 `[Trait("RequiresHardware")]` 过滤。 | `tests/RobotVision.Tests` | 未开始 |
| 6 | **ONNX Runtime 版本**：`Intel.ML.OnnxRuntime.OpenVino 1.22.0`（换包源），上游已 1.29+。评估升级收益与 OpenVino EP 兼容性。 | `Directory.Packages.props` | 未开始 |

### P2（性能/工程化，建议配合 .NET 10 升级）

| # | 项 | 规模 | 状态 |
|---|---|---|---|
| 7 | Binding 未指定 Mode | 975/1034 未指定 | 未开始 |
| 8 | `UpdateSourceTrigger=PropertyChanged` 数值框每键重算 | 40 处（例：`CalibrationScaleWorkspace.xaml:126` 单键 4 次通知） | 未开始 |
| 9 | 显式关闭虚拟化 | 5 处（MonitorPage:229-230,298、CommunicationPage:133、RecipeSetupWizardWindow:291,307,324） | 未开始 |
| 10 | `IsBusy = true; try{…} finally{…}` 样板重复 | 13 处 / 10 文件 | 未开始 |
| 11 | 本地化缺失（中文硬编码进 XAML，`.resx` 数量 0） | 721 处 | 未开始 |
| 12 | 硬编码尺寸/颜色（`UseLayoutRounding` 仅 1 处） | 924 Margin / 655 尺寸 / 111 颜色 | 未开始 |
| 13 | 布局嵌套过深 | `CommunicationPage.xaml:238` 最深 18 层 | 未开始 |

### 环境/流程注意事项（勿当 bug）

- **`RobotVision.Wpf.csproj` 有未提交改动**（`appsettings.Development.json` 拷贝规则 +1 行）——并行会话改动，三轮提交均排除；工作区可能随时出现其他会话的未提交文件，提交前先 `git status --porcelain` 核对归属。
- docs/ 下 `DEPLOYMENT.md`、`ERROR-CODES.md`、`PLC-TRIGGER-Protocol.md` 是旧 untracked 文件（`8f9e48a` 起即存在），未卷入提交。
- 并行会话注意：编译瞬态错误（文件锁/移动）先 `git status` + `git log --oneline -3` 确认，勿急着回滚；禁止 `git stash`。
