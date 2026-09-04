using RobotVision.Tests.HalconBench;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>HALCON shape_model 与 RobotVision MaskShapeMatch 转正窗内精修 side-by-side。</summary>
[Trait("Category", "Bench")]
public sealed class ShapeMatchHalconSideBySideTests(ITestOutputHelper output)
{
    [Fact]
    public void Bench_shape_match_halcon_export_fixtures()
    {
        var benchRoot = ShapeMatchHalconBenchIo.ResolveBenchRoot();
        ShapeMatchHalconBenchIo.ExportFixtures(benchRoot);
        var dir = ShapeMatchHalconBenchIo.ShapeMatchFixturesDir(benchRoot);
        Assert.True(File.Exists(Path.Combine(dir, "teach_0.png")));
        Assert.True(File.Exists(Path.Combine(dir, "live_-37.png")));
        Assert.True(File.Exists(Path.Combine(dir, "live_180.png")));
        output.WriteLine($"Shape match fixtures → {dir}");
    }

    [Fact]
    public void Bench_shape_match_halcon_robotvision_baseline_csv()
    {
        var benchRoot = ShapeMatchHalconBenchIo.ResolveBenchRoot();
        var rows = ShapeMatchHalconBenchIo.BuildRobotVisionBaseline();
        var path = ShapeMatchHalconBenchIo.RobotVisionResultsPath(benchRoot);
        ShapeMatchHalconBenchIo.WriteResultsCsv(path, rows);
        Assert.Equal(ShapeMatchHalconBenchIo.MatrixAngles.Length, rows.Count);
        Assert.True(rows.All(r => r.Ok), "RV shape match must succeed on synthetic matrix");
        output.WriteLine($"Wrote RV shape baseline ({rows.Count} rows) → {path}");
        foreach (var r in rows)
            output.WriteLine($"  {r.TrueDeg,6:0.0}° err={r.AngleErrDeg:0.00}° score={r.Score:0.00}");
    }

    [Fact]
    public void Bench_shape_match_halcon_committed_robotvision_csv_matches_runtime()
    {
        var benchRoot = ShapeMatchHalconBenchIo.ResolveBenchRoot();
        ShapeMatchHalconBenchIo.AssertCommittedRobotVisionCsvMatchesRuntime(benchRoot);
        output.WriteLine($"Committed CSV OK: {ShapeMatchHalconBenchIo.RobotVisionResultsPath(benchRoot)}");
    }

    [SkippableFact]
    public void Bench_shape_match_halcon_side_by_side_engine_parity()
    {
        var benchRoot = ShapeMatchHalconBenchIo.ResolveBenchRoot();
        var halconPath = ShapeMatchHalconBenchIo.HalconResultsPath(benchRoot);
        Skip.If(!File.Exists(halconPath),
            $"HALCON shape results not found: {halconPath}. Run bench_shape_match.hdev on a machine with HDevelop.");

        var halcon = ShapeMatchHalconBenchIo.ReadResultsCsv(halconPath);
        var rv = ShapeMatchHalconBenchIo.ReadResultsCsv(
            ShapeMatchHalconBenchIo.RobotVisionResultsPath(benchRoot));

        var gaps = new List<ShapeMatchHalconEngineCompare.Gap>();
        foreach (var trueDeg in ShapeMatchHalconBenchIo.MatrixAngles)
        {
            if (!halcon.TryGetValue((trueDeg, "halcon"), out var hRow) ||
                !rv.TryGetValue((trueDeg, "robotvision"), out var rRow))
            {
                output.WriteLine($"SKIP missing row true_deg={trueDeg}");
                continue;
            }

            Assert.True(hRow.Ok && rRow.Ok, $"true_deg={trueDeg} both engines must succeed");
            gaps.Add(ShapeMatchHalconEngineCompare.Compare(hRow, rRow));
        }

        Assert.NotEmpty(gaps);
        output.WriteLine(ShapeMatchHalconEngineCompare.FormatGapReport(gaps));
        foreach (var g in gaps)
        {
            Assert.True(g.AngleGapDeg < ShapeMatchHalconBenchGates.EngineAngleGapDeg,
                $"true_deg={g.TrueDeg} 角差 {g.AngleGapDeg:0.000}°");
            Assert.True(g.CenterGapPx < ShapeMatchHalconBenchGates.EngineCenterGapPx,
                $"true_deg={g.TrueDeg} 中心差 {g.CenterGapPx:0.00}px");
        }
    }
}
