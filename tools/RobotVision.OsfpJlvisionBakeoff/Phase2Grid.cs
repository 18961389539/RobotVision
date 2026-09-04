using System.Diagnostics;
using System.Globalization;
using System.Text;
using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using RobotVision.JlVision;
using RobotVision.Vision;

namespace RobotVision.OsfpJlvisionBakeoff;

internal static class Phase2Grid
{
    public static int Run(
        string repo,
        string captureDir,
        string recipeDir,
        string modelsDir,
        IReadOnlyList<string> files)
    {
        var recipe = new RecipeLoader(recipeDir).Get("Product");
        using var models = new ModelManager(modelsDir);
        using var template = MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
        using var templateGray = JlImageConvert.ToGray(template);
        using var shapeModel = JlShapeRefine.CreateModel(templateGray);
        using var nccModel = JlNccRefine.CreateModel(templateGray);

        var items = new List<CachedSeg>();
        try
        {
            foreach (var name in files)
            {
                var path = Path.Combine(captureDir, name);
                Console.WriteLine($"seg {name}");
                var item = SegmentOne(path, recipe, models);
                if (item is not null)
                    items.Add(item);
            }

            var rows = new List<string>
            {
                "config,file,found,angle_deg,score,ms,note,coarse_deg,flip_vs_coarse",
            };
            var summaries = new List<string>();

            foreach (var cfg in BConfigs())
            {
                var (ok, flips, sigma, p90, n) = EvalB(items, shapeModel, cfg, rows);
                summaries.Add(
                    $"B min={cfg.MinScore:0.00} g={cfg.Greediness:0.0} lv={cfg.NumLevels} up={cfg.PreferUpright} " +
                    $"found {ok}/{n} flip={flips} σ={sigma:0.00}° P90={p90:0.0}ms");
                Console.WriteLine(summaries[^1]);
            }

            foreach (var minScore in new[] { 0.55, 0.75, 0.85 })
            {
                var (ok, flips, sigma, p90, n) = EvalE(items, nccModel, minScore, rows);
                summaries.Add(
                    $"E min={minScore:0.00} found {ok}/{n} flip={flips} σ={sigma:0.00}° P90={p90:0.0}ms");
                Console.WriteLine(summaries[^1]);
            }

            var outCsv = Path.Combine(repo, "benchmarks", "osfp-jlvision", "jlvision_p2_grid.csv");
            File.WriteAllLines(outCsv, rows, Encoding.UTF8);
            var outSum = Path.Combine(repo, "benchmarks", "osfp-jlvision", "jlvision_p2_summary.txt");
            File.WriteAllLines(outSum, summaries, Encoding.UTF8);
            Console.WriteLine($"wrote {outCsv}");
            return 0;
        }
        finally
        {
            foreach (var item in items)
                item.Dispose();
        }
    }

    private static IEnumerable<BConfig> BConfigs()
    {
        yield return new BConfig(0.45, 0.9, 0, false);
        foreach (var min in new[] { 0.40, 0.50, 0.60, 0.75 })
        foreach (var g in new[] { 0.7, 0.9 })
            yield return new BConfig(min, g, 0, true);
    }

    private static (int Ok, int Flips, double Sigma, double P90, int N) EvalB(
        List<CachedSeg> items, JlShapeModel model, BConfig cfg, List<string> rows)
    {
        var options = new JlFindOptions
        {
            PreferUpright = cfg.PreferUpright,
            FlipScoreMargin = 0.08,
            NumLevels = cfg.NumLevels,
            Greediness = cfg.Greediness,
        };
        var tag = $"B_min{cfg.MinScore:0.00}_g{cfg.Greediness:0.0}_up{cfg.PreferUpright}";
        var angles = new List<double>();
        var times = new List<double>();
        var ok = 0;
        var flips = 0;
        foreach (var item in items)
        {
            var sw = Stopwatch.StartNew();
            JlRefineHit hit;
            try
            {
                hit = JlShapeRefine.TryRefine(item.Scene, item.Points, model, item.Range, 0.3, options);
            }
            catch (Exception ex)
            {
                hit = JlRefineHit.Miss(ex.Message);
            }

            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            var gated = hit.Found && hit.Score >= cfg.MinScore;
            var flip = gated && Math.Abs(AngleGeometry.NormalizeSignedDeg(hit.AngleDeg)) > 150;
            if (gated)
            {
                ok++;
                angles.Add(hit.AngleDeg);
            }

            if (flip)
                flips++;

            rows.Add(string.Join(',',
                tag,
                item.File,
                gated ? "yes" : "no",
                F(hit.AngleDeg),
                F(hit.Score),
                sw.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture),
                '"' + hit.Note.Replace("\"", "'", StringComparison.Ordinal) + '"',
                F(item.CoarseDeg),
                flip ? "yes" : "no"));
        }

