# RobotVision.Wpf 表现层代码评审

- 日期：2026-09-02
- 范围：`src/RobotVision.Wpf`（115 个 .cs / 16 933 行 / 37 个 XAML，net8.0-windows）
- 方式：四路并行静态审查 + 关键结论逐一实证复核（含 WPF-UI 3.1.1 框架源码核对、NuGet 包元数据提取）

## 结论先行

工程质量**明显高于工控上位机同类平均水平**：ViewModel 层零 Service Locator、零 `async void`、零 UI 线程死锁、跨对象事件订阅 6/6 全部退订、code-behind 无业务逻辑。这些是多数 WPF 项目做不到的。

缺陷集中在两处**真实的生产事故面**：异步路径异常完全静默（无兜底），以及配方读取失败后保存会覆盖磁盘真实文件。其余多为 P1/P2 的边界脆弱与性能损耗。

| 等级 | 数量 | 说明 |
|---|---|---|
| P0 | 2 | 会导致数据丢失或静默失效，建议本迭代修 |
| P1 | 7 | 长时间运行下的确定性劣化或安全网失效 |
| P2 | 11 | 可维护性 / 性能 / 一致性 |

---

## P0

### P0-1 异步命令与后台任务异常完全静默

全仓（src 全目录 grep）**仅** `App.xaml.cs:72` 注册 `DispatcherUnhandledException`；`AppDomain.UnhandledException` 与 `TaskScheduler.UnobservedTaskException` **零注册**。

Wpf 工程共 137 个 `[RelayCommand]`，其中 22 个是 `async Task`（生成 `AsyncRelayCommand`）。AsyncRelayCommand 的 `Execute` 丢弃返回 Task，异常沉入 `ExecutionTask`；无人 await 且无 `UnobservedTaskException` 处理器 → 异常从产生到消失全程无任何痕迹。

对比：同步 `void` 命令反而安全（抛到 Dispatcher 被 App 兜住并弹窗）。即**加了 async 反而更危险**。

危害：产线上异步命令（保存配方、切换模型、下发参数）失败后界面停在旧值，无提示无日志，操作员会重复操作或误判。

修复（成本极低）：

```csharp
// App.OnStartup
AppDomain.CurrentDomain.UnhandledException += (_, e) => /* 记日志 */;
TaskScheduler.UnobservedTaskException += (_, e) => { /* 记日志 */ e.SetObserved(); };
```

### P0-2 配方读取失败 → 空白编辑器 → 保存即覆盖磁盘真实配方

`Features/Recipe/RecipeListCatalog.cs:352-372`：

```csharp
_host.IsNew = false;                       // ← 先置为「已存在」
try { var loaded = _loader.Get(name); _host.Editor = loaded.Clone(); ... }
catch (Exception ex)
{
    _host.Editor = new RecipeConfig { Name = name };   // ← 空白配方
    _host.Baseline = _host.Editor.Clone();
    _host.ResetDirtyCache();                            // ← 脏标记被清空
    _host.Message = $"读取失败：{ex.Message}";
}
```

此时保存走 `:254 _loader.Save(Editor, previousName)`，因 `IsNew == false` 且名字不变 → **用空白配方覆盖磁盘上的真实文件**。触发条件：文件被占用、JSON 损坏、权限不足、磁盘满。

修复：读取失败时置 `_host.Editor = null` 并禁用保存命令，或将 `IsNew` 保持为 `true`（走另存语义，不覆盖原名）。

---

## P1

### P1-1 WebView2 永不释放（确定性非托管泄漏）

`Shared/HtmlPreviewService.cs:22-35`：每次预览 `new WebView2()` 塞进新 Window，`window.Show()`，无 `Closed` 处理、无 `view.Dispose()`。每个 WebView2 带一个浏览器进程与非托管内存，关闭窗口后不释放。Chat 助手可反复触发。

修复：`window.Closed += (_, _) => { view.Dispose(); window.Content = null; };`

### P1-2 预览路径每帧全量分配（高分辨率相机下等同 P0）

`Shared/ImageConverter.cs:38,42,52`：

```csharp
bgra = new Mat(); Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);  // 5MP ≈ 20 MB
var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
```

`CamerasViewModel.cs:344` 预览定时器 150 ms（≈6.7 fps）。5MP 下分配速率 ≈ 134 MB/s。
（说明：`WriteableBitmap` 的后备缓冲是**非托管内存**，不进 LOH；代价在于非托管内存由终结器异步回收，长跑下内存曲线呈锯齿且峰值偏高，并持续产生 GC 压力。OpenCV 侧 Mat 有内存池，可复用。）

修复：尺寸不变时复用同一个 `WriteableBitmap`，只调 `WritePixels`。

### P1-3 整条资源释放链挂在 `Page.Unloaded` 单点

