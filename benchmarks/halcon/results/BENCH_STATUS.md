# HALCON 对标状态

### 无 HALCON 已覆盖的 RV 质量（shape_match）

| 项目 | 路径 |
|------|------|
| 合成夹具 | `fixtures/shape_match/*.png` |
| RV 基线 | `results/shape_match_robotvision_results.csv` |
| HALCON 输出 | `results/shape_match_halcon_results.csv`（可选） |
| 矩阵门槛 | `ShapeMatchBenchGates`（P90≤0.35°） |
| 引擎 parity | `Bench_shape_match_halcon_side_by_side_engine_parity`（有 HALCON CSV 时） |

---

## 本机无 HALCON 时

当前仓库 **可以不安装 HALCON** 完成日常开发与回归：

- `Bench_halcon_gap_report`、`Bench_halcon_robotvision_baseline`、`Bench_halcon_export_fixtures` 在 CI / `dotnet test` 中 **应通过**
- `Bench_halcon_side_by_side_engine_parity` **预期 Skip**（直到存在 `halcon_results.csv`）

这不是失败，而是「尚未提供 HALCON 参照输出」的占位跳过。

### 无 HALCON 已覆盖的 RV 质量

| 项目 | 路径 | 说明 |
|------|------|------|
| 合成夹具 | `fixtures/*.png` + `contour.csv` | 有限旋转矩形 `Rectangle`（四边可测） |
| RV 基线 | `results/robotvision_results.csv` | `Bench_halcon_robotvision_baseline`（日常 clip=2） |
| RV 基线（HALCON profile） | `results/robotvision_results_halcon_clip0.csv` | clip=0 轮廓；`Bench_halcon_side_by_side_engine_parity` 对比用 |
| 真值差距（日常 clip=2） | `results/truth_gaps.csv` | RV vs 几何真值；P90 门槛见下表 |
| 真值差距（HALCON clip=0 profile） | `results/truth_gaps_halcon_clip0.csv` | `ClippingEndPoints=0` 轮廓 + 同全链路；`Bench_halcon_gap_report` 自动生成 |
| HALCON 输出 | `results/halcon_results.csv` | **可选**；有 HDevelop 时生成，格式见 `halcon_results.csv.example` |

### 产品规格（合成真值）

全链路（轮廓 + 亚像素）相对几何真值：

| 指标 | 规格 | 常量 |
|------|------|------|
| 角度 | ≤ **0.5°** | `SpecAngleDeg` |
| 中心位置 | ≤ **0.1 px** | `SpecCenterPx` |

轮廓级只需满足角 0.5°；0.1 px 位置以全链路为准（亚像素卡尺）。

### truth_gaps P90 门槛（full 链路，RV vs 几何真值）

| 指标 | 门槛 | 常量 |
|------|------|------|
| 角差 | ≤ 0.5° | `TruthFullAngleP90` / `SpecAngleDeg` |
| 中心 | ≤ 0.1 px | `TruthFullCenterP90` / `SpecCenterPx` |
| 长边 | < 0.05 px | `TruthFullLongP90` |
| 短边 | < 0.02 px | `TruthFullShortP90` |
| 归一化 RMS | < 0.001 × 短边 | `TruthNormRmsP90` |
| 轮廓长边（standard P90） | < 0.06 px | `TruthContourLongP90` |
| 轮廓短边（standard P90） | < 0.08 px | `TruthContourShortP90` |
| 轮廓 HALCON clip=0 轮廓角 P90（standard） | ≤ 0.5° | `TruthContourAngleP90HalconClip` |
| 轮廓 HALCON profile P90（clip=0，standard） | L&lt;0.06 S&lt;0.08 px | `TruthContourLongP90HalconClip` / `TruthContourShortP90HalconClip` |
| 轮廓 HALCON clip=0 standard 长边 max / 135° | &lt; 0.06 px | `TruthContourLongMaxHalconClip` / `TruthContourLong135HalconClip` |
| 轮廓 HALCON clip=0 高 jitter / 缺边 | L&lt;0.08 / L&lt;0.08 px；jitter ΔS&lt;0.08 | `TruthContourLongNoiseHalconClip` / `TruthContourLongPartialHalconClip` / `TruthContourShortNoiseHalconClip` |
| 轮廓 HALCON clip=0 `standard_-18` 长边 | &lt; 0.10 px | `TruthContourLongMinus18HalconClip` |
| full HALCON profile P90（clip=0，全夹具） | 同 full 链路表 | `AssertHalconClip0FullGates` |

