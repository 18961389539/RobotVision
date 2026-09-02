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
| `177ae7d` | 全仓复查 P0-1 + P1-1 + P1-4：git rm 两个 `_wpftmp.csproj`（WPF 临时工程文件，泄本机路径）+ .gitignore 防回归；Skip 机制修复（xunit 2.9.2+runner 2.8.2 不支持 `SkipException.ForSkip` → 启用 SkippableFact，22 处 `[Fact]→[SkippableFact]`，13 环境性失败→11 跳过）；CA2213 泄漏 ×4（`_refreshCts` + 两个 `_pageSession` Dispose + CS8604）；顺带修 2 个真实测试 bug（SmoothRectangle 4 角点轮廓不满足 band≥8 契约、ApplicationPaths 对未创建目录 Delete） |
| `7c50a0b` | 全仓复查 P1-5：4 处空 catch 补留痕（AtomicFile/ProcessHealthStore/CameraManager/LightingManager） |
| `1aeab71` | 复查报告 `docs/review/2026-09-02-Repo-Audit.md` 补修复闭环 |
| `eb7fdfd` | 重审新发现：ImageAnalysisService CWT 缓存竞态 → GetValue 原子工厂；CA2213 误报 ×2 标注（Wpf 首次 0 警告）。重审报告 `docs/review/2026-09-02-ReAudit.md` |
| `d789230` | **拆分纯视觉算法为独立类库 RobotVision.Vision**：9 算法文件（模板匹配/卡尺/SIFT/形状匹配/壳体/缓存）从 Infrastructure 拆出，仅依赖 Core+OpenCvSharp4，零硬件与推理会话依赖；引用链 Hosting/Teach/tests/benchmarks 显式引用，零行为回归 |
| `6533809` | **配方 NoFlipConstraint 开关**（产线需求）：目标永不翻转 180° 时跳过 180° 分支搜索与翻转重试——缓存/计算省一半、杜绝近对称件误判 180°；Core/Vision/Runtime/UI 四层 + 4 测试 |
| `0af54c1` | **三项工业强化**：①失败归因诊断（LastDebug 写 BestScore/MinScore/歧义比，Runtime 拼可读 QualityNote→试触发 UI，1019 可归因）；②第二峰歧义门 `MaxSecondPeakRatio`（抽 FindSecondPeak 复用，挡周期纹理/近对称误匹配）；③角度窗 UI 提示（Core 抽 RefineRangeHintText 纯函数 + NumberBox/CheckBox Dispatcher 刷新）。+4 测试 |
| `8594047` | ShapeMatch 方向一致性诊断（DirAgree 进 DebugInfo/QualityNote，可区分"距离 vs 方向"失败归因） |
| `d32b6ad` | **UprightCrop 裁剪窗显式长/短边**：修复 MinAreaRect 表示歧义（Width 可能为短边）导致的转正窗错向（-33° 目标被裁）；warp 保持原口径；参考实现同步 |
| `87c921d` | **ShapeMatch 接入 NoFlipConstraint**：跳过翻转窗判决（±20° 内极性不可靠误走 180° 支）；UprightCrop 无向 warp 已处理整体 180° |
| `f2b8f6c` | **翻转判决极性证据门**：两支极性(polar0/polar180)须分离 ≥0.5×|PolarTeach| 才可判 180° 相位，否则信任主窗——默认(NoFlip=false)配置下 8.7°/20° 误翻转 174° 事故修复(err 1.33°/5.65°)；FollowsRotation(37/180) 正向对照仍绿；+2 回归 |
| `a3969f3` | **精度基线回归** `PrecisionBaselineTests`(+10)：固化实测可达精度——TPL 角度 ≤0.2/0.6/0.6°(0°/1.3°/3.2° 残差)、TPL XY ≤±0.8px、SHP 恒等 ≤1.0°(rng3)、SHP 平移 ≤0.75px(基线校正)；合成基线非规格书，真机标定后收紧 |

