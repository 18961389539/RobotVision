# HALCON rectangle2 对标基准

与 RobotVision `fit_rectangle2` 全链路（`fit_rectangle2_contour_xld` + `measure_pairs`）做数值 side-by-side。

**产品规格（合成真值，全链路）：角度 ≤ 0.5°，中心位置 ≤ 0.1 px。**

## 没有 HALCON 环境时（常见）

**未安装 HALCON / HDevelop 是正常情况**，日常开发与 CI 不依赖它。

| 现象 | 是否正常 |
|------|----------|
| `Bench_halcon_side_by_side_engine_parity` 显示 **Skipped** | 是（缺少 `results/halcon_results.csv`） |
| `Bench_halcon_gap_report` / `Bench_halcon_robotvision_baseline` **通过** | 是（RV 相对合成几何真值回归） |
| 全量测试 1036+ 通过、仅少数 Bench Skip | 是 |

**无 HALCON 时仍能验证：**

- `truth_gaps.csv` — RV 日常 profile（clip=2 轮廓）相对合成真值
- `truth_gaps_halcon_clip0.csv` — HALCON engine profile（clip=0 轮廓 + measure_pairs 全链路）
- `robotvision_results.csv` — RV 轮廓级 + 全链路基线（日常 clip=2）
- `robotvision_results_halcon_clip0.csv` — HALCON engine profile（clip=0 轮廓）RV 基线，供 `halcon_results.csv` side-by-side
- 现场 `FieldCaptureRefineBenchTests`（需 `RobotVisionData`）

**需要 HALCON 才能验证：** RV 与 HALCON 引擎逐行数值 diff（`halcon_contour` / `halcon_full` vs `rv_*`，对比 `robotvision_results_halcon_clip0.csv`）。

### 一次性补全（在有 HALCON 的机器上）

```powershell
cd E:\RobotVision
.\benchmarks\halcon\run_halcon_bench.ps1 -RunHalcon
```

将生成的 `benchmarks/halcon/results/halcon_results.csv` **提交进仓库** 后，任意无 HALCON 的机器跑 side-by-side 测试也会执行（不再 Skip）。格式见 `results/halcon_results.csv.example`；存在 CSV 时 `Bench_halcon_results_csv_schema_when_present` 会校验表头与合成夹具覆盖。

详见 `results/BENCH_STATUS.md`。

**CI：** `.github/workflows/halcon-rectangle2-bench.yml` 在每次 PR/push 跑 truth_gaps 与已提交 `robotvision_results.csv` 同步校验（无需 HALCON）。

---

## 目录

```
benchmarks/halcon/
  README.md                 ← 本文件
  bench_rectangle2.hdev     ← HDevelop 脚本（在 HALCON 中打开运行）
  fixtures/                 ← 合成夹具（PNG + contour.csv + manifest.json）
  results/
    robotvision_results.csv ← C# 自动生成（日常 clip=2）
    robotvision_results_halcon_clip0.csv ← HALCON engine profile（clip=0）
    halcon_results.csv      ← HALCON 脚本输出（需本机运行）
```

## 1. 生成 / 更新夹具（C#）

```powershell
cd E:\RobotVision
.\benchmarks\halcon\run_halcon_bench.ps1
```

或手动：

```powershell
dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj -c Release `
  --filter "FullyQualifiedName~Bench_halcon_export_fixtures|FullyQualifiedName~Bench_halcon_robotvision_baseline|FullyQualifiedName~Bench_halcon_gap_report"
```

输出：
- `results/robotvision_results.csv` — RV 引擎基线
- `results/truth_gaps.csv` — RV 相对合成真值的角/中心/尺寸/归一化 RMS 差距（回归追踪）

或指定输出根目录：

```powershell
$env:HALCON_BENCH_DIR = "D:\halcon_bench"
dotnet test ... --filter "FullyQualifiedName~Bench_halcon"
```

## 2. 运行 HALCON 脚本

```powershell
.\benchmarks\halcon\run_halcon_bench.ps1 -RunHalcon
```

或手动在 **HDevelop** 中加载 `bench_rectangle2.hdev`，改 `FixtureDir` / `ResultPath` 后运行。

脚本流程：

| 阶段 | HALCON 算子 | 对标 RV |
|------|-------------|---------|
| `halcon_contour` | `fit_rectangle2_contour_xld`（Tukey） | `RotatedRectFitter` |
| `halcon_full` | 轮廓拟合 + `add_metrology_object_rectangle2_measure` + `apply_metrology_model` | `RotatedRectPipeline.Fit` |

### 轮廓拟合参数对齐

`bench_rectangle2.hdev` 调用：

```text
fit_rectangle2_contour_xld (..., 'tukey', -1, 0, 0, 3, 2, ...)
```

即 **ClippingEndPoints=0**、Iterations=3、ClippingFactor=2。日常 RV 回归夹具默认 `ClipEndPoints=2`（更抗 jitter）；引擎 side-by-side 前应使用同一 profile：

| 用途 | RV 选项 | 测试 |
|------|---------|------|
| 日常合成回归 | `ClipEndPoints=2` | `Bench_halcon_gap_report` |
| HALCON 引擎 profile | `ClipEndPoints=0` | `Bench_halcon_contour_halcon_clip0_profile_gates` |

`fixtures/manifest.json` 导出 `rv_clip_end_points` 与 `halcon_clip_end_points` 供脚本核对。

## 3. 引擎 side-by-side 门槛测试

**前置条件：** `results/halcon_results.csv` 存在（见上文「没有 HALCON 环境时」）。

在 `halcon_results.csv` 存在时：

```powershell
dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj -c Release `
  --filter "FullyQualifiedName~Bench_halcon_side_by_side"
```

门槛（`RotatedRectHalconBenchGates`）：

- 角差 &lt; 0.15°
- 中心差 &lt; 0.5 px
- 长边差 &lt; 0.5 px
- 短边差 &lt; 0.3 px

## CSV 列

```
id,scenario,true_deg,engine,ok,angle_deg,center_x,center_y,long_len,short_len,rms_px,quality
```

`engine` 取值：`rv_contour` / `rv_full` / `halcon_contour` / `halcon_full`。

## 现场图扩展

现场 `*_Product_OK.png` 导出（需 `RobotVisionData` 或 `FIELD_CAPTURE_DIR`）：

```powershell
dotnet test ... --filter "FullyQualifiedName~Bench_halcon_export_field"
```

输出：`fixtures/field/*.png` + `contour.csv` + `manifest.json`（种子角来自剪影 `MaskHousing`）。

HALCON 脚本可对 `fixtures/field/` 单独循环；无合成真值，仅做引擎间 diff。
