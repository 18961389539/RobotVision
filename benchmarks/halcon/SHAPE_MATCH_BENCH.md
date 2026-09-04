# HALCON shape-based 对标（规划中）

对标 `create_shape_model` / `find_shape_model` 的**分割后局部精修**精度（RobotVision `MaskShapeMatch`），非整图搜索。

## 无 HALCON 时

```powershell
dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj -c Release `
  --filter "FullyQualifiedName~Bench_shape_match"
```

`ShapeMatchBenchReportTests` 在合成不对称件上跑角度矩阵（-37°…180°），门槛见 `ShapeMatchBenchGates`（角度 P90≤0.3°，中心相对**旋转后示教原点** &lt;0.1px，耗时 P90&lt;180ms，成功率&gt;0.992）。鲁棒矩阵含光照、噪声、遮挡、±3% 尺度与 3% 剪切。

夹具：0° 主图一次栅格化，旋转/平移/尺度用 WarpAffine；轮廓点做同一刚体变换，避免每角整数顶点抖动。中心真值为示教 Canny 原点绕绘制中心旋转（独立于壳体 MinAreaRect）。

示教与现场均使用 `MaskShapeMatch.CropMarginRatio`（0.15）；导出夹具：

```powershell
dotnet test tests\RobotVision.Tests\RobotVision.Tests.csproj `
  --filter "FullyQualifiedName~Export_halcon_shape_match"
```

## 有 HALCON 时

```powershell
cd E:\RobotVision
.\benchmarks\halcon\run_halcon_bench.ps1 -ShapeMatch          # 导出夹具 + RV 基线
.\benchmarks\halcon\run_halcon_bench.ps1 -ShapeMatch -RunHalcon  # + HDevelop + engine parity
```

1. 夹具：`fixtures/shape_match/`（`teach_0.png`、`live_{deg}.png`）
2. 运行 `bench_shape_match.hdev` → `results/shape_match_halcon_results.csv`
3. `Bench_shape_match_halcon_side_by_side_engine_parity` 对比 RV 基线 `shape_match_robotvision_results.csv`

无 HALCON 时 parity 测试 **Skip**（与 rectangle2 相同）。提交 `shape_match_halcon_results.csv` 后 CI 可跑 parity。

参考：`RotatedRectHalconSideBySideTests`、`shape_match_halcon_results.csv.example`。