对应评审报告：`docs/review/2026-09-02-Wpf-CodeReview.md`、`docs/review/2026-09-02-Repo-Audit.md`。

---

## 待办

### P0（优先）

| # | 项 | 状态 |
|---|---|---|
| 1 | **.NET 8 → 10 LTS 升级**。✅ **已完成**（本日）：20 个 csproj TFM 全部切 net10.0/net10.0-windows，Microsoft.Extensions 全家 + Sqlite 升 10.0.11，CI 工作流 4 处 dotnet-version 升 10.0.x，`DOTNET_ROLL_FORWARD=LatestMajor` 不再需要（本机已有 10.0.11 运行时）。全解决方案 0 错误；RobotVision.Tests 936 通过 + 11 跳过（环境性）+ 0 失败；Wpf.Tests 244/244；ImageViewerControl.Tests 56/56；IntegrationTests 43 通过 + 4 跳过（UI 自动化）。P2 性能项仍可打包后续迭代。 | 全仓 | ✅ 已完成 |
| 2 | **ImageViewer God Object**：`Controls/ImageViewer.*.cs` 100 个 partial / 13771 行（占 ImageViewerControl 43%）。按 Controller 边界拆独立类（非新 partial）。用户未选，暂缓。 | 暂缓 |

### P1（安全/健壮性）

