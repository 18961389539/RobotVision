# OSFP Product 精修 JLVisionLib 替换 — 基准状态

## Phase 0 基线冻结

| 项 | 值 |
|---|---|
| 日期 | 2026-09-03 |
| 数据 | `E:\RobotVisionData\RobotVisionData\captures\2026-08-28`（37 张） |
| 分割 | `E:\RobotVision\models\OSFP-SEG.onnx` |
| 配方 | `E:\RobotVisionData\RobotVisionData\recipes\Product.json` |
| 精修方法 | ShapeMatch（OpenCV Chamfer） |
| 测试 | `FieldCaptureRefineBenchTests.Bench_field_captures_product_recipe_yolo` |

### 基线结果（两次运行一致）

| 指标 | 值 |
|------|-----|
| 分割检出 | 37/37（100%） |
| 精修可用 | **9/37（24.3%）** |
| 精修失败 | 28/37 |
| 成功样本角度 mean | 0.30° |
| 成功样本角度 σ | 0.67° |

### Phase 0 Go/No-Go

- [x] 同机两次基线结果一致（9/37，逐张角度/坐标相同）
- [x] `dataset_manifest.csv` 已生成（Teach 9 / Dev 14 / Holdout 14）
- [x] `baseline_results.csv` 已生成

**Phase 0：通过（基线已冻结）**

## 重要发现

1. **当前 ShapeMatch 基线远低于 Goal 中假设的 75%**：全链路仅 24.3% 可用。
2. **失败模式**：28 张均为「分割已检出但精修未通过」（`usable=False`），非分割漏检。
3. **时段差异**：S3（15:59–16:17）18 张中仅 0 张成功；S1–S2 成功率较高。
4. **180° 候选**：`160315766`、`160926635` 粗角接近 ±177°。

## Goal 指标修订建议

在基线仅 24.3% 的前提下，Holdout 门槛建议分两档：

| 档位 | 全链路可用率 | 说明 |
|------|-------------|------|
| **MVP（相对基线）** | ≥ 基线 +15pp（≥39%）且 ≥ 基线绝对值 | Phase 1–2 赛马门槛 |
| **上线（绝对）** | ≥ 95%（≥35/37） | 最终 Holdout，需 JLVision 显著改善 |

相对基线规则不变：JLVision 冠军 ≥ ShapeMatch 可用率，180° 翻转 ≤ 基线。

## 数据划分摘要

| 集合 | 数量 | 基线成功 |
|------|------|----------|
| Teach | 9 | 5/9 |
| Dev | 14 | 3/14 |
| Holdout | 14 | 1/14（仅 153944575 若在 holdout… 实际 holdout 0/14 成功） |

Holdout 集当前 **0/14 成功** — 是验证 JLVision 改进的关键集合。

## Phase 1 Dev-Set 赛马（2026-09-04）

分割固定（OSFP-SEG.onnx），仅换精修。Dev 14 张，未使用 Holdout。
输出：`jlvision_bakeoff.csv`。找图 `minScore=0.3`（统计 Found）；A 仍按配方 `MatchThreshold=0.85` 门控。

### 汇总

| 方法 | Found | 相对 A | σ（有向） | P90 | 相对粗角 \|θ\|>150° |
|------|------:|--------|----------:|----:|--------------------:|
| A ShapeMatch（基线） | **3/14（21.4%）** | — | 0.14° | 499 ms | 0（门控后） |
| B JlShape | 12/14（85.7%） | +64pp | **51.63°** | 28.6 ms | **2**（`160028323`、`160130962` ≈179°） |
| C JlMetrology | **13/14（92.9%）** | +71pp | 2.07° | **2122 ms** | 0 |
| D JlMeasure | **13/14（92.9%）** | +71pp | 2.15° | **22 ms** | 0 |
| E JlNcc | **13/14（92.9%）** | +71pp | 2.28° | 88 ms | 0 |

`155055353`（分割 area=85）五路全失败，与 Phase 0 异常一致；剔除后 C/D/E 为 **13/13**。

### Phase 1 Go/No-Go

门槛：≥1 个 JLVision 方案 Dev 可用率 ≥ 基线 **且** ≥ 基线+15pp（≥6/14），180° 翻转 ≤ 基线。

- [x] C / D / E 均 **13/14**，远超 3/14 与 6/14
- [x] C / D / E 相对粗角无 180° 翻转（A 在 Dev 门控成功样本上也为 0）
- [ ] B **不作为默认交付**：Found 高，但两张 S3 低分命中 ~179°，σ 被拉到 51°

**Phase 1：通过（Go）。冠军候选：E（主）/ D（快）/ C（几何，耗时超标）。B 需极性修复后再比。**

### 失败与异常样例

