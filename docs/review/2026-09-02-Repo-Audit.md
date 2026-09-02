# RobotVision 全仓合理性复查报告

- 日期：2026-09-02
- 范围：src 全部 6 工程 + 关键测试（编译实测 + 测试实测 + 四文件深挖）
- 方式：模式扫描（空 catch / async void / 硬编码路径 / .Result / 静态可变 / 依赖违规 / git 卫生）+ 超大文件深度审查 + `dotnet build` + `dotnet test` 实证

## 结论先行

**整体工程质量处于工控上位机平均线以上**：分层依赖严格（Core 零框架引用、Infrastructure 零上层引用）、0 处 `async void`、UI 线程取图已包 `Task.Run`、VisionService 编排已拆出调度器/指标、ImageViewer 已从 100 个 partial 拆为 38 partial + 91 独立类、编译 0 错误、非环境性测试 933/946 通过。

不合理之处集中在 **4 个真实问题面**：仓库卫生（wpftmp 临时文件入库 + 4.1GB bin）、测试环境性失败被误计（Skip 机制失效）、相机锁内长阻塞、推理静态全局状态。另有大量 P2 魔法阈值与超长方法。

| 等级 | 数量 | 说明 |
|---|---|---|
| P0 | 1 | 环境耦合的临时文件入库（泄本机路径） |
| P1 | 5 | 锁内长阻塞 / Skip 机制失效 / 静态全局 / 资源泄漏 ×4 / 潜在 null |
| P2 | 7 | 魔法阈值 / 超长方法 / 性能 / 仓库体积 |

---

## P0

### P0-1 `_wpftmp.csproj` 临时工程文件被 git 跟踪（环境耦合 + 泄本机路径）

两个 WPF XAML 编译器生成的临时工程文件被提交（引入于 8f9e48a）：

- `src/ImageViewerControl/ImageViewerControl_ewfnr2sp_wpftmp.csproj`
- `src/RobotVision.Wpf/RobotVision.Wpf_x5smx2ov_wpftmp.csproj`

内容为一次性构建产物：`MSBuildProjectExtensionsPath=E:\光模块\RobotVision\...`、数百条 `C:\Users\Administrator\.nuget\...`、`C:\Program Files\dotnet\packs\...` **本机绝对路径引用**。问题：

1. **环境耦合**：换机器/换用户名/换目录即失效，属构建垃圾。
2. **泄路径**：把已清理的 `E:\光模块` 硬编码路径和他人用户目录带回仓库。
3. 影响面小（不参与编译），但属于「不应该存在的文件」——**git rm + .gitignore 加 `*_wpftmp.csproj`** 即可。

---

## P1

### P1-1 测试 SkipException 机制失效，13 个环境性失败被计为「失败」（实测）

`TestSkip.Throw` 用 xUnit 2.9.2 的 `SkipException.ForSkip` 抛 `$XunitDynamicSkip$`，但 `xunit.runner.visualstudio 2.8.2` 把它计为 **FAIL** 而非 SKIP（实测 `RobotVision.Tests 933/946,失败 13`）：

- HardwareCameraSmokeTests ×4：需 `RV_HARDWARE_TEST=1` + 真实相机
- FieldCaptureRefineBenchTests ×3 / MaskTemplateLiveOsdpTests ×3 / SegmentRefineBakeOffTests ×1 / ProductLiveAdvisorTests ×1：依赖 `data/captures/2026-08-28` 现场图与 OSDP 数据
- ApplicationPathsTests ×1：TempDir 清理竞态（DirectoryNotFoundException，测试自身问题）

影响：CI 或全量跑永远红 13 个，**把真实回归信号淹没**。9/1 记忆「全量 934/934 清零」与此不矛盾——彼时口径不同或已按环境过滤。建议：升级 `xunit.runner.visualstudio` 至 ≥2.9，或改用 `Xunit.SkippableFact`（已在 CPM 中声明 1.4.13 却未用）。

### P1-2 BaslerCamera 锁内最长 60s 阻塞（`_grabLock` 内 GrabOne）

`BaslerCamera.cs:123-141`：`lock (_grabLock)` 内 `GrabOne(_grabTimeoutMs)`（默认 60000ms 超时），且 `ConnectCore` 内含网络发现 + `Thread.Sleep(1000)`（:230）。同一把锁还串行化曝光/增益操作（:669-721）。

影响：UI 预览 / 标定取图 / 产线管线共用同一相机锁——任一请求长超时，**其余全部排队等锁**。WPF 调用已包 `Task.Run`（不卡 UI 线程），但管线吞吐会被拖到秒级。建议：取图移出锁（用 pylon 并发策略或门闩+超时收窄）、连接/发现不与取图争锁。

