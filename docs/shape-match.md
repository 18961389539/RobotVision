# 形状匹配（MaskShapeMatch）

分割转正窗内的有向 Chamfer 精修，对标 HALCON `find_shape_model` 的**局部精修**阶段（非整图搜索）。

## 快速使用

```csharp
using RobotVision.Vision;

// 示教：转正裁剪 + 源图分割轮廓（写入模型原点相对轮廓质心，运行时刚体搬运）
var crop = MaskTemplateMatcher.UprightCrop(teachRoi, teachContour, MaskShapeMatch.CropMarginRatio);
var model = MaskShapeMatch.BuildTeach(crop, teachContour);
// 仅有配方 PNG 时：BuildTeach(uprightPng)，原点回退为转正窗壳体+偏移
// 或运行时缓存
var model2 = MaskShapeMatch.GetOrCreate(recipe);

// 精修（分割轮廓 + 原图 ROI）
var opt = ShapeMatchOptions.From(recipe.Template);
var attempt = MaskShapeMatch.TryRefine(roi, contour, model, refineRangeDeg: 8,
    noFlip: recipe.Template.NoFlipConstraint, options: opt);
if (attempt.Pose is { } pose)
{
    // pose.AngleDeg, pose.Center, pose.Score, pose.HitRate, pose.MeanDistPx
}
// 诊断
var dbg = MaskShapeMatch.LastDebug; // hit/mean/residual/DirAgree
```

## 关键参数（`ShapeMatchOptions` / 配方 `TemplateOptions`）

| HALCON | 本库 | 说明 |
|--------|------|------|
| `NumLevels` | `ShapeMatchNumLevels` / `NumLevels` | 搜索金字塔层数 1–3（默认 2：½+全） |
| `AngleStart` / `AngleExtent` | `AngleStartDeg` / `AngleExtentDeg` | 转正窗残差角搜索范围 |
| `AngleStep` | `AngleStepDeg` | 粗搜角步长（默认 1°） |
| `Metric` | `ShapeMatchMetric` | `UsePolarity` / `IgnoreLocalPolarity` / `IgnoreGlobalPolarity` |
| `MinContrast` | `ShapeMatchMinContrast` / `MinContrast` | 0=自适应 Canny；>0 抬高边缘阈值抗噪声 |
| — | `MinHitRate` / `MaxMeanDistPx` | Chamfer 命中门 / 均距门 |
| — | `RefineRangeDeg`（配方） | 精修半窗（±度），与 `AngleExtent` 联动 |

## 可视化

`TryRefine` 返回 `Attempt.Viz`：
- `Inliers` / `Rejected`：边点（原图像素）
- `DistHistogram`：8 桶 Chamfer 距离分布（0–7+ px），对标得分分布
- `PyramidLevels`：实际使用的金字塔层数
- `SearchDebug`（`EmitSearchDebug=true`）：粗/细网格评估次数与最优代价

`EnableVisualization=false` 时仍返回位姿，可减少内点列表分配。运行时 `QualityNote` 含命中/均距/残差角/方向一致；叠加层绘制内点/拒点。

## 位姿中心

`BuildTeach(crop, sourceContour)` 把示教 Canny 原点相对分割轮廓**多边形质心**记入模型。运行时输出：

`质心(现场轮廓) + R(报告角) × 示教偏移`

质心是轮廓点的仿射不变量，不依赖 MinAreaRect 壳体中心（壳体中心不是物点，旋转后可偏 0.3–0.4 px）。真值按示教原点绕绘制中心刚体旋转，合成矩阵 P90 **&lt;0.1 px**。

仅 `BuildTeach(png)` 时回退为转正窗壳体 + `HousingOffset`，再经 WarpAffine dest→src（`MapCropToSource`，与 `ContourInUpright` 互逆）映回源图。

角度：贴边强时优先线拟合（与 MinAreaRect 差 &lt;1.5°）；无向线拟合落在补角则用 warp，不叠加 Chamfer 0.25° 网格残差。|warp|≥150° 时 `FuseDirected`。

## 确定性

搜索网格与亚像素细化步长固定；同输入同参数结果可重复。  
大 warp 下 NCC 角种子仅在转正窗残差 ≤2.5° 时收窄 Chamfer 旋转窗，避免 180° 假峰锁死。贴边强时不以 NCC 角替换 Chamfer/warp。

## 轻微尺度

现场转正窗小于示教模板时（零件收缩），NCC 会因模板放不进搜索图而跳过全部角。匹配前将现场窗升采样到示教尺寸（与大 warp 规范化同一套 `canonMapped`），Chamfer 在统一像素格上精修；轻微放大则保留原生分辨率并用各向同性 `ChamferScale`。

分割轮廓按 WarpAffine 逆变换画入距离场，避免转正插值 Canny 与示教边点系统性错位。

## 运行日志

`MaskShapeMatch.FormatQualityNote` / `LastDebug` 写入 `QualityNote`（命中、均距、残差角、方向一致 DirAgree）。

## 精度与性能（合成矩阵，2026-09-03）

刚体夹具：0° 主图 WarpAffine 旋转/平移/尺度；分割轮廓为 0° 轮廓点的同一仿射（不再每角整数栅格化）。

| 指标 | 产品规格 | 当前合成基线 | 门槛常量 |
|------|----------|--------------|----------|
| 角度 P90 | **≤0.3°** | 0.24°（8 角矩阵；最大 37° 为 0.24°） | `AngleP90Deg` |
| 中心 P90 | **<0.1 px** | 相对旋转后示教原点 **0.008 px**（独立于壳体） | `SpecCenterPx` |
| 单次精修 P90 | **<180 ms** | ~16 ms（-20°）；37° ~50 ms；3% 收缩 ~22 ms | `SpecLatencyMs` |
| 矩阵成功率 | **>0.992** | 100%（8 角 + 光照/噪声/遮挡/±3% 尺度/3% 剪切，28/28） | `SpecSuccessRate` |
| 轻微尺度角 | **≤0.3°** | `0.97@-20°` 0.13°；`0.97@37°` 0.18° | `SpecAngleDeg` |
| Chamfer 贴边 | 命中≥0.55、均距≤2.5 px | 旋转场景命中约 0.83–0.87、均距 1.1–2.0 px | `OverlayMinHitRate` / `OverlayMaxMeanDistPx` |

## SIMD

`MaskShapeMatchScoreSimd`：**AVX2** 旋转投影（4 点/批）+ 扁平距离场双线性累加（无向/粗细搜快路径）。  
平移 0.1 px 网格 + 抛物线插值（`RefineCenterFine`）。大角度有向精修仅在亚像素细化（`|warp|≥25°` 且粗代价偏高）时惰性构建梯度幅值图。
