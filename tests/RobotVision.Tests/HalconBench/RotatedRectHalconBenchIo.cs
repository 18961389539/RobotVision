using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Vision;

namespace RobotVision.Tests.HalconBench;

internal sealed record RotatedRectBenchResultRow(
    string Id,
    string Scenario,
    double TrueDeg,
    string Engine,
    bool Ok,
    double AngleDeg,
    double CenterX,
    double CenterY,
    double LongLen,
    double ShortLen,
    double RmsPx,
    double Quality)
{
    public string ToCsvLine() => string.Join(',',
        Csv(Id), Csv(Scenario), F(TrueDeg), Csv(Engine), Ok ? "1" : "0",
        F(AngleDeg), F(CenterX), F(CenterY), F(LongLen), F(ShortLen), F(RmsPx), F(Quality));

    public static string Header =>
        "id,scenario,true_deg,engine,ok,angle_deg,center_x,center_y,long_len,short_len,rms_px,quality";

    public static RotatedRectBenchResultRow FromRobotVision(
        RotatedRectHalconFixture fixture, string stage, RotatedRectFitResult fit, double quality = double.NaN) =>
        new(
            fixture.Id,
            fixture.Scenario,
            fixture.TrueDeg,
            $"rv_{stage}",
            fit.Ok,
            fit.AngleDeg,
            fit.Center.X,
            fit.Center.Y,
            fit.LongLen,
            fit.ShortLen,
            fit.RmsPx,
            quality);

    public static bool TryParse(string line, out RotatedRectBenchResultRow row)
    {
        row = default!;
        var parts = line.Split(',');
        if (parts.Length < 12)
            return false;
        if (string.Equals(parts[0], "id", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var trueDeg))
            return false;
        var ok = parts[4] == "1" || (bool.TryParse(parts[4], out var okBool) && okBool);
        if (!TryD(parts[5], out var angle) ||
            !TryD(parts[6], out var cx) ||
            !TryD(parts[7], out var cy) ||
            !TryD(parts[8], out var longLen) ||
            !TryD(parts[9], out var shortLen))
            return false;
        var rms = 0.0;
        var quality = 0.0;
        if (!string.IsNullOrWhiteSpace(parts[10]) && !TryD(parts[10], out rms))
            return false;
        if (parts.Length > 11 && !string.IsNullOrWhiteSpace(parts[11]) && !TryD(parts[11], out quality))
            return false;

        row = new(parts[0], parts[1], trueDeg, parts[3], ok, angle, cx, cy, longLen, shortLen, rms, quality);
        return true;
    }

    private static string Csv(string s) => s.Contains(',', StringComparison.Ordinal) ? $"\"{s}\"" : s;
    private static string F(double v) => double.IsFinite(v) ? v.ToString("0.######", CultureInfo.InvariantCulture) : "";
    private static bool TryD(string s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}

internal static class RotatedRectHalconBenchIo
{
    public static string ResolveBenchRoot()
    {
        var env = Environment.GetEnvironmentVariable("HALCON_BENCH_DIR");
        if (!string.IsNullOrWhiteSpace(env))
            return Path.GetFullPath(env);

        var root = TestBuildPaths.FindRepoRoot();
        return root is null
            ? Path.Combine(Path.GetTempPath(), "RobotVisionHalconBench")
            : Path.Combine(root, "benchmarks", "halcon");
    }

    public static string FixturesDir(string benchRoot) => Path.Combine(benchRoot, "fixtures");
    public static string ResultsDir(string benchRoot) => Path.Combine(benchRoot, "results");

    public static void ExportFixtures(string benchRoot, IEnumerable<RotatedRectHalconFixture> fixtures)
    {
        var dir = FixturesDir(benchRoot);
        Directory.CreateDirectory(dir);

        var manifest = new List<object>();
        foreach (var fx in fixtures)
        {
            using var image = fx.CreateImage();
            var contour = fx.CreateContour();
            var png = Path.Combine(dir, $"{fx.Id}.png");
            Cv2.ImWrite(png, image);
            WriteContour(Path.Combine(dir, $"{fx.Id}.contour.csv"), contour);
            manifest.Add(new
            {
                id = fx.Id,
                scenario = fx.Scenario,
                true_deg = fx.TrueDeg,
                seed_deg = fx.SeedDeg,
                cx = RotatedRectBenchSynth.Cx,
                cy = RotatedRectBenchSynth.Cy,
                long_len = RotatedRectBenchSynth.Long,
                short_len = RotatedRectBenchSynth.Short,
                edge_mode = fx.Options.EdgeMeasureMode.ToString(),
                rv_clip_end_points = fx.Options.ClipEndPoints,
                halcon_clip_end_points = 0,
                halcon_contour_algorithm = "tukey",
                halcon_contour_iterations = 3,
                halcon_contour_clipping_factor = 2,
                image = $"{fx.Id}.png",
                contour = $"{fx.Id}.contour.csv",
            });
        }

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, "manifest.json"), json, Encoding.UTF8);
    }

    public static List<RotatedRectBenchResultRow> RunRobotVision(IEnumerable<RotatedRectHalconFixture> fixtures) =>
        RunRobotVision(fixtures, fx => fx.Options);

    public static List<RotatedRectBenchResultRow> RunRobotVision(
        IEnumerable<RotatedRectHalconFixture> fixtures,
        Func<RotatedRectHalconFixture, RectFitOptions> optSelector)
    {
        var rows = new List<RotatedRectBenchResultRow>();
        foreach (var fx in fixtures)
        {
            using var image = fx.CreateImage();
            var contour = fx.CreateContour();
            if (contour.Length < 12)
                continue;

            var opt = optSelector(fx);
            var seed = double.IsFinite(fx.TrueDeg) ? fx.SeedDeg : MaskHousing.Fit(contour).LongAxisDeg;
            var contourFit = RotatedRectPipeline.FitContour(contour, seed, opt);
            rows.Add(RotatedRectBenchResultRow.FromRobotVision(
                fx, "contour", contourFit, RotatedRectFitQuality.FromContour(contourFit).Score));

            var full = RotatedRectPipeline.Fit(contour, image, seed, opt);
            rows.Add(RotatedRectBenchResultRow.FromRobotVision(
                fx, "full", full, RotatedRectPipeline.QualityScore(full)));
        }
        return rows;
    }

    /// <summary>合成夹具相对 ground truth 门槛（无 HALCON 时仍约束 RV 精度）。</summary>
    public static string AssertSyntheticTruthGates(
        IEnumerable<RotatedRectHalconFixture> fixtures,
        Action<string>? log = null)
    {
        var report = new StringBuilder();
        report.AppendLine("id,stage,theta_err,center_err,oracle_theta,oracle_center");
        foreach (var fx in fixtures.Where(f => double.IsFinite(f.TrueDeg)))
        {
            using var image = fx.CreateImage();
            var contour = fx.CreateContour();
            var truth = RotatedRectSyntheticOracle.From(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
                fx.TrueDeg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);

            var contourFit = RotatedRectPipeline.FitContour(contour, fx.SeedDeg, fx.Options);
            AssertStage(fx, "contour", contourFit, truth, RotatedRectHalconBenchGates.ContourAngleDeg, report, log);

            var full = RotatedRectPipeline.Fit(contour, image, fx.SeedDeg, fx.Options);
            AssertStage(fx, "full", full, truth, RotatedRectHalconBenchGates.FullAngleDeg, report, log);
            RotatedRectSyntheticOracle.AssertHalconGrade(RotatedRectSyntheticOracle.Compare(full, truth), $"{fx.Id}@full");
        }
        return report.ToString();
    }

    public static string WriteTruthGapReport(string benchRoot, IEnumerable<RotatedRectHalconFixture> fixtures) =>
        WriteTruthGapReport(benchRoot, fixtures, fx => fx.Options, "truth_gaps.csv");

    /// <summary>HALCON engine profile（clip=0 轮廓 + measure_pairs）真值差距 CSV。</summary>
    public static string WriteTruthGapReportHalconClip0(string benchRoot, IEnumerable<RotatedRectHalconFixture> fixtures) =>
        WriteTruthGapReport(benchRoot, fixtures, RotatedRectHalconFixtureCatalog.HalconProfileOptions, "truth_gaps_halcon_clip0.csv");

    private static string WriteTruthGapReport(
        string benchRoot,
        IEnumerable<RotatedRectHalconFixture> fixtures,
        Func<RotatedRectHalconFixture, RectFitOptions> optSelector,
        string fileName)
    {
        var rows = BuildTruthGapRows(fixtures, optSelector);
        var path = Path.Combine(ResultsDir(benchRoot), fileName);
        File.WriteAllLines(path, rows, Encoding.UTF8);
        return path;
    }

    private static List<string> BuildTruthGapRows(
        IEnumerable<RotatedRectHalconFixture> fixtures,
        Func<RotatedRectHalconFixture, RectFitOptions> optSelector)
    {
        var rows = new List<string> { "id,stage,theta_err_deg,center_err_px,long_err_px,short_err_px,norm_rms" };
        foreach (var fx in fixtures.Where(f => double.IsFinite(f.TrueDeg)))
        {
            using var image = fx.CreateImage();
            var contour = fx.CreateContour();
            var opt = optSelector(fx);
            var truth = RotatedRectSyntheticOracle.From(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
                fx.TrueDeg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);

            foreach (var (stage, fit) in new[]
                     {
                         ("contour", RotatedRectPipeline.FitContour(contour, fx.SeedDeg, opt)),
                         ("full", RotatedRectPipeline.Fit(contour, image, fx.SeedDeg, opt)),
                     })
            {
                if (!fit.Ok)
                    continue;
                var d = RotatedRectSyntheticOracle.Compare(fit, truth);
                var norm = RotatedRectFitQuality.NormalizedRms(fit);
                rows.Add(string.Join(',',
                    fx.Id, stage,
                    d.AngleDeg.ToString("0.######", CultureInfo.InvariantCulture),
                    d.CenterPx.ToString("0.######", CultureInfo.InvariantCulture),
                    d.LongPx.ToString("0.######", CultureInfo.InvariantCulture),
                    d.ShortPx.ToString("0.######", CultureInfo.InvariantCulture),
                    norm.ToString("0.######", CultureInfo.InvariantCulture)));
            }
        }
        return rows;
    }

    public static List<RotatedRectBenchResultRow> BuildRobotVisionBaseline()
    {
        var rows = RunRobotVision(RotatedRectHalconFixtureCatalog.All());
        var field = RotatedRectHalconFieldFixtures.TryLoad(8);
        if (field.Count > 0)
            rows.AddRange(RunRobotVision(field));
        return rows;
    }

    /// <summary>HALCON engine profile（clip=0 轮廓）RV 基线，供 <c>halcon_results.csv</c> side-by-side。</summary>
    public static List<RotatedRectBenchResultRow> BuildRobotVisionHalconClip0Baseline()
    {
        var rows = RunRobotVision(
            RotatedRectHalconFixtureCatalog.All(),
            RotatedRectHalconFixtureCatalog.HalconProfileOptions);
        var field = RotatedRectHalconFieldFixtures.TryLoad(8);
        if (field.Count > 0)
            rows.AddRange(RunRobotVision(field, RotatedRectHalconFixtureCatalog.HalconProfileOptions));
        return rows;
    }

    public const string RobotVisionHalconClip0Csv = "robotvision_results_halcon_clip0.csv";

    public static void AssertTruthGapGates(IEnumerable<RotatedRectHalconFixture> fixtures, Action<string>? log = null)
    {
        var fullAngles = new List<double>();
        var fullCenters = new List<double>();
        var fullLong = new List<double>();
        var fullShort = new List<double>();
        var fullNorm = new List<double>();
        var contourLong = new List<double>();
        var contourShort = new List<double>();
        foreach (var fx in fixtures.Where(f => double.IsFinite(f.TrueDeg)))
        {
            using var image = fx.CreateImage();
            var contour = fx.CreateContour();
            var truth = RotatedRectSyntheticOracle.From(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
                fx.TrueDeg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
            var contourFit = RotatedRectPipeline.FitContour(contour, fx.SeedDeg, fx.Options);
            Xunit.Assert.True(contourFit.Ok, $"{fx.Id} contour must succeed");
            if (fx.Id.StartsWith("standard_", StringComparison.Ordinal))
            {
                var cd = RotatedRectSyntheticOracle.Compare(contourFit, truth);
                contourLong.Add(cd.LongPx);
                contourShort.Add(cd.ShortPx);
            }

            var full = RotatedRectPipeline.Fit(contour, image, fx.SeedDeg, fx.Options);
            Xunit.Assert.True(full.Ok, $"{fx.Id} full must succeed");
            var d = RotatedRectSyntheticOracle.Compare(full, truth);
            fullAngles.Add(d.AngleDeg);
            fullCenters.Add(d.CenterPx);
            fullLong.Add(d.LongPx);
            fullShort.Add(d.ShortPx);
            fullNorm.Add(RotatedRectFitQuality.NormalizedRms(full));
        }

        var angP90 = Percentile(fullAngles.OrderBy(x => x).ToArray(), 0.9);
        var centerP90 = Percentile(fullCenters.OrderBy(x => x).ToArray(), 0.9);
        var longP90 = Percentile(fullLong.OrderBy(x => x).ToArray(), 0.9);
        var shortP90 = Percentile(fullShort.OrderBy(x => x).ToArray(), 0.9);
        var normP90 = Percentile(fullNorm.OrderBy(x => x).ToArray(), 0.9);
        log?.Invoke($"truth full P90: θ={angP90:0.000}° c={centerP90:0.00}px L={longP90:0.000}px S={shortP90:0.000}px nrms={normP90:0.000}");
        if (contourLong.Count > 0)
        {
            var contourLongP90 = Percentile(contourLong.OrderBy(x => x).ToArray(), 0.9);
            var contourShortP90 = Percentile(contourShort.OrderBy(x => x).ToArray(), 0.9);
            log?.Invoke($"truth contour P90 (standard): L={contourLongP90:0.000}px S={contourShortP90:0.000}px");
            Xunit.Assert.True(contourLongP90 < RotatedRectHalconBenchGates.TruthContourLongP90,
                $"contour long P90={contourLongP90:0.000}px");
            Xunit.Assert.True(contourShortP90 < RotatedRectHalconBenchGates.TruthContourShortP90,
                $"contour short P90={contourShortP90:0.000}px");
        }

        Xunit.Assert.True(angP90 < RotatedRectHalconBenchGates.TruthFullAngleP90,
            $"full θ P90={angP90:0.000}°");
        Xunit.Assert.True(centerP90 < RotatedRectHalconBenchGates.TruthFullCenterP90,
            $"full center P90={centerP90:0.00}px");
        Xunit.Assert.True(longP90 < RotatedRectHalconBenchGates.TruthFullLongP90,
            $"full long P90={longP90:0.000}px");
        Xunit.Assert.True(shortP90 < RotatedRectHalconBenchGates.TruthFullShortP90,
            $"full short P90={shortP90:0.000}px");
        Xunit.Assert.True(normP90 < RotatedRectHalconBenchGates.TruthNormRmsP90,
            $"full norm RMS P90={normP90:0.000}");
    }

    /// <summary>HALCON <c>fit_rectangle2_contour_xld</c> 参数 profile（clip=0）轮廓真值门槛。</summary>
    public static void AssertHalconClip0ContourGates(Action<string>? log = null)
    {
        var longD = new List<double>();
        var shortD = new List<double>();
        var angD = new List<double>();
        foreach (var fx in RotatedRectHalconFixtureCatalog.All().Where(f => f.Id.StartsWith("standard_", StringComparison.Ordinal)))
        {
            var contour = fx.CreateContour();
            var truth = RotatedRectSyntheticOracle.From(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
                fx.TrueDeg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
            var fit = RotatedRectPipeline.FitContour(contour, fx.SeedDeg, RotatedRectHalconFixtureCatalog.HalconEngineOpt);
            Xunit.Assert.True(fit.Ok, $"{fx.Id} halcon_clip0 contour");
            var d = RotatedRectSyntheticOracle.Compare(fit, truth);
            longD.Add(d.LongPx);
            shortD.Add(d.ShortPx);
            angD.Add(d.AngleDeg);
            log?.Invoke(
                $"{fx.Id} halcon_clip0 contour Δθ={d.AngleDeg:0.###}° Δc={d.CenterPx:0.###}px ΔL={d.LongPx:0.###}px ΔS={d.ShortPx:0.###}px " +
                $"L={fit.LongLen:0.###} S={fit.ShortLen:0.###}");
            Xunit.Assert.True(d.AngleDeg < RotatedRectHalconBenchGates.SpecAngleDeg,
                $"{fx.Id} halcon_clip0 contour θ Δ={d.AngleDeg:0.000}°");
            if (fx.Id == "standard_-18")
            {
                Xunit.Assert.True(d.LongPx < RotatedRectHalconBenchGates.TruthContourLongMinus18HalconClip,
                    $"{fx.Id} halcon_clip0 long ΔL={d.LongPx:0.000}px");
            }
            if (fx.Id == "standard_135")
            {
                Xunit.Assert.True(d.LongPx < RotatedRectHalconBenchGates.TruthContourLong135HalconClip,
                    $"{fx.Id} halcon_clip0 long ΔL={d.LongPx:0.000}px");
            }
        }

        var longP90 = Percentile(longD.OrderBy(x => x).ToArray(), 0.9);
        var shortP90 = Percentile(shortD.OrderBy(x => x).ToArray(), 0.9);
        var angP90 = Percentile(angD.OrderBy(x => x).ToArray(), 0.9);
        var longMax = longD.Max();
        log?.Invoke($"halcon_clip0 contour P90 (standard): θ={angP90:0.000}° L={longP90:0.000}px S={shortP90:0.000}px maxL={longMax:0.000}px");
        Xunit.Assert.True(angP90 < RotatedRectHalconBenchGates.TruthContourAngleP90HalconClip,
            $"halcon_clip0 contour θ P90={angP90:0.000}°");
        Xunit.Assert.True(longP90 < RotatedRectHalconBenchGates.TruthContourLongP90HalconClip,
            $"halcon_clip0 long P90={longP90:0.000}px");
        Xunit.Assert.True(longMax < RotatedRectHalconBenchGates.TruthContourLongMaxHalconClip,
            $"halcon_clip0 long max={longMax:0.000}px");
        Xunit.Assert.True(shortP90 < RotatedRectHalconBenchGates.TruthContourShortP90HalconClip,
            $"halcon_clip0 short P90={shortP90:0.000}px");

        AssertHalconClip0DegradationContourGates(log);
    }

    /// <summary>clip=0 轮廓：缺边 / 高 jitter 相对真值（full 链路另有 P90）。</summary>
    private static void AssertHalconClip0DegradationContourGates(Action<string>? log)
    {
        foreach (var fx in RotatedRectHalconFixtureCatalog.All().Where(f =>
                     f.Id.StartsWith("noise_", StringComparison.Ordinal) ||
                     f.Id == "partial_edge"))
        {
            var contour = fx.CreateContour();
            var truth = RotatedRectSyntheticOracle.From(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
                fx.TrueDeg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
            var fit = RotatedRectPipeline.FitContour(
                contour, fx.SeedDeg, RotatedRectHalconFixtureCatalog.HalconProfileOptions(fx));
            Xunit.Assert.True(fit.Ok, $"{fx.Id} halcon_clip0 contour");
            var d = RotatedRectSyntheticOracle.Compare(fit, truth);
            log?.Invoke($"{fx.Id} halcon_clip0 contour ΔL={d.LongPx:0.###}px ΔS={d.ShortPx:0.###}px");
            var longGate = fx.Id == "partial_edge"
                ? RotatedRectHalconBenchGates.TruthContourLongPartialHalconClip
                : RotatedRectHalconBenchGates.TruthContourLongNoiseHalconClip;
            Xunit.Assert.True(d.LongPx < longGate,
                $"{fx.Id} halcon_clip0 contour ΔL={d.LongPx:0.000}px (gate {longGate})");
            if (fx.Id.StartsWith("noise_", StringComparison.Ordinal))
            {
                Xunit.Assert.True(d.ShortPx < RotatedRectHalconBenchGates.TruthContourShortNoiseHalconClip,
                    $"{fx.Id} halcon_clip0 contour ΔS={d.ShortPx:0.000}px");
            }
        }
    }

    /// <summary>HALCON engine profile 全链路（clip=0 轮廓 + measure_pairs）真值 P90 门槛。</summary>
    public static void AssertHalconClip0FullGates(Action<string>? log = null)
    {
        var angles = new List<double>();
        var centers = new List<double>();
        var longD = new List<double>();
        var shortD = new List<double>();
        var norm = new List<double>();
        foreach (var fx in RotatedRectHalconFixtureCatalog.All().Where(f => double.IsFinite(f.TrueDeg)))
        {
            using var image = fx.CreateImage();
            var contour = fx.CreateContour();
            var truth = RotatedRectSyntheticOracle.From(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
                fx.TrueDeg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
            var full = RotatedRectPipeline.Fit(
                contour, image, fx.SeedDeg, RotatedRectHalconFixtureCatalog.HalconProfileOptions(fx));
            Xunit.Assert.True(full.Ok, $"{fx.Id} halcon_clip0 full");
            var d = RotatedRectSyntheticOracle.Compare(full, truth);
            angles.Add(d.AngleDeg);
            centers.Add(d.CenterPx);
            longD.Add(d.LongPx);
            shortD.Add(d.ShortPx);
            norm.Add(RotatedRectFitQuality.NormalizedRms(full));
            log?.Invoke($"{fx.Id} halcon_clip0 full Δθ={d.AngleDeg:0.###}° Δc={d.CenterPx:0.###}px ΔL={d.LongPx:0.###}px ΔS={d.ShortPx:0.###}px");
            Xunit.Assert.True(d.AngleDeg < RotatedRectHalconBenchGates.SpecAngleDeg,
                $"{fx.Id} full θ Δ={d.AngleDeg:0.000}°");
            Xunit.Assert.True(d.CenterPx < RotatedRectHalconBenchGates.SpecCenterPx,
                $"{fx.Id} full center Δ={d.CenterPx:0.000}px");
        }

        var angP90 = Percentile(angles.OrderBy(x => x).ToArray(), 0.9);
        var centerP90 = Percentile(centers.OrderBy(x => x).ToArray(), 0.9);
        var longP90 = Percentile(longD.OrderBy(x => x).ToArray(), 0.9);
        var shortP90 = Percentile(shortD.OrderBy(x => x).ToArray(), 0.9);
        var normP90 = Percentile(norm.OrderBy(x => x).ToArray(), 0.9);
        log?.Invoke($"halcon_clip0 full P90: θ={angP90:0.000}° c={centerP90:0.00}px L={longP90:0.000}px S={shortP90:0.000}px nrms={normP90:0.000}");
        Xunit.Assert.True(angP90 < RotatedRectHalconBenchGates.TruthFullAngleP90,
            $"halcon_clip0 full θ P90={angP90:0.000}°");
        Xunit.Assert.True(centerP90 < RotatedRectHalconBenchGates.TruthFullCenterP90,
            $"halcon_clip0 full center P90={centerP90:0.00}px");
        Xunit.Assert.True(longP90 < RotatedRectHalconBenchGates.TruthFullLongP90,
            $"halcon_clip0 full long P90={longP90:0.000}px");
        Xunit.Assert.True(shortP90 < RotatedRectHalconBenchGates.TruthFullShortP90,
            $"halcon_clip0 full short P90={shortP90:0.000}px");
        Xunit.Assert.True(normP90 < RotatedRectHalconBenchGates.TruthNormRmsP90,
            $"halcon_clip0 full norm RMS P90={normP90:0.000}");
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0)
            return double.NaN;
        var i = (int)Math.Clamp(Math.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[i];
    }

    private static void AssertStage(
        RotatedRectHalconFixture fx,
        string stage,
        RotatedRectFitResult fit,
        RotatedRectSyntheticOracle.Truth truth,
        double angleGate,
        StringBuilder report,
        Action<string>? log)
    {
        Xunit.Assert.True(fit.Ok, $"{fx.Id}/{stage} must succeed");
        var delta = RotatedRectSyntheticOracle.Compare(fit, truth);
        report.AppendLine(string.Join(',',
            fx.Id, stage,
            delta.AngleDeg.ToString("0.000", CultureInfo.InvariantCulture),
            delta.CenterPx.ToString("0.00", CultureInfo.InvariantCulture),
            delta.AngleDeg.ToString("0.000", CultureInfo.InvariantCulture),
            delta.CenterPx.ToString("0.00", CultureInfo.InvariantCulture)));
        log?.Invoke($"{fx.Id}/{stage} θΔ={delta.AngleDeg:0.000}° cΔ={delta.CenterPx:0.00}px");
        Xunit.Assert.True(delta.AngleDeg < angleGate, $"{fx.Id}/{stage} θΔ={delta.AngleDeg:0.000}°");
        if (stage == "full")
            Xunit.Assert.True(delta.CenterPx < RotatedRectHalconBenchGates.CenterPx, $"{fx.Id} center");
    }

    public static void WriteResultsCsv(string path, IEnumerable<RotatedRectBenchResultRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = new List<string> { RotatedRectBenchResultRow.Header };
        lines.AddRange(rows.Select(r => r.ToCsvLine()));
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    public static Dictionary<(string Id, string Engine), RotatedRectBenchResultRow> ReadResultsCsv(string path)
    {
        var map = new Dictionary<(string, string), RotatedRectBenchResultRow>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (!RotatedRectBenchResultRow.TryParse(line.Trim(), out var row))
                continue;
            map[(row.Id, row.Engine)] = row;
        }
        return map;
    }

    /// <summary>若存在 <c>halcon_results.csv</c>，校验表头、行解析与合成夹具 halcon_contour/full 覆盖。</summary>
    public static void AssertHalconResultsCsvSchema(string path, Action<string>? log = null)
    {
        var lines = File.ReadAllLines(path);
        Xunit.Assert.NotEmpty(lines);
        Xunit.Assert.Equal(RotatedRectBenchResultRow.Header, lines[0].Trim());
        var map = ReadResultsCsv(path);
        Xunit.Assert.NotEmpty(map);
        var halconRows = map.Values.Where(r => r.Engine.StartsWith("halcon_", StringComparison.Ordinal)).ToArray();
        Xunit.Assert.NotEmpty(halconRows);
        Xunit.Assert.True(halconRows.All(r => r.Ok), "HALCON rows must be ok=1");
        foreach (var fx in RotatedRectHalconFixtureCatalog.All())
        {
            foreach (var stage in new[] { "contour", "full" })
            {
                var key = (fx.Id, $"halcon_{stage}");
                Xunit.Assert.True(map.ContainsKey(key), $"HALCON CSV missing {fx.Id}/halcon_{stage}");
            }
        }
        log?.Invoke($"HALCON CSV schema OK: {halconRows.Length} halcon_* rows, {map.Count} total");
    }

    /// <summary>已提交的 RV 基线 CSV 须与当前引擎一致（合成夹具）。</summary>
    public static void AssertCommittedRobotVisionCsvMatchesRuntime(
        string benchRoot,
        IEnumerable<RotatedRectHalconFixture> syntheticFixtures) =>
        AssertCommittedRobotVisionCsvMatchesRuntime(
            benchRoot, syntheticFixtures, "robotvision_results.csv", RunRobotVision);

    public static void AssertCommittedRobotVisionHalconClip0CsvMatchesRuntime(
        string benchRoot,
        IEnumerable<RotatedRectHalconFixture> syntheticFixtures) =>
        AssertCommittedRobotVisionCsvMatchesRuntime(
            benchRoot,
            syntheticFixtures,
            RobotVisionHalconClip0Csv,
            fixtures => RunRobotVision(fixtures, RotatedRectHalconFixtureCatalog.HalconProfileOptions));

    private static void AssertCommittedRobotVisionCsvMatchesRuntime(
        string benchRoot,
        IEnumerable<RotatedRectHalconFixture> syntheticFixtures,
        string fileName,
        Func<IEnumerable<RotatedRectHalconFixture>, List<RotatedRectBenchResultRow>> run)
    {
        var path = Path.Combine(ResultsDir(benchRoot), fileName);
        Xunit.Assert.True(File.Exists(path),
            $"缺少 {path}，请运行 benchmarks/halcon/run_halcon_bench.ps1 后提交。");
        var committed = ReadResultsCsv(path);
        var runtime = run(syntheticFixtures);
        foreach (var row in runtime)
        {
            var key = (row.Id, row.Engine);
            Xunit.Assert.True(committed.TryGetValue(key, out var saved), $"CSV 缺少 {row.Id}/{row.Engine}");
            Xunit.Assert.Equal(row.Ok, saved.Ok);
            AssertClose(row.AngleDeg, saved.AngleDeg, 0.003, $"{key} angle");
            AssertClose(row.CenterX, saved.CenterX, 0.05, $"{key} cx");
            AssertClose(row.CenterY, saved.CenterY, 0.05, $"{key} cy");
            AssertClose(row.LongLen, saved.LongLen, 0.05, $"{key} long");
            AssertClose(row.ShortLen, saved.ShortLen, 0.05, $"{key} short");
            AssertClose(row.RmsPx, saved.RmsPx, 0.02, $"{key} rms");
            if (double.IsFinite(row.Quality) && double.IsFinite(saved.Quality))
                AssertClose(row.Quality, saved.Quality, 0.02, $"{key} quality");
        }
    }

    private static void AssertClose(double a, double b, double tol, string label) =>
        Xunit.Assert.True(Math.Abs(a - b) <= tol, $"{label}: runtime={a:0.######} csv={b:0.######}");

    private static void WriteContour(string path, IReadOnlyList<Point2f> contour)
    {
        var sb = new StringBuilder();
        sb.AppendLine("x,y");
        foreach (var p in contour)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###}", p.X, p.Y));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}

internal static class RotatedRectHalconEngineCompare
{
    public readonly record struct Gap(
        string Id,
        string Stage,
        double AngleDeg,
        double CenterPx,
        double LongPx,
        double ShortPx);

    public static Gap Compare(RotatedRectBenchResultRow halcon, RotatedRectBenchResultRow rv)
    {
        var ang = AngleGeometry.UndirectedDeltaDeg(halcon.AngleDeg, rv.AngleDeg);
        var dx = halcon.CenterX - rv.CenterX;
        var dy = halcon.CenterY - rv.CenterY;
        var center = Math.Sqrt(dx * dx + dy * dy);
        var longPx = Math.Min(Math.Abs(halcon.LongLen - rv.LongLen), Math.Abs(halcon.LongLen - rv.ShortLen));
        var shortPx = Math.Min(Math.Abs(halcon.ShortLen - rv.ShortLen), Math.Abs(halcon.ShortLen - rv.LongLen));
        return new(halcon.Id, halcon.Engine.Replace("halcon_", "", StringComparison.Ordinal), ang, center, longPx, shortPx);
    }

    public static string FormatGapReport(IEnumerable<Gap> gaps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,stage,angle_gap_deg,center_gap_px,long_gap_px,short_gap_px");
        foreach (var g in gaps.OrderBy(g => g.Id).ThenBy(g => g.Stage))
        {
            sb.AppendLine(string.Join(',',
                g.Id,
                g.Stage,
                g.AngleDeg.ToString("0.######", CultureInfo.InvariantCulture),
                g.CenterPx.ToString("0.######", CultureInfo.InvariantCulture),
                g.LongPx.ToString("0.######", CultureInfo.InvariantCulture),
                g.ShortPx.ToString("0.######", CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }
}
