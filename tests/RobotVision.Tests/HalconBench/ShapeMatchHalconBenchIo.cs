using System.Globalization;
using System.Text;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Vision;

namespace RobotVision.Tests.HalconBench;

internal sealed record ShapeMatchBenchResultRow(
    double TrueDeg,
    string Engine,
    bool Ok,
    double AngleDeg,
    double AngleErrDeg,
    double Score,
    double CenterX,
    double CenterY)
{
    public string ToCsvLine() => string.Join(',',
        F(TrueDeg), Csv(Engine), Ok ? "1" : "0",
        F(AngleDeg), F(AngleErrDeg), F(Score), F(CenterX), F(CenterY));

    public static string Header =>
        "true_deg,engine,ok,angle_deg,angle_err_deg,score,center_x,center_y";

    public static bool TryParse(string line, out ShapeMatchBenchResultRow row)
    {
        row = default!;
        var parts = line.Split(',');
        if (parts.Length < 8)
            return false;
        if (string.Equals(parts[1], "engine", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!TryD(parts[0], out var trueDeg))
            return false;
        var ok = parts[2] == "1";
        if (!TryD(parts[3], out var angle) ||
            !TryD(parts[4], out var err) ||
            !TryD(parts[5], out var score) ||
            !TryD(parts[6], out var cx) ||
            !TryD(parts[7], out var cy))
            return false;
        row = new(trueDeg, parts[1], ok, angle, err, score, cx, cy);
        return true;
    }

    private static string Csv(string s) => s.Contains(',', StringComparison.Ordinal) ? $"\"{s}\"" : s;
    private static string F(double v) => double.IsFinite(v) ? v.ToString("0.######", CultureInfo.InvariantCulture) : "";
    private static bool TryD(string s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}

internal static class ShapeMatchHalconBenchIo
{
    public static readonly double[] MatrixAngles = [-37, -20, -8.7, 0, 8.7, 20, 37, 180];

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

    public static string ShapeMatchFixturesDir(string benchRoot) =>
        Path.Combine(benchRoot, "fixtures", "shape_match");

    public static string ResultsDir(string benchRoot) => Path.Combine(benchRoot, "results");

    public static string RobotVisionResultsPath(string benchRoot) =>
        Path.Combine(ResultsDir(benchRoot), "shape_match_robotvision_results.csv");

    public static string HalconResultsPath(string benchRoot) =>
        Path.Combine(ResultsDir(benchRoot), "shape_match_halcon_results.csv");

    public static void ExportFixtures(string benchRoot) =>
        ShapeMatchBenchSynth.ExportHalconFixtures(ShapeMatchFixturesDir(benchRoot));

    public static List<ShapeMatchBenchResultRow> BuildRobotVisionBaseline()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachContour = ShapeMatchBenchSynth.Contour(0);
        using var model = ShapeMatchBenchSynth.Teach(teachImg, teachContour)
                          ?? throw new InvalidOperationException("shape teach model is null");

        var rows = new List<ShapeMatchBenchResultRow>(MatrixAngles.Length);
        foreach (var deg in MatrixAngles)
        {
            using var img = ShapeMatchBenchSynth.Paint(deg);
            var contour = ShapeMatchBenchSynth.Contour(deg);
            var noFlip = Math.Abs(Math.Abs(deg) - 180.0) > 1.0;
            var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8, noFlip);
            if (attempt.Pose is not { } pose)
            {
                rows.Add(new(deg, "robotvision", false, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN));
                continue;
            }

            var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(pose.AngleDeg - deg));
            rows.Add(new(
                deg, "robotvision", true, pose.AngleDeg, err, pose.Score,
                pose.Center.X, pose.Center.Y));
        }

        return rows;
    }

    public static void WriteResultsCsv(string path, IEnumerable<ShapeMatchBenchResultRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = new List<string> { ShapeMatchBenchResultRow.Header };
        lines.AddRange(rows.Select(r => r.ToCsvLine()));
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    public static Dictionary<(double TrueDeg, string Engine), ShapeMatchBenchResultRow> ReadResultsCsv(string path)
    {
        var map = new Dictionary<(double, string), ShapeMatchBenchResultRow>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (!ShapeMatchBenchResultRow.TryParse(line.Trim(), out var row))
                continue;
            map[(row.TrueDeg, row.Engine)] = row;
        }
        return map;
    }

    public static void AssertCommittedRobotVisionCsvMatchesRuntime(string benchRoot)
    {
        var path = RobotVisionResultsPath(benchRoot);
        Xunit.Assert.True(File.Exists(path),
            $"缺少 {path}，请运行 Bench_shape_match_halcon_robotvision_baseline_csv 后提交。");
        var committed = ReadResultsCsv(path);
        var runtime = BuildRobotVisionBaseline();
        foreach (var row in runtime)
        {
            var key = (row.TrueDeg, row.Engine);
            Xunit.Assert.True(committed.TryGetValue(key, out var saved), $"CSV 缺少 true_deg={row.TrueDeg} engine={row.Engine}");
            Xunit.Assert.Equal(row.Ok, saved.Ok);
            if (!row.Ok)
                continue;
            AssertClose(row.AngleDeg, saved.AngleDeg, 0.02, $"{row.TrueDeg}° angle");
            AssertClose(row.AngleErrDeg, saved.AngleErrDeg, 0.02, $"{row.TrueDeg}° err");
            AssertClose(row.Score, saved.Score, 0.03, $"{row.TrueDeg}° score");
            AssertClose(row.CenterX, saved.CenterX, 0.5, $"{row.TrueDeg}° cx");
            AssertClose(row.CenterY, saved.CenterY, 0.5, $"{row.TrueDeg}° cy");
        }
    }

    private static void AssertClose(double a, double b, double tol, string label) =>
        Xunit.Assert.True(Math.Abs(a - b) <= tol, $"{label}: runtime={a:0.######} csv={b:0.######}");
}

internal static class ShapeMatchHalconEngineCompare
{
    public readonly record struct Gap(double TrueDeg, double AngleGapDeg, double CenterGapPx);

    public static Gap Compare(ShapeMatchBenchResultRow halcon, ShapeMatchBenchResultRow rv)
    {
        var ang = Math.Abs(AngleGeometry.NormalizeSignedDeg(halcon.AngleDeg - rv.AngleDeg));
        var dx = halcon.CenterX - rv.CenterX;
        var dy = halcon.CenterY - rv.CenterY;
        var center = Math.Sqrt(dx * dx + dy * dy);
        return new(halcon.TrueDeg, ang, center);
    }

    public static string FormatGapReport(IEnumerable<Gap> gaps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("true_deg,angle_gap_deg,center_gap_px");
        foreach (var g in gaps.OrderBy(g => g.TrueDeg))
        {
            sb.AppendLine(string.Join(',',
                g.TrueDeg.ToString("0.######", CultureInfo.InvariantCulture),
                g.AngleGapDeg.ToString("0.######", CultureInfo.InvariantCulture),
                g.CenterGapPx.ToString("0.######", CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }
}
