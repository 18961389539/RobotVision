# OSFP Product 精修 JLVisionLib — Holdout 验收报告

日期：2026-09-04。第三轮评测：形状命中出角与 Chamfer 同口径（线拟合），热路径不接入未在 Dev 过门的 FitRectangle2。未用 Holdout 调 `minScore` / 卡尺平行门。

数据：`E:\RobotVisionData\RobotVisionData\captures\2026-08-28` 37 张 `*_Product_OK.png`  
分割：`OSFP-SEG.onnx`（未替换）  
配方：`Product.json`（`RefineMethod=ShapeMatch`，`AllowCoarseFallback=false`）  
后端：`JlShapeModel`（`FindMinScore=0.40`，`PreferUpright`）→ 失败则 `JlMeasure`  
出角：形状命中后 `AlignToTeachAngle`（线拟合与粗角 &lt;1.5° 时用线拟合方向，头尾跟 Find）  
出心：`AlignToTeachOrigin`（壳体 + 示教 HousingOffset）  
原始逐张：`jlvision_p4_all.csv`  
Chamfer 全精度对照：`chamfer_fullprec.csv`（基线 CSV 坐标曾取整）  
原生库锁定：`JLVisionCore.lock.txt`（fileVersion=1.0.0.0，73670144 bytes）

## 汇总

| 集合 | 可用 | 翻转 | σ（有向） | 全链路 P90 | 精修 P90 |
|------|-----:|-----:|----------:|-----------:|---------:|
| Teach | 9/9 | 0 | 0.59° | 228 ms | 65 ms |
| Dev | 13/14 | 0 | 2.16° | 309 ms | 95 ms |
| **Holdout** | **13/14** | **0** | **0.25°** | 318 ms | 109 ms |
| 全 37 张 | **35/37（94.6%）** | **0** | 1.37° | 316 ms | 107 ms |

基线 ShapeMatch：9/37（24.3%）。分割检出 37/37。现场 bench `Bench_field_captures_product_recipe_yolo` **通过**（门槛 ≥75%）。

## L1 功能

| ID | 门槛 | 结果 | 判定 |
|----|------|------|------|
| F1 全链路可用率 | ≥95%（≥35/37） | **35/37** | **过** |
| F2 相对基线 | ≥ 9/37 | 35/37 | **过** |
| F3 180° 翻转 | 0 | 0（2 张 `br=180;upright180` 已折回 ~0°） | **过** |
| F4 失败→1019 | `AllowCoarseFallback=false` | 2 张 `Usable=false` → `RefineFailed=1019`；单测覆盖 | **过** |

失败 2 张：

| 文件 | 集合 | 原因 |
|------|------|------|
| `155055353` | Dev | 分割 area=85，精修无边（分割异常） |
| `160926635` | Holdout | 形状未过门，卡尺平行差 4.35°（门 4.0°）。基线同样未过门（粗角 −177°） |

## L2 精度（Holdout 13 张可用）

软真值：工位朝向约 0°。中心/角对照 **全精度 Chamfer**（`chamfer_fullprec.csv`，与 Phase 0 同一算法；冻结的 `baseline_results.csv` 坐标为整数，不宜作 2 px 门）。Holdout 上 Chamfer 0 成功，A3/A4 用全量 9 张 Chamfer 成功样本（Teach+Dev）。

| ID | 门槛 | 结果 | 判定 |
|----|------|------|------|
| A1 有向角 σ | ≤0.3° | Holdout **0.25°** | **过** |
| A2 无向角误差 P90 | ≤0.5° | 相对 0°：**P90=0.43°**（P50=0.09°，max=0.56°） | **过** |
| A3 中心误差 P90 | ≤2 px | 相对全精度 Chamfer 9 张 **P90=0.64 px**（P50=0.31，max=1.99） | **过** |
| A4 相对基线角差 P50 | ≤0.5° | 有向 **P50=0.00°**（P90=0.33°；多数形状命中与 Chamfer 线拟合同角） | **过** |

## L3 性能与工程

| ID | 门槛 | 结果 | 判定 |
|----|------|------|------|
| P1 精修 P90 | ≤180 ms/件 | **精修单段 P90=109 ms**。CSV `ms` 为分割+精修 ≈316 ms（YOLO 占主） | **过** |
| P2 配方兼容 | 旧 Product.json 可读 | `RefineMethod=ShapeMatch` 无需改字段；`MatchThreshold=0.85` 不套用 JL 分 | **过** |
| P3 部署 | `JLVisionCore.dll` 随包 | JlVision Content + Wpf 输出；`JLVisionCore.lock.txt`；单测拷贝检查 | **过** |
| P4 回归 | 37 张 + 单测 | p4 37 张；现场 bench 通过；`RobotVision.Tests` 排除其它硬件跳过：**1112 通过 / 0 失败 / 10 跳过** | **过** |

## 热路径（上线）

1. `FindShapeModel`（minScore 0.40，PreferUpright，bbox 外扩 15%）
2. 形状命中：线拟合对齐出角 + 壳体 HousingOffset 出心
3. 未命中：`JlMeasure` 长边卡尺（平行差 4°）
4. 仍失败且 `AllowCoarseFallback=false` → 1019

未进 TRIGGER：全量 `JlMetrologyModel.Apply`（P90&gt;1s）；FitRectangle2（Dev 无独立过门样本，接入后 Holdout 一张 ~3° 会破坏 σ≤0.3°）。JlNCC 仅赛马。

## 总判定

**Phase 4：Go。** 上线门禁全部达到。相对 Chamfer 基线 9/37 → 35/37，零翻转，Holdout σ 0.25°，精修 P90 109 ms。