### P1-3 推理层静态全局状态，多线程互相污染

- `MaskTemplateMatcher.cs:239/150/560`：静态 `LastDebug` + 跨调用魔法阈值（NeedsUprightAlign 0.08、PickOrientation 0.12）
- `MaskCaliperTab.cs:105` / `MaskShapeMatch.cs:63` / `MaskSiftRefine.cs:61`：`[ThreadStatic] LastDebug` 隐式全局，`PickAutoLayout` 依赖其副作用选布局

影响：多线程推理（不同模型并行时）调试状态互相串扰；阈值藏于调用间无名字，调参只能靠猜。建议：LastDebug 改为按调用返回或 `AsyncLocal`/参数传递；阈值集中为命名常量表。

### P1-4 CA2213 资源泄漏警告 ×4（另有 1 处潜在 null）

Wpf 强制重建后 16 个警告，其中真实泄漏：

| 位置 | 字段 | 说明 |
|---|---|---|
| CamerasViewModel.cs:213 | `_previewSessionCamera` (ICamera?) | 预览会话相机未释放 |
| CamerasViewModel.cs:184 | `_refreshCts` | 刷新取消令牌未 Dispose |
| CalibrationWizardViewModel.cs:175 | `_pageSession` (PageAsyncSession) | 页面会话未释放 |
| RecipeSetupWizardViewModel.cs:47 | `_pageSession` (PageAsyncSession) | 页面会话未释放 |

`App.xaml.cs:38` 的 `_host` 系**误报**（`ApplicationShutdownCoordinator.cs:78` 已调 `host.Dispose`，有超时保护）。
另：`RecipeTestSession.cs:117` CS8604 可能传 null 给 `LightControllerId`。

### P1-5 空 catch 虽少但含生命周期竞态（4 处）

`AtomicFile.cs:53`、`ProcessHealthStore.cs:163`（IOException）、`CameraManager.cs:227`、`LightingManager.cs:194`（ObjectDisposedException）——并发 Dispose 竞态静默吞掉，无日志。建议统一补 Trace/Debug 留痕（成本低，符合 8ad24bb 已建立的 P2-5 模式）。

---

## P2

### P2-1 魔法阈值泛滥（无命名常量，调参靠 grep）

- `MaskCaliperTab.cs`：20+ 个（4.0 / 20 / 0.55 / 0.22 / 0.82 / 0.40…）
- `MaskTemplateMatcher.cs:385-409`：Canny 链 60/160/15/0.65/0.003/0.22/1.35
- `ImageAnalysisService.cs`：置信度权重 `/64.0`、`/5.0`，卡尺数量 Clamp(3,31)/Clamp(6,180)
- `BaslerCamera.cs:612-614`：`D:\Program Files\Basler` 等 4 个安装路径硬编码（回退链可接受，但建议环境变量优先）

### P2-2 超长方法与上帝类

- `MaskCaliperTab.RefineGrayCore` ~188 行（布探针/采样/滤波/拟合/头尾判定一体）
- `ImageAnalysisService.cs` 1243 行静态类兼直方图/剖面/三类卡尺/统计/几何五职责
- `RoiInteractionService.cs` 1191 行，22 个嵌套行为类，RotatedRect/Blob 行为整段重复（206-278 vs 908-984）

### P2-3 ImageAnalysisService 并发/契约问题

- :33 静态 `ConditionalWeakTable`「先查后 Add」，并发首帧同 key 竞态抛 ArgumentException → 用 `GetValue`
- :154-157 `Try*` 方法内抛 `ArgumentOutOfRangeException`，违背 Try 契约
- :213-216 `List.Contains` 判 rejected 为 O(n²)

### P2-4 RoiInteractionService 强转风险

`RoiInteractionService.cs:722`：多个类型共用 `LineMeasureBehavior`，HitTest 强转 `(LineMeasureRoi)`——若其一非该派生类即 InvalidCastException。需确认继承关系或分专用行为。

### P2-5 仓库体积 14GB（bin 4.1GB）

- `src/RobotVision.Wpf/bin` 3.3GB（Release 1.9G + publish 923M + win-x64 342M + models 51M）
- Hosting/Infrastructure/Teach bin 合计 ~800MB
- 建议：`dotnet clean` + 定期清 bin（CI 产物不入本地缓存）、publish 目录不进日常开发

### P2-6 Basler 靠异常消息文本判 underrun

`BaslerCamera.cs:497-517`：`"incompletely grabbed"` / `"(code="` 文本匹配判断 underrun，pylon 版本升级易碎。建议用异常类型/返回码。

### P2-7 其他零星