        var sigma = AngleGeometry.CircularStdDeg(angles, 360);
        var ordered = times.OrderBy(x => x).ToArray();
        var p90 = ordered.Length == 0
            ? 0
            : ordered[(int)Math.Clamp(Math.Round(0.9 * (ordered.Length - 1)), 0, ordered.Length - 1)];
        return (ok, flips, sigma, p90, items.Count);
    }

    private static (int Ok, int Flips, double Sigma, double P90, int N) EvalE(
        List<CachedSeg> items, JlNCCModel model, double minScore, List<string> rows)
    {
        var options = JlFindOptions.ProductDefault;
        var tag = $"E_min{minScore:0.00}";
        var angles = new List<double>();
        var times = new List<double>();
        var ok = 0;
        var flips = 0;
        foreach (var item in items)
        {
            var sw = Stopwatch.StartNew();
            JlRefineHit hit;
            try
            {
                hit = JlNccRefine.TryRefine(item.Scene, item.Points, model, item.Range, 0.3, options);
            }
            catch (Exception ex)
            {
                hit = JlRefineHit.Miss(ex.Message);
            }

            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            var gated = hit.Found && hit.Score >= minScore;
            var flip = gated && Math.Abs(AngleGeometry.NormalizeSignedDeg(hit.AngleDeg)) > 150;
            if (gated)
            {
                ok++;
                angles.Add(hit.AngleDeg);
            }

            if (flip)
                flips++;

            rows.Add(string.Join(',',
                tag,
                item.File,
                gated ? "yes" : "no",
                F(hit.AngleDeg),
                F(hit.Score),
                sw.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture),
                '"' + hit.Note.Replace("\"", "'", StringComparison.Ordinal) + '"',
                F(item.CoarseDeg),
                flip ? "yes" : "no"));
        }

        var sigma = AngleGeometry.CircularStdDeg(angles, 360);
        var ordered = times.OrderBy(x => x).ToArray();
        var p90 = ordered.Length == 0
            ? 0
            : ordered[(int)Math.Clamp(Math.Round(0.9 * (ordered.Length - 1)), 0, ordered.Length - 1)];
        return (ok, flips, sigma, p90, items.Count);
    }

    private static CachedSeg? SegmentOne(string path, RecipeConfig recipe, ModelManager models)
    {
        const int minArea = 400;
        using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
        if (mat.Empty())
            return null;

        using var vision = VisionImageCv.FromMat(mat, ownsMat: false);
        using var roiImage = RoiHelper.CropToVisionImage(vision, recipe.Roi, out var ox, out var oy);
        var input = roiImage ?? vision;
        using var full = VisionImageCv.AsMat(vision);
        Mat? roiOwned = recipe.Roi is null ? null : RoiHelper.Crop(full, recipe.Roi, out _, out _);
        var roiView = roiOwned ?? full.Clone();
        if (roiOwned is not null)
        {
            roiView = roiOwned;
            roiOwned = null;
        }

        try
        {
            var session = models.Open(recipe.Models[0], RobotVision.Core.Inference.InferenceTask.Segmentation);
            var segs = session.Run(y =>
                y.RunSegmentation(input, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou));
            foreach (var seg in segs)
            {
                var box = seg.Box;
                if ((double)box.Width * box.Height < minArea || seg.ContourLocal.Count < 4)
                    continue;
                var pts = new Point2f[seg.ContourLocal.Count];
                for (var i = 0; i < seg.ContourLocal.Count; i++)
                    pts[i] = new Point2f((float)(seg.ContourLocal[i].X + box.Left), (float)(seg.ContourLocal[i].Y + box.Top));
                var housing = MaskHousing.Fit(pts);
                var range = MaskHousing.AdaptiveRefineRange(recipe.Template.RefineRangeDeg, housing);
                return new CachedSeg(
                    Path.GetFileName(path),
                    roiView,
                    JlImageConvert.FromGrayMat(roiView),
                    pts,
                    range,
                    housing.WarpAngleDeg);
            }

            roiView.Dispose();
            return null;
        }
        catch
        {
            roiView.Dispose();
            throw;
        }
        finally
        {
            roiOwned?.Dispose();
        }
    }

    private static string F(double v) =>
        double.IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private readonly record struct BConfig(double MinScore, double Greediness, int NumLevels, bool PreferUpright);

    private sealed class CachedSeg(
        string file, Mat roiView, JlImage scene, Point2f[] points, double range, double coarseDeg) : IDisposable
    {
        public string File { get; } = file;
        public JlImage Scene { get; } = scene;
        public Point2f[] Points { get; } = points;
        public double Range { get; } = range;
        public double CoarseDeg { get; } = coarseDeg;
        private readonly Mat _roiView = roiView;

        public void Dispose()
        {
            Scene.Dispose();
            _roiView.Dispose();
        }
    }
}