`Shared/ViewModelPageLifetime.cs:17-27` 是 14 个页面唯一的 Dispose 触发点。

已排除的场景：曾怀疑 WPF-UI 缓存页面实例导致「切回页面 VM 已死」。核对框架源码 `NavigationView.Navigation.cs` 的 `GetNavigationItemInstance`：**当 `_pageService` 非空时完全绕过 `_cache`**，直接 `_pageService.GetPage()`。本项目注入了 `DiPageService`，页面注册为 Transient → 每次导航都是全新 Page + 全新 VM，切页时 Unloaded → Dispose 属正确时机。

**剩余的真实风险**：WPF 的 `Unloaded` 并不只在元素移除时触发，资源字典/模板重建同样会触发。`AppThemeManager.Apply` 会增删 `Application.Current.Resources.MergedDictionaries`，而主题切换是运行时可用功能（`SettingsViewModel.cs:210 partial void OnUiThemeChanged(string value) => AppThemeManager.Apply(value);`）。一旦误触发，当前页 VM 被 Dispose 且不会重建（如 `MonitorPage.xaml.cs:24-30` 的 Loaded 只重建日志订阅，不重建 `_vision.FrameProcessed`）→ 结果图永久停止更新，**无异常、无日志**。

修复：改为 `Loaded`/`Unloaded` 对称的 Attach/Detach，并让 Loaded 侧幂等重建订阅；或改由导航事件驱动生命周期。

### P1-4 脏检查覆盖不全，四个可编辑页无保护

| 页面 | 脏检查 | 页面 | 脏检查 |
|---|---|---|---|
| Cameras | 有 | Calibration | **无** |
| Recipe | 有 | CalibrationWizard | **无** |
| Settings | 有 | Models | **无** |
| — | — | Lightings | **无** |

标定向导是**多步采集流程**，切页即丢弃整套标定数据，代价最高。

### P1-5 引用检查失败按「未引用」处理

`Shared/RecipeReferenceCheck.cs:17-18`：`catch { return false; }` — 配方读取失败一律判定为「未被引用」，删除相机/光源前的安全网在异常路径下失效。应改为失败即拒绝删除（更安全的一侧）。

### P1-6 WPF 层零数值有限性校验

全工程 `double.IsFinite` / `IsNaN` 出现 **0 次**（全仓 62 次集中在 Core/Infrastructure）。范围校验形如 `is < 0 or > 1`，而 **NaN 与任何数比较均为 false**，可穿透全部校验。`Math.Clamp` 对 NaN 同样无效（`ModelsViewModel.cs:600-602` 把它当净化器用）。

一个 NaN 角度进入机器人引导坐标是物理级风险。缓解因素：System.Text.Json 序列化 NaN 会抛异常，多数落盘路径最终报错——属偶然保护，非设计。

### P1-7 `set_camera` 不带 action 时写配置零护栏（跨层，Hosting 工程）

`RobotVision.Hosting/Chat/ChatDangerousActionGuard.cs:96` 仅拦截 `action == "unregister"`。样例 `{"camera_id":"CAM1","exposure_us":2000,"gain":20}` 无 `confirm`、不过护栏，直接改曝光并落盘下发。

同源问题：`:51-53` 判定为 `targets.Any(...)`（OR 匹配）+ 子串 `Contains` + 大小写不敏感；`:111-113` `clear_inhibit` 的 targets 硬编码 `"1018"/"联锁"/"解除"`，用户只需说「确认解除」即通过，无需点名配方。

> 此条位于 Hosting 层，超出本次 Wpf 评审范围，作为跨层风险记录。

---

## P2

| # | 问题 | 位置 | 数据 |
|---|---|---|---|
| 1 | Binding 未指定 Mode | 全工程 | 975 / 1034 未指定（仅 52 OneWay + 7 TwoWay） |
| 2 | `UpdateSourceTrigger=PropertyChanged` 用于数值框，每键触发重算 | `CalibrationScaleWorkspace.xaml:126` → `CalibrationViewModel.cs:291` 单键 4 次通知 | 40 处 |
| 3 | 显式关闭虚拟化 | `MonitorPage.xaml:298`、`:229-230`、`CommunicationPage.xaml:133`、`RecipeSetupWizardWindow.xaml:291,307,324` | 5 处 |
| 4 | `IsBusy = true; try{…} finally{…}` 样板重复 | Analysis / CalibrationWizard×2 / Cameras×2 / Chat / Monitor / Recipe×3 | 13 处 / 10 文件 |
| 5 | 静默 catch（无日志无通知） | `ModelsViewModel.cs:574,604`、`RecipeViewModel.cs:872`、`RecipeTestSession.cs:201`、`FailuresViewModel.cs:278,320`、`MonitorPage.xaml.cs:77`、`CalibrationWizardViewModel.Grab.cs:134` | 8 处 |
| 6 | 采集帧计数先增后存，保存失败则计数虚高 | `CalibrationWizardViewModel.Grab.cs:120-123` | 影响内参计算 |
| 7 | 本地化缺失，中文硬编码进 XAML | 全工程，`.resx` 数量 0 | 721 处 |
| 8 | 硬编码尺寸/颜色 | 924 处 `Margin=`、655 处 `Width/Height/FontSize=`、111 处 `#RRGGBB`；`UseLayoutRounding` 仅 1 处 | — |
| 9 | 布局嵌套过深 | `CommunicationPage.xaml:238` | 最深 18 层 |
| 10 | 注释与注册生命周期不符 | `CamerasViewModel.Preview.cs:99-100` 称「进程级单例」，实际 `AddTransient<CamerasViewModel>()` | 注释过期 |
| 11 | 旧 CTS 未 Dispose | `FailuresViewModel.cs:237-238`（托管级，未访问 WaitHandle，不泄漏句柄） | 1 处 |

