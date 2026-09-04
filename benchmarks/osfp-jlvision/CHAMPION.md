# OSFP Product 精修冠军说明

日期：2026-09-04。P2 参数仅 Teach/Dev 锁定。P4 第三轮 Go。

## 方法

**JlShapeModel + FindShapeModel**（Goal 默认交付 B），替换 OpenCV Chamfer ShapeMatch。

TRIGGER 热路径：

1. `FindShapeModel`（`FindMinScore=0.40`，`PreferUpright`）
2. 形状命中：`AlignToTeachAngle`（Chamfer 同口径线拟合）+ `AlignToTeachOrigin`（壳体 + HousingOffset）
3. 失败：`JlMeasure` 长边卡尺（平行差 4.0°）
4. 仍失败且 `AllowCoarseFallback=false` → `Usable=false` → **1019**

不上热路径：全量 `JlMetrologyModel.Apply`（P90&gt;1s）；FitRectangle2（Dev 无过门样本）；JlNCC（Goal 规定不单独上线）。

## 锁定参数

| 项 | 值 |
|---|---|
| 模型 | 配方示教 PNG → `JlShapeRefine.CreateModel`（`use_polarity`，全周角） |
| 搜索 | 分割 bbox 外扩 15% `ReduceDomain` |
| 角度窗 | `MaskHousing.AdaptiveRefineRange`（Product 轴比 ~2.18 时约 ±5°） |
| 两支 | 粗角 与 粗角+180° |
| 极性 | **PreferUpright** |
| Find minScore | **0.40** |
| greediness | **0.9** |
| numLevels | **0（auto）** |
| subPixel | `least_squares` |
| 配方 MatchThreshold 0.85 | **不套用** |

实现：`JlShapeDefaults`、`ShapeMatchSegmentRefineRuntime`、`JlGeometryFallback`、`MaskShapeMatch.AlignToTeachAngle` / `AlignToTeachOrigin`。

## Dev（相对 ShapeMatch 基线 3/14）

- 全链路 **13/14**，翻转 0
- 形状支 P2 锁定 9/14、σ 1.61°、P90 17 ms

## 失败样例

| 文件 | 集合 | 说明 |
|------|------|------|
| `155055353` | Dev | 分割异常（area=85） |
| `160926635` | Holdout | 形状未过门，卡尺平行差 4.35°；Chamfer 基线同样失败 |
| `153947060` / `153948870` | Dev | 形状常无匹配；卡尺约 ±5°（与 NCC/计量同量级） |

## P4 第三轮

全 37 张 **35/37（94.6%）**，Holdout 13/14，σ **0.25°**，无向 P90 **0.43°**，相对全精度 Chamfer 中心 P90 **0.64 px**、角差 P50 **0.00°**，精修 P90 **109 ms**。

详见 `HOLDOUT_REPORT.md`。判定 **Go / 可上线**。
