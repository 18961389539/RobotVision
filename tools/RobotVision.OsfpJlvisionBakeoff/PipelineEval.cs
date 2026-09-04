using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;

namespace RobotVision.OsfpJlvisionBakeoff;

/// <summary>全链路（分割 + JLVision ShapeMatch 运行时）评测。</summary>
internal static class PipelineEval
{
    public static int Run(
        string repo,
        string captureDir,
        string recipeDir,
        string modelsDir,
        IReadOnlyList<(string File, string Split)> items,
        string outName)
    {
        WriteCoreLock(repo);
        var recipe = new RecipeLoader(recipeDir).Get("Product");
        using var models = new ModelManager(modelsDir);
        var strategy = new MaskTemplateStrategy(models);
        var rows = new List<string>
        {
            "file,split,usable,cx,cy,angle_deg,score,ms,refine_ms,note,flip",
        };
        var bySplit = new Dictionary<string, List<Row>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, split) in items)
        {
            var path = Path.Combine(captureDir, name);
            Console.WriteLine($"-- {name}");
            using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
            var sw = Stopwatch.StartNew();
            List<PixelPose> poses;
            var refineMs = 0.0;
            try
            {
                if (mat.Empty())
                {
                    sw.Stop();
                    Add(rows, bySplit, name, split, false, double.NaN, double.NaN, double.NaN, 0,
                        sw.Elapsed.TotalMilliseconds, 0, "decode_fail", false);
                    continue;
                }

                using var vision = VisionImageCv.FromMat(mat, ownsMat: false);
                InferenceStageClock.Reset();
                poses = strategy.Compute(vision, recipe);
                (_, refineMs) = InferenceStageClock.Snapshot();
            }
            catch (Exception ex)
            {
                sw.Stop();
                Add(rows, bySplit, name, split, false, double.NaN, double.NaN, double.NaN, 0,
                    sw.Elapsed.TotalMilliseconds, 0, ex.Message.Replace(',', ';'), false);
                continue;
            }

            sw.Stop();
            var usable = poses.FirstOrDefault(p => p.Usable);
            var any = poses.FirstOrDefault();
            var pose = usable ?? any;
            var angle = pose?.AngleDeg ?? double.NaN;
            var flip = usable is not null && Math.Abs(AngleGeometry.NormalizeSignedDeg(angle)) > 150;
            var note = usable is not null
                ? (usable.Overlay?.RefineQualityNote ?? "ok")
                : poses.Count == 0
                    ? "no_segment"
                    : "refine_fail " + (any?.Overlay?.RefineQualityNote ?? "");
            Add(rows, bySplit, name, split, usable is not null,
                pose?.Cx ?? double.NaN, pose?.Cy ?? double.NaN, angle, pose?.Score ?? 0,
                sw.Elapsed.TotalMilliseconds, refineMs,
                note.Replace(',', ';').Replace('\n', ' '), flip);
        }

        var outPath = Path.Combine(repo, "benchmarks", "osfp-jlvision", outName);
        File.WriteAllLines(outPath, rows, Encoding.UTF8);
        Console.WriteLine($"wrote {outPath}");
        Print(bySplit, items.Count);
        return 0;
    }

    private static void WriteCoreLock(string repo)
    {
        var probe = Path.Combine(AppContext.BaseDirectory, "JLVisionCore.dll");
        if (!File.Exists(probe))
            return;

        var info = FileVersionInfo.GetVersionInfo(probe);
        var line =
            $"file={Path.GetFileName(probe)}" +
            $" size={new FileInfo(probe).Length}" +
            $" fileVersion={info.FileVersion ?? ""}" +
            $" productVersion={info.ProductVersion ?? ""}" +
            $" lastWriteUtc={File.GetLastWriteTimeUtc(probe):yyyy-MM-ddTHH:mm:ssZ}";
        File.WriteAllText(
            Path.Combine(repo, "benchmarks", "osfp-jlvision", "JLVisionCore.lock.txt"),
            line + Environment.NewLine,
            Encoding.UTF8);
        Console.WriteLine(line);
    }

    private static void Add(
        List<string> rows,
        Dictionary<string, List<Row>> bySplit,
        string file,
        string split,
        bool usable,
        double cx,
        double cy,
        double angle,
        double score,
        double ms,
        double refineMs,
        string note,
        bool flip)
    {
        rows.Add(string.Join(',',
            file,
            split,
            usable ? "yes" : "no",
            F(cx),
            F(cy),
            F(angle),
            F(score),
            ms.ToString("0.0", CultureInfo.InvariantCulture),
            refineMs.ToString("0.0", CultureInfo.InvariantCulture),
            '"' + note.Replace("\"", "'", StringComparison.Ordinal) + '"',
            flip ? "yes" : "no"));
        if (!bySplit.TryGetValue(split, out var list))
        {
            list = [];
            bySplit[split] = list;
        }

        list.Add(new Row(usable, angle, ms, refineMs, flip));
    }

    private static void Print(Dictionary<string, List<Row>> bySplit, int total)
    {
        var all = bySplit.SelectMany(kv => kv.Value).ToList();
        Summarize("ALL", all);
        foreach (var kv in bySplit.OrderBy(k => k.Key, StringComparer.Ordinal))
            Summarize(kv.Key, kv.Value);
        Console.WriteLine($"n={total}");
    }

    private static void Summarize(string label, List<Row> rows)
    {
        var n = rows.Count;
        var ok = rows.Count(r => r.Usable);
        var flips = rows.Count(r => r.Flip);
        var angles = rows.Where(r => r.Usable && double.IsFinite(r.Angle)).Select(r => r.Angle).ToList();
        var sigma = AngleGeometry.CircularStdDeg(angles, 360);
        var p90 = Percentile(rows.Select(r => r.Ms).ToList(), 0.9);
        var refineP90 = Percentile(rows.Select(r => r.RefineMs).ToList(), 0.9);
        Console.WriteLine(
            $"{label}: usable {ok}/{n} ({100.0 * ok / Math.Max(1, n):0.0}%)  flip={flips}  σ={sigma:0.00}°  P90={p90:0.0}ms  refineP90={refineP90:0.0}ms");
    }

    private static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0)
            return 0;
        values.Sort();
        var idx = (int)Math.Clamp(Math.Round(p * (values.Count - 1)), 0, values.Count - 1);
        return values[idx];
    }

    private static string F(double v) =>
        double.IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private readonly record struct Row(bool Usable, double Angle, double Ms, double RefineMs, bool Flip);
}
