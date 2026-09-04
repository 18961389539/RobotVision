using RobotVision.Tests.HalconBench;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// HALCON fit_rectangle2 真机 side-by-side：导出夹具、生成 RV 基线、与 halcon_results.csv 对比。
/// </summary>
[Trait("Category", "Bench")]
public sealed class RotatedRectHalconSideBySideTests(ITestOutputHelper output)
{
    [Fact]
    public void Bench_halcon_export_fixtures_to_disk()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        var fixtures = RotatedRectHalconFixtureCatalog.All();
        RotatedRectHalconBenchIo.ExportFixtures(benchRoot, fixtures);

        var manifest = Path.Combine(RotatedRectHalconBenchIo.FixturesDir(benchRoot), "manifest.json");
        Assert.True(File.Exists(manifest));
        Assert.True(fixtures.All(fx => File.Exists(Path.Combine(
            RotatedRectHalconBenchIo.FixturesDir(benchRoot), $"{fx.Id}.png"))));
        output.WriteLine($"Exported {fixtures.Count} fixtures → {RotatedRectHalconBenchIo.FixturesDir(benchRoot)}");
    }

    [Fact]
    public void Bench_halcon_fixture_files_are_paired()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        var dir = RotatedRectHalconBenchIo.FixturesDir(benchRoot);
        foreach (var fx in RotatedRectHalconFixtureCatalog.All())
        {
            Assert.True(File.Exists(Path.Combine(dir, $"{fx.Id}.png")), fx.Id);
            Assert.True(File.Exists(Path.Combine(dir, $"{fx.Id}.contour.csv")), fx.Id);
        }

        var fieldDir = Path.Combine(dir, "field");
        if (!Directory.Exists(fieldDir))
        {
            output.WriteLine("No field fixtures directory (skip field pairing check).");
            return;
        }

        foreach (var png in Directory.GetFiles(fieldDir, "*.png"))
        {
            var id = Path.GetFileNameWithoutExtension(png);
            Assert.True(File.Exists(Path.Combine(fieldDir, $"{id}.contour.csv")), id);
        }
        output.WriteLine($"Fixture pairing OK: synthetic={RotatedRectHalconFixtureCatalog.All().Count} field PNG={Directory.GetFiles(fieldDir, "*.png").Length}");
    }

    [Fact]
    public void Bench_halcon_robotvision_baseline_csv()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        var fixtures = RotatedRectHalconFixtureCatalog.All();
        var report = RotatedRectHalconBenchIo.AssertSyntheticTruthGates(fixtures, output.WriteLine);
        output.WriteLine(report);

        var rows = RotatedRectHalconBenchIo.BuildRobotVisionBaseline();
        var path = Path.Combine(RotatedRectHalconBenchIo.ResultsDir(benchRoot), "robotvision_results.csv");
        RotatedRectHalconBenchIo.WriteResultsCsv(path, rows);

        var halconClip0Rows = RotatedRectHalconBenchIo.BuildRobotVisionHalconClip0Baseline();
        var halconClip0Path = Path.Combine(
            RotatedRectHalconBenchIo.ResultsDir(benchRoot),
            RotatedRectHalconBenchIo.RobotVisionHalconClip0Csv);
        RotatedRectHalconBenchIo.WriteResultsCsv(halconClip0Path, halconClip0Rows);

        var synthRows = rows.Where(r => !r.Id.StartsWith("field_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(RotatedRectHalconFixtureCatalog.All().Count * 2, synthRows.Length);
        Assert.True(synthRows.All(r => r.Ok), "RV baseline must succeed on all synthetic fixtures");
        var fieldRows = rows.Where(r => r.Id.StartsWith("field_", StringComparison.Ordinal)).ToArray();
        if (fieldRows.Length > 0)
            output.WriteLine($"Field baseline rows appended: {fieldRows.Length}");
        output.WriteLine($"Wrote RV baseline ({rows.Count} rows) → {path}");
        output.WriteLine($"Wrote HALCON clip=0 RV baseline ({halconClip0Rows.Count} rows) → {halconClip0Path}");
    }

    [SkippableFact]
    public void Bench_halcon_export_field_fixtures()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        RotatedRectHalconFieldFixtures.ExportFieldSubset(benchRoot, maxCount: 8);
        var fieldDir = Path.Combine(RotatedRectHalconBenchIo.FixturesDir(benchRoot), "field");
        Skip.If(!Directory.Exists(fieldDir) || Directory.GetFiles(fieldDir, "*.png").Length == 0,
            "No field captures (set FIELD_CAPTURE_DIR or RobotVisionData).");

        var fixtures = RotatedRectHalconFieldFixtures.TryLoad(8);
        Assert.NotEmpty(fixtures);
        var rows = RotatedRectHalconBenchIo.RunRobotVision(fixtures);
        Assert.True(rows.Count >= fixtures.Count, "Field fixtures should produce RV rows");
        Assert.True(rows.All(r => r.Ok), "RV must succeed on exported field silhouettes");
        output.WriteLine($"Field HALCON fixtures: {Directory.GetFiles(fieldDir, "*.png").Length} PNG → {fieldDir}");
        output.WriteLine($"Field RV baseline rows: {rows.Count(r => r.Ok)}/{rows.Count}");
    }

    [Fact]
    public void Bench_halcon_gap_report()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        var fixtures = RotatedRectHalconFixtureCatalog.All();
        var path = RotatedRectHalconBenchIo.WriteTruthGapReport(benchRoot, fixtures);
        var pathClip0 = RotatedRectHalconBenchIo.WriteTruthGapReportHalconClip0(benchRoot, fixtures);
        Assert.True(File.Exists(path));
        Assert.True(File.Exists(pathClip0));
        RotatedRectHalconBenchIo.AssertTruthGapGates(fixtures, output.WriteLine);
        var lines = File.ReadAllLines(path).Skip(1).ToArray();
        Assert.True(lines.Length >= fixtures.Count, "Gap report should cover all synthetic fixtures");
        output.WriteLine($"Truth gap report → {path} ({lines.Length} rows)");
        output.WriteLine($"HALCON clip=0 gap report → {pathClip0}");
        foreach (var line in lines.Take(6))
            output.WriteLine(line);
    }

    [Fact]
    public void Bench_halcon_committed_robotvision_csv_matches_runtime()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        RotatedRectHalconBenchIo.AssertCommittedRobotVisionCsvMatchesRuntime(
            benchRoot, RotatedRectHalconFixtureCatalog.All());
        output.WriteLine($"Committed CSV OK: {Path.Combine(RotatedRectHalconBenchIo.ResultsDir(benchRoot), "robotvision_results.csv")}");
    }

    [Fact]
    public void Bench_halcon_committed_robotvision_halcon_clip0_csv_matches_runtime()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        RotatedRectHalconBenchIo.AssertCommittedRobotVisionHalconClip0CsvMatchesRuntime(
            benchRoot, RotatedRectHalconFixtureCatalog.All());
        output.WriteLine($"Committed CSV OK: {Path.Combine(
            RotatedRectHalconBenchIo.ResultsDir(benchRoot),
            RotatedRectHalconBenchIo.RobotVisionHalconClip0Csv)}");
    }

    [Fact]
    public void Bench_halcon_results_csv_schema_when_present()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        var halconPath = Path.Combine(RotatedRectHalconBenchIo.ResultsDir(benchRoot), "halcon_results.csv");
        if (!File.Exists(halconPath))
        {
            output.WriteLine($"No halcon_results.csv — schema check skipped ({halconPath})");
            return;
        }
        RotatedRectHalconBenchIo.AssertHalconResultsCsvSchema(halconPath, output.WriteLine);
    }

    [SkippableFact]
    public void Bench_halcon_side_by_side_engine_parity()
    {
        var benchRoot = RotatedRectHalconBenchIo.ResolveBenchRoot();
        var halconPath = Path.Combine(RotatedRectHalconBenchIo.ResultsDir(benchRoot), "halcon_results.csv");
        Skip.If(!File.Exists(halconPath),
            $"HALCON results not found (expected without HALCON install): {halconPath}. " +
            "See benchmarks/halcon/README.md — commit halcon_results.csv from a machine with HDevelop, or run run_halcon_bench.ps1 -RunHalcon.");

        var halcon = RotatedRectHalconBenchIo.ReadResultsCsv(halconPath);
        var rvPath = Path.Combine(
            RotatedRectHalconBenchIo.ResultsDir(benchRoot),
            RotatedRectHalconBenchIo.RobotVisionHalconClip0Csv);
        Xunit.Assert.True(File.Exists(rvPath),
            $"缺少 {rvPath}（HALCON clip=0 RV 基线）。请运行 Bench_halcon_robotvision_baseline 或 run_halcon_bench.ps1。");
        var rv = RotatedRectHalconBenchIo.ReadResultsCsv(rvPath);

        var gaps = new List<RotatedRectHalconEngineCompare.Gap>();
        foreach (var ((id, engine), hRow) in halcon)
        {
            if (!engine.StartsWith("halcon_", StringComparison.Ordinal))
                continue;
            var stage = engine["halcon_".Length..];
            if (!rv.TryGetValue((id, $"rv_{stage}"), out var rRow))
            {
                output.WriteLine($"SKIP missing RV row {id} rv_{stage}");
                continue;
            }
            Assert.True(hRow.Ok && rRow.Ok, $"{id}/{stage} both engines must succeed");
            gaps.Add(RotatedRectHalconEngineCompare.Compare(hRow, rRow));
        }

        Assert.NotEmpty(gaps);
        output.WriteLine(RotatedRectHalconEngineCompare.FormatGapReport(gaps));

        var angP50 = Percentile(gaps.Select(g => g.AngleDeg).OrderBy(x => x).ToArray(), 0.5);
        var centerP50 = Percentile(gaps.Select(g => g.CenterPx).OrderBy(x => x).ToArray(), 0.5);
        output.WriteLine($"引擎间差距 P50: 角={angP50:0.000}° 中心={centerP50:0.00}px (n={gaps.Count})");

        foreach (var g in gaps)
        {
            Assert.True(g.AngleDeg < RotatedRectHalconBenchGates.EngineAngleGapDeg,
                $"{g.Id}/{g.Stage} 角差 {g.AngleDeg:0.000}°");
            Assert.True(g.CenterPx < RotatedRectHalconBenchGates.EngineCenterGapPx,
                $"{g.Id}/{g.Stage} 中心差 {g.CenterPx:0.00}px");
            Assert.True(g.LongPx < RotatedRectHalconBenchGates.EngineLongGapPx,
                $"{g.Id}/{g.Stage} 长边差 {g.LongPx:0.00}px");
            Assert.True(g.ShortPx < RotatedRectHalconBenchGates.EngineShortGapPx,
                $"{g.Id}/{g.Stage} 短边差 {g.ShortPx:0.00}px");
        }
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0)
            return double.NaN;
        var i = (int)Math.Clamp(Math.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[i];
    }
}