附带：`MatLabelDrawer.cs:19` 静态 `LabelCache` 无上限（当前键空间碰巧有界 ≤101，若标签嵌入配方名/时间戳即失控）；`RecipeSetupWizardViewModel` 等 6 个文件的 `ILogger` 注入计数为 0，异常仅写 `Message` 无日志留痕。

---

## 被推翻 / 被修正的判断

评审中逐条复核，以下三条与初步扫描的结论不同，记录以免后续重复排查：

1. **~~页面被 WPF-UI 缓存导致切回后 VM 已死~~** — 不成立。框架源码证实 `_pageService` 非空时绕过 `_cache`，每次导航取新实例。降级为 P1-3，机制改写为「主题切换引发 Unloaded 误触发」。
2. **~~每帧 WriteableBitmap 进入 LOH~~** — 不准确。后备缓冲是非托管内存，不进 LOH；真实代价是非托管内存的终结器延迟回收 + GC 压力。等级维持 P1。
3. **~~AnalysisPlots 是自绘图表、数据点会无限增长~~** — 不成立。基于 OxyPlot，且有上限（`TableLimit=500`、`Limit=10_000`）。

## 未发现问题的项（正面清单）

以下均为实测确认，可作为后续重构时「不要动」的基线：

- **ViewModel 层 Service Locator 反模式计数 = 0**（`GetRequiredService` 仅出现在组合根 `App.xaml.cs` 与 `DiPageService`）
- **`async void` 方法 0 处**（仅在注释中出现）
- **UI 线程 `.Result`/`.Wait()` 死锁 0 处**（`App.xaml.cs:154-158` 用 `Task.Run` + 超时预算，写法正确）
- **裸 fire-and-forget `Task.Run` 0 处**（全部经 `UiFireAndForget`）
- **跨对象（单例/相机/静态事件）未退订的订阅 0 处**（6 处全部退订，`MonitorViewModel.cs:182-183→544-545`、`CommunicationViewModel.cs:142-145→160-163`）
- **未 Dispose 的 Mat / CameraFrame / VisionImage 0 处**；`VisionImageCv.cs:41` 用 `Mat.FromPixelData`（非所有权 header），故 `ImageConverter` 的 `using var mat` 不会误释放调用方缓冲，无 double-free
- **属性名硬编码 0 处**（`[ObservableProperty]` 306 处、`OnPropertyChanged(nameof(...))` 294 处）
- **相同值仍触发通知 0 处**（CommunityToolkit `SetProperty` 有相等短路）
- **无上限集合 0 处**（日志 500 / 请求 2000 / 表格 20000 / 图表 10000 / 帧队列 500）
- **后台循环无重入闸门 0 处**（两处预览循环均有 `Interlocked` / bool 闸门）
- **code-behind 1139 行 / 27 文件**，多数 8–15 行，统一 Attach + Loaded 骨架，无业务逻辑
- **IDisposable 实现 20 处全部采用简化模式**（无 `Dispose(bool)` + 终结器），正确——这些类型不直接持有非托管句柄
- 工程化细节到位：单实例 Mutex（Global→Local 回退）、关停超时预算 `ApplicationShutdownCoordinator`、`--snapshot` 离屏渲染 CLI 模式

## 建议修复顺序

1. P0-1（两行代码，收益最高）
2. P0-2（数据丢失）
3. P1-1、P1-4（各一处，成本低）
4. P1-6（NaN 防线，建议在 Core 层统一 `IsFinite` 校验后再落到 UI）
5. P1-2、P1-3、P1-5、P1-7（需设计，排入下个迭代）
6. P2 第 1/2/3 项（性能，配合 `.NET 8 → 10` 升级一起做）