- `appsettings.Development.json` 含 `E:\llm\...`、`E:\Qwen3.8-27B-GGUF\...` 本机路径（开发配置可接受，建议环境变量覆盖）
- `RoiInteractionService` / `ImageAnalysisService` 滤波与梯度扫描重复代码块 ×2 组可抽公共方法
- sln 未收录 5 个 tools 工程（CaptureBasler / FixXamlEncoding / HardwareCameraVerify / RobotVision.Diag / ViewerControlCompare）——建议登记或补录

---

## 正面基线（后续重构"不要动"的项）

- **分层依赖严格**：Core 零框架引用、零上层引用；Infrastructure 零 Hosting/Wpf 引用（实测 grep 空）
- **VisionService 已拆**：PipelineScheduler / VisionMetrics 独立，编排职责单一（590 行）
- **ImageViewer 已拆**：100 partial → 38 partial + 91 独立类（Controller/Host/Assembler/Composition 模式）
- **0 处 async void**（仅注释提及）、UI 线程取图均包 `Task.Run`（Calibration/Cameras/Recipe 实测）
- **git 卫生**：0 个 obj/bin 被跟踪；packages.lock.json ×17 全锁定；CPM 已启用
- **Chat 护栏已补洞**（6f18b60）：set_camera 全 action 走护栏、clear_inhibit 需点名对象
- **9/2 评审 P0/P1 全部修复**：全局异常兜底、配方覆盖防护、WebView2 释放、脏检查补 4 页、IsFinite 防线
- **空 catch 仅 4 处**（此前 ~180 处）；**硬编码绝对路径 0 处**（代码层实测）

## 修复闭环（2026-09-02 本报告同日，3 项低成本修复已落地）

- **P0-1 ✅**：`git rm` 两个 `_wpftmp.csproj` + `.gitignore` 加 `*_wpftmp.csproj`。
- **P1-1 ✅**：根因是 xunit 2.9.2 + runner.visualstudio 2.8.2 组合不支持 `SkipException.ForSkip` 的 dynamic skip（runner v2 最新即 2.8.2，无法升级；SkippableFact 包依赖 xunit.core 2.4.0 亦不可用）。修法：启用 CPM 已声明未用的 `Xunit.SkippableFact`，`TestSkip.Throw` 改抛 `Xunit.Skip.If(true, reason)`（SkippableFact 特性捕获），7 个测试文件 22 处 `[Fact]`→`[SkippableFact]`。**13 个环境性失败全部转为"已跳过"**。
- **P1-4 ✅**：`CamerasViewModel.Dispose` 补 `_refreshCts?.Dispose()`；CalibrationWizard/RecipeSetupWizard 的 `_pageSession.Deactivate()`→`Dispose()`（幂等）；`RecipeTestSession:117` 补 `?? ""`（LightControllerId 可空→record 非空参数）。Wpf 编译警告 16→4（剩余 2 个 CA2213 系已证实的误报：App._host 由 ShutdownCoordinator 释放、_previewSessionCamera 由 StopPreview 链路释放）。
- **顺带修复 2 个真实测试问题**：
  - `SegmentRefineBakeOffTests.SmoothRectangle_LineFitWinsWhenCaliperHasNoTab`：8f9e48a 把"winner null 即 return"改严为 `Assert.NotNull`，但测试用手写 4 角点轮廓，而 `RefineByLineFitBands` 要求 band ≥8 点→**真实回归**。改用 FindContours 生成真实轮廓修复。
  - `ApplicationPathsTests.NormalizeAppConfig_EmptyDataRoot_UsesDefaultDataRoot`：测试对"从未创建"的临时目录执行 `Directory.Delete`（此前误判为 TempDir 竞态）。移除无效 Delete + 加 `[Collection("Serial")]` 串行化。
- **验证**：RobotVision.Tests **935 通过 + 11 跳过 + 0 失败**（修复前 933/13）；全仓编译 0 错误。

## 遗留待办（本次未动）

- P1-2 BaslerCamera 锁拆分、P1-3 推理静态全局、P1-5 空 catch 留痕
- P2 魔法阈值 / 超长方法 / 仓库体积 / CalibrationWizardViewModel.cs 的「每行空一行」怪异格式（提交时即存在，建议格式化）
- 上述建议打包进 .NET 8→10 升级窗口

## 建议优先级

1. P0-1 git rm wpftmp + .gitignore（十分钟，收益最高）
2. P1-1 修 Skip 机制（升级 runner 或启用 SkippableFact）——让 13 个环境性失败从「红」变「黄」，恢复回归信号
3. P1-4 补 4 处 CA2213 Dispose（各一行）
4. P1-2 Basler 锁拆分（需设计，排下迭代）
5. P1-3 / P1-5 推理静态状态与空 catch 留痕（低成本）
6. P2 项建议打包进 .NET 8→10 升级窗口（与 roadmap 既有条目一致）