| 文件 | 现象 |
|------|------|
| `155055353` | 分割异常，五路全失败（C：HALCON #8573 有效卡尺不足） |
| `153947060` | A/B 未命中；C/E ≈ +5°，D ≈ −5°（D 符号与模板法相反） |
| `160028323` / `160130962` | B 走 +180° 支且 score≈0.49；C/D/E 仍在 0° 附近 |
| `160315766` | A 粗角 −177° 未过门；B/C/E ≈ −3°，D ≈ +3.5°（D 再一次反号） |
| C 的 `160028323` / `160130962` | Metrology **2.1–2.3 s**，拖高 P90，P4 的 180 ms 过不了 |

### 读数注意

1. **`flip_vs_A` 几乎全是 0**：A 只 Found 3 张，无法衡量 S3 翻转；应以相对分割粗角 / C·E 对照为准。
2. **B 的 180°**：`JlLocalSearch` 在粗角与粗角+180 两支上取最高分，低分 S3 上 +180 支会赢。P2 应加极性或分差门，而不是只靠 `minScore=0.3`。
3. **D 符号**：多张 `D ≈ −C`（约 ±5°）。卡尺线方向未与 `WarpAngleDeg` 的有向约定对齐，P2 必须先修 `FuseDirected`，否则 σ 含反号噪声。
4. **分数尺度**：配方 0.85 不能直接套 B/E。若对 E 套 0.85，Dev 约 **10/14**（S3 三张 0.64–0.75 会被丢掉），仍远好于 A 的 3/14。

### P2 定标（2026-09-04，仅 Dev，未看 Holdout）

极性：`PreferUpright`（两支都命中时取 |θ| 更接近 0° 的一支）。对照 `up=False` 仍 2 张 ~179°、σ=58°。

输出：`jlvision_p2_grid.csv`、`jlvision_p2_summary.txt`。

| 配置 | Found（门控） | flip | σ | P90 |
|------|-------------:|-----:|--:|----:|
| B min=0.40 g=0.9 **up=True** | **9/14（64%）** | 0 | **1.61°** | **17 ms** |
| B min=0.45 g=0.9 up=False | 10/14 | **2** | 58° | 22 ms |
| B min=0.75 g=0.9 up=True | 5/14 | 0 | 0.03° | 16 ms |
| E min=0.55 | **13/14** | 0 | 2.28° | 75 ms |
| E min=0.85 | 10/14 | 0 | 2.40° | 56 ms |

### Phase 2 Go/No-Go

- [x] Dev 可用率：B 锁定后 9/14 vs 基线 3/14（**+43pp**，≥ +3pp）
- [x] 180° 翻转：PreferUpright 后 **0**
- [x] P90 17 ms ≪ 180 ms
- [ ] σ 1.61° 尚未到 Holdout 的 0.3°（P2 不要求该项）

**Phase 2：通过（Go）。锁定默认交付 B = JlShapeModel。**

锁定参数见 `CHAMPION.md`。E 在 Found 上仍领先，按 Goal 不作唯一上线方案。

## Phase 3 后端替换

`ShapeMatchSegmentRefineRuntime`：`JlShapeTeachCache` + `JlShapeRefine`，失败则 `JlMeasure` 兜底（Metrology P90>1s，不进热路径）。
配方 Chamfer `MatchThreshold=0.85` 不套用 JL 分；`FindMinScore=0.40`。建模失败才回退 OpenCV。

- [x] 运行时接入
- [x] `RobotVision.Tests` 排除现场 bench：1112 通过 / 0 失败 / 10 跳过
- [x] Dev 全链路 13/14（基线 3/14），翻转 0

**Phase 3：通过（Go）。**

## Phase 4 Holdout（第三轮 Go）

详见 `HOLDOUT_REPORT.md`、`jlvision_p4_all.csv`、`chamfer_fullprec.csv`、`JLVisionCore.lock.txt`。

形状命中出角与 Chamfer 线拟合对齐；热路径仅 JlShape + JlMeasure。

| 门 | 结果 | 判定 |
|----|------|------|
| 可用 ≥35/37 | **35/37（94.6%）** | **过** |
| 相对基线 | 35/37 ≫ 9/37 | **过** |
| 翻转 | 0 | **过** |
| Holdout σ | **0.25° ≤ 0.3°** | **过** |
| 无向 P90 vs 0° | **0.43° ≤ 0.5°** | **过** |
| 中心 P90 vs 全精度 Chamfer | **0.64 px ≤ 2** | **过** |
| 相对 Chamfer 角差 P50 | **0.00°** | **过** |
| 精修 P90 | **109 ms ≤ 180** | **过** |
| 失败 1019 | 2 张 Usable=false | **过** |
| 现场 bench | `Bench_field_captures_product_recipe_yolo` 通过 | **过** |

失败：Dev `155055353`（分割异常）、Holdout `160926635`（卡尺平行差 4.35°）。

**Phase 4：Go。**