| # | 项 | 位置 | 状态 |
|---|---|---|---|
| 3 | **Core 层统一 IsFinite 校验**。~~62 处分散~~ → **2026-09-02 实证复核：关闭**——实测仅 39 处，且分布于几何/校准/推理各计算点的中间值校验（AngleGeometry/PolynomialCalibrator/MaskCaliperTab 等），每处上下文不同属正常防御，强制统一入口反损可读性。与既往 grep 误报同型（数字≠真问题）。 | Core/Infrastructure | ✅ 关闭 |
| 4 | **ChatDangerousActionGuard 剩余风险**：~~IntentKeywords 单字词（"改"/"停"）误命中~~ → **已修复**（`4398062`，移除单字词，保留双字"修改/停止/停用"等；新增闲聊不绕过回归测试）。`targets.Count==0 → 放行` 通用规则经核实：所有危险工具（manage_recipe/set_camera/clear_inhibit/manage_calibration）工具层均有空参校验，放行后会被工具层拒绝——风险已闭环，维持现状。 | `Hosting/Chat/ChatDangerousActionGuard.cs:49-50` | ✅ `4398062` |
| 5 | ~~13 个环境性测试失败~~ → **已解决**（`177ae7d`）：Skip 机制修复后 Hardware/Live/BakeOff/Bench 11 项转跳过；SmoothRectangle 与 ApplicationPaths 2 个真实 bug 已修。全量 936 通过 + 11 跳过 + 0 失败。 | `tests/RobotVision.Tests` | ✅ `177ae7d` |
| 6 | **ONNX Runtime（OpenVINO EP）升级评估**（2026-09-02 完成）→ **版本线澄清**：`Intel.ML.OnnxRuntime.OpenVino` 1.22.0 已是 OpenVINO EP 专用线，镜像最高 1.24.1（=OpenVINO 2025.4.1）；记忆"落后到 1.29"混淆了 `Microsoft.ML.OnnxRuntime.Managed`（1.29.0，本项目未用）。**兼容性**：lock `[1.22.0,)` 开区间无冲突；`YoloDotNetEngineFactory.cs:126-134` 的 `OpenVino{Precision=FP16/FP32}` 属 ORT 1.23 起弃用 options（precision→load_config），EP 1.1.0 内部翻译，升级后 precision 语义可能变。**建议**：随 .NET 10 升级一并评估（需真机 OpenVINO 精度回归），不急单独升。 | `Directory.Packages.props` | 已评估，待 .NET 10 |
| 7 | **推理策略静态 `[ThreadStatic] LastDebug`（降级自 P1-3）**：4 个策略（MaskCaliperTab/MaskShapeMatch/MaskSiftRefine/MaskTemplateMatcher）各有一份，**实证非运行时 bug**——所有读取方与策略调用同一同步调用栈、入口 `default` 重置、ThreadStatic 天然线程隔离。真实问题是隐式状态传递 + `PickAutoLayout` 依赖副作用选布局，属可维护性债。建议后续改为显式返回 DebugInfo（非本迭代）。 | `Inference/Strategies/*.cs`（9 文件 36 处） | 降级 P2，未开始 |
| 8 | **BaslerCamera 锁内长阻塞**：`_grabLock` 内 `GrabOne(_grabTimeoutMs 默认 60000ms)` + 连接期 `Thread.Sleep(1000)`，UI 预览/标定/管线取图共用同一相机锁。WPF 已包 `Task.Run` 不卡 UI，但长超时互相排队拖吞吐。需真实相机验证，排 .NET 10 窗口。 | `Infrastructure/Cameras/BaslerCamera.cs` | 未开始 |
| 9 | **ShapeMatch 对标 HALCON 迭代**（`5e2ecb0` v1 有向 Chamfer 已落地）：
    - v1 已做：梯度方向通道（16 bin 折叠 180°）、方向一致性代价（失配只扣 hit）、弱梯度豁免、67.5° 容差
    - 待迭代：①UprightCrop 转正缺陷——**两味表示歧义(2026-09-02 二次实证)**：a) MinAreaRect Width 可能是短边(已由 `d32b6ad` 裁剪窗长/短边修复)；b) **长轴 180° 补角表示翻转内容**(未修)：实测(220×56 条)负向角 φ=-37/-20/-8.7 时 MinAreaRect 返回 W≈59(短) A=53~81，`Width<Height→Angle+90` 得 warp=143~172 → 转正后内容翻转 179.8°(φ≥0 则正常,内容≈0°)——**代码只在 φ≥0 自洽,负向角全翻**。**修复设计(已验证)**:warp 规范化到 [-90,90):`Canon(w)=((w+90)%180)-90` 于 `MaskHousing.Fit` 单点(-37→-36.9、-20→-19.9、-8.7→-8.5,内容全角度 ±0.3° 一致)。**迁移检查单**(跨消费者,勿只改 Fit):CropMapping 参考 `FullImageUprightCrop` 同步;`MaskCaliperTab` θ0/探针方向语义、`FeatureRoiAdvisor` ROI 方向语义(今晨改 Fit 曾破其测试)、ShapeMatch 翻转窗(extraWarp 保持原算术、勿二次规范化)、UprightCrop 小轮廓回退分支。**已落地**：`d32b6ad` 裁剪窗长/短边、`87c921d` ShapeMatch NoFlip、`f2b8f6c` 翻转极性证据门(8.7°/20° 误翻转修复,-20° 属本味残留)。②方向图抗插值漂移(金字塔粗到细)；③正交干扰定量测试(需真机)；④32 bin | `Vision/MaskShapeMatch.cs`+`MaskTemplateMatcher.cs`+`Teach/FeatureRoiAdvisor.cs` | 迭代中 |

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
| 14 | **CalibrationWizardViewModel.cs「每行空一行」怪异格式**（454 空行/769 行，提交时即存在，git HEAD 同款）——疑似历史格式化工具误写，建议一次格式化收敛 | `Wpf/Features/CalibrationWizard/CalibrationWizardViewModel.cs` | 未开始 |

### 环境/流程注意事项（勿当 bug）

- **`RobotVision.Wpf.csproj` 有未提交改动**（`appsettings.Development.json` 拷贝规则 +1 行）——并行会话改动，三轮提交均排除；工作区可能随时出现其他会话的未提交文件，提交前先 `git status --porcelain` 核对归属。
- docs/ 下 `DEPLOYMENT.md`、`ERROR-CODES.md`、`PLC-TRIGGER-Protocol.md` 是旧 untracked 文件（`8f9e48a` 起即存在），未卷入提交。
- 并行会话注意：编译瞬态错误（文件锁/移动）先 `git status` + `git log --oneline -3` 确认，勿急着回滚；禁止 `git stash`。