### truth_gaps_halcon_clip0 实测 P90（HALCON engine profile）

| 阶段 | θ | 中心 | 长边 | 短边 | norm RMS |
|------|---|------|------|------|----------|
| contour（standard 6 角） | **0.096°** | — | **0.047** | **0.070** | — |
| full（含 blur/noise/partial） | **0.014°** | **0.01 px** | **0.015 px** | **0.007 px** | **0.000** |

退化场景 clip=0 **轮廓**：`noise_j1.5` ΔL≈0.002 ΔS≈0.055、`noise_j2.5` ΔL≈0.04、`partial_edge` ΔL≈0.05。**full** 链路亚像素仍与 clip=2 同量级。

门槛定义：`tests/RobotVision.Tests/RotatedRectHalconBenchGates.cs`  
断言入口：`Bench_halcon_gap_report`

### contour 阶段（HALCON clip=0 profile，jitter=0.6）

`standard_*` 六角度轮廓长边已压至 **max 0.047 px**（P90≈0.044，见 `truth_gaps_halcon_clip0.csv`）。**full 链路**亚像素进一步压至 P90 与 clip=2 同量级。

| 夹具 | contour ΔL (px) | full ΔL (px) |
|------|-----------------|--------------|
| standard_0 | clip=0 L≈**0.02** S≈**0.07** | ~0.03 |
| standard_-18 | clip=0 L≈**0.03** S≈**0.01** | ~0.01 |
| standard_22 | clip=0 L≈**0.007** | ~0.002 |
| standard_45 | clip=0 L≈**0.006** | ~0.012 |
| standard_135 | clip=0 L≈**0.04** | ~0.013 |
| standard_88 | clip=0 L≈**0.05** S≈**0.03** | ~0.026 |

半长精修后以有符号四边残差做 ±0.4° 一维角搜索（不把短边直线角直接融进长轴）。轮廓角 P90 约 **0.10°**。质量分含归一化 RMS（相对短边）。

轮廓中心在 `standard_0` 上约 0.13 px，由 full 亚像素收至 **≤0.1 px**（规格）。

---

## 有 HALCON 时：补全 engine parity

在已安装 HDevelop 的机器上（设置 `HALCONROOT` 或默认安装路径）：

```powershell
cd E:\RobotVision
.\benchmarks\halcon\run_halcon_bench.ps1 -RunHalcon
```

或手动在 HDevelop 中运行 `bench_rectangle2.hdev`（`FixtureDir` 指向本仓库 `benchmarks/halcon/fixtures`）。

成功后：

1. 生成 `results/halcon_results.csv`
2. **建议提交该 CSV**，使无 HALCON 的协作者/CI 也能跑 `Bench_halcon_side_by_side_engine_parity`
3. 该测试将不再 Skip

### 引擎 parity 门槛（HALCON vs RV）

对比文件：`halcon_results.csv` vs `robotvision_results_halcon_clip0.csv`（clip=0 轮廓 profile，与 `bench_rectangle2.hdev` 一致）。日常 `robotvision_results.csv`（clip=2）仅用于真值回归，不参与引擎 diff。

| 指标 | 门槛 |
|------|------|
| 角差 | ≤ 0.5° |
| 中心 | ≤ 0.1 px |
| 长边 | < 0.5 px |
| 短边 | < 0.3 px |

常量：`EngineAngleGapDeg` / `EngineCenterGapPx` / `EngineLongGapPx` / `EngineShortGapPx`

---

## 本地快速刷新 RV 基线（无需 HALCON）

```powershell
cd E:\RobotVision
.\benchmarks\halcon\run_halcon_bench.ps1
```

等价于运行 export + baseline + gap_report 三个测试。
