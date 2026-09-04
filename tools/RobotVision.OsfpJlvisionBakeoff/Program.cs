using System.Diagnostics;
using System.Globalization;
using System.Text;
using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Inference;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using RobotVision.JlVision;
using RobotVision.Vision;

namespace RobotVision.OsfpJlvisionBakeoff;

internal static class Program
{
    private const int MinMaskAreaPx = 400;

    public static int Main(string[] args)
    {
        var repo = FindRepoRoot() ?? @"E:\RobotVision";
        var captureDir = EnvOr("FIELD_CAPTURE_DIR", @"E:\RobotVisionData\RobotVisionData\captures\2026-08-28");
        var recipeDir = EnvOr("ROBOTVISION_RECIPES", @"E:\RobotVisionData\RobotVisionData\recipes");
        var modelsDir = EnvOr("ROBOTVISION_MODELS", Path.Combine(repo, "models"));
        var manifestPath = Path.Combine(repo, "benchmarks", "osfp-jlvision", "dataset_manifest.csv");
        var outPath = Path.Combine(repo, "benchmarks", "osfp-jlvision", "jlvision_bakeoff.csv");
        var split = args.Length > 0 ? args[0] : "dev";

        Console.WriteLine($"repo={repo}");
        Console.WriteLine($"captures={captureDir}");
        Console.WriteLine($"recipes={recipeDir}");
        Console.WriteLine($"models={modelsDir}");
        Console.WriteLine($"split={split}");

        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"缺少 {manifestPath}");
            return 2;
        }

        if (split.Equals("grid", StringComparison.OrdinalIgnoreCase))
        {
            var dev = LoadSplit(manifestPath, "dev");
            if (dev.Count == 0)
            {
                Console.Error.WriteLine("manifest 中没有 split=dev 的行");
                return 2;
            }

            return Phase2Grid.Run(repo, captureDir, recipeDir, modelsDir, dev);
        }

        if (split.Equals("pipeline", StringComparison.OrdinalIgnoreCase))
        {
            var devItems = LoadSplitRows(manifestPath, "dev");
            return PipelineEval.Run(repo, captureDir, recipeDir, modelsDir, devItems, "jlvision_p3_dev_pipeline.csv");
        }

        if (split.Equals("p4", StringComparison.OrdinalIgnoreCase) ||
            split.Equals("holdout", StringComparison.OrdinalIgnoreCase))
        {
            var items = split.Equals("p4", StringComparison.OrdinalIgnoreCase)
                ? LoadSplitRows(manifestPath, split: null)
                : LoadSplitRows(manifestPath, "holdout");
            var name = split.Equals("p4", StringComparison.OrdinalIgnoreCase)
                ? "jlvision_p4_all.csv"
                : "jlvision_p4_holdout.csv";
            return PipelineEval.Run(repo, captureDir, recipeDir, modelsDir, items, name);
        }

        if (split.Equals("chamfer", StringComparison.OrdinalIgnoreCase))
        {
            var items = LoadSplitRows(manifestPath, split: null);
            return ChamferFullPrec.Run(repo, captureDir, recipeDir, modelsDir, items);
        }

        var files = LoadSplit(manifestPath, split);
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"manifest 中没有 split={split} 的行");
            return 2;
        }

        var recipe = new RecipeLoader(recipeDir).Get("Product");
        using var models = new ModelManager(modelsDir);
        using var template = MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
        using var templateGray = JlImageConvert.ToGray(template);

        JlShapeModel? shapeModel = null;
        JlNCCModel? nccModel = null;
        try
        {
            try
            {
                shapeModel = JlShapeRefine.CreateModel(templateGray);
                Console.WriteLine("JlShapeModel 训练完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JlShapeModel 训练失败: {ex.Message}");
            }

            try
            {
                nccModel = JlNccRefine.CreateModel(templateGray);
                Console.WriteLine("JlNCCModel 训练完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JlNCCModel 训练失败: {ex.Message}");
            }

            var rvShape = MaskShapeMatch.GetOrCreate(recipe);
            var rows = new List<Row>();
            foreach (var name in files)
            {
                var path = Path.Combine(captureDir, name);
                Console.WriteLine($"-- {name}");
                rows.AddRange(RunFile(path, recipe, models, rvShape, shapeModel, nccModel));
            }

            MaskShapeMatch.Release(rvShape);
            WriteCsv(outPath, rows);
            PrintSummary(rows, split);
            Console.WriteLine($"wrote {outPath}");
            return 0;
        }
        finally
        {
            shapeModel?.Dispose();
            nccModel?.Dispose();
        }
    }

    private static List<Row> RunFile(
        string path,
        RecipeConfig recipe,
        ModelManager models,
        MaskShapeMatch.ShapeModel? rvShape,
        JlShapeModel? jlShape,
        JlNCCModel? jlNcc)
    {
        var rows = new List<Row>();
        var file = Path.GetFileName(path);
        using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
        if (mat.Empty())
        {
            rows.Add(Row.Skip(file, "decode_fail"));
            return rows;
        }

        using var vision = VisionImageCv.FromMat(mat, ownsMat: false);
        using var roiImage = RoiHelper.CropToVisionImage(vision, recipe.Roi, out var ox, out var oy);
        var input = roiImage ?? vision;
        using var full = VisionImageCv.AsMat(vision);
        Mat? roiOwned = recipe.Roi is null ? null : RoiHelper.Crop(full, recipe.Roi, out _, out _);
        var roiView = roiOwned ?? full;
        try
        {
            var session = models.Open(recipe.Models[0], InferenceTask.Segmentation);
            var segs = session.Run(y =>
                y.RunSegmentation(input, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou));
            InstanceSegmentation? pick = null;
            Point2f[]? points = null;
            foreach (var seg in segs)
            {
                var box = seg.Box;
                if ((double)box.Width * box.Height < MinMaskAreaPx || seg.ContourLocal.Count < 4)
                    continue;
                var pts = new Point2f[seg.ContourLocal.Count];
                for (var i = 0; i < seg.ContourLocal.Count; i++)
                    pts[i] = new Point2f((float)(seg.ContourLocal[i].X + box.Left), (float)(seg.ContourLocal[i].Y + box.Top));
                pick = seg;
                points = pts;
                break;
            }

            if (pick is null || points is null)
            {
                rows.Add(Row.Skip(file, "no_segment"));
                return rows;
            }

            var housing = MaskHousing.Fit(points);
            var range = MaskHousing.AdaptiveRefineRange(recipe.Template.RefineRangeDeg, housing);
            using var scene = JlImageConvert.FromGrayMat(roiView);

            rows.Add(Time("A_ShapeMatch", file, () => BaselineShape(roiView, points, rvShape, recipe, range)));
            rows.Add(Time("B_JlShape", file, () =>
                jlShape is null
                    ? JlRefineHit.Miss("模型未训练")
                    : JlShapeRefine.TryRefine(scene, points, jlShape, range, 0.3, JlFindOptions.ProductDefault)));
            rows.Add(Time("C_JlMetrology", file, () => JlMetrologyRefine.TryRefine(scene, points)));
            rows.Add(Time("D_JlMeasure", file, () =>
                JlMeasureRefine.TryRefine(scene, points, recipe.Template.HousingEdgePolarity)));
            rows.Add(Time("E_JlNcc", file, () =>
                jlNcc is null
                    ? JlRefineHit.Miss("模型未训练")
                    : JlNccRefine.TryRefine(scene, points, jlNcc, range, 0.3, JlFindOptions.ProductDefault)));

            foreach (var row in rows.Where(r => r.File == file && r.Found))
            {
                row.Cx += ox;
                row.Cy += oy;
            }

            return rows;
        }
        finally
        {
            roiOwned?.Dispose();
        }
    }

    private static JlRefineHit BaselineShape(
        Mat roi, Point2f[] contour, MaskShapeMatch.ShapeModel? model, RecipeConfig recipe, double range)
    {
        var attempt = MaskShapeMatch.TryRefine(
            roi, contour, model, range,
            noFlip: recipe.Template.NoFlipConstraint,
            options: ShapeMatchOptions.From(recipe.Template));
        if (attempt.Pose is not { } p)
            return JlRefineHit.Miss("ShapeMatch miss");
        var gated = p.Score >= recipe.Template.MatchThreshold;
        return new JlRefineHit(gated, p.Center.X, p.Center.Y, p.AngleDeg, p.Score,
            gated ? $"score={p.Score:0.00}" : $"below gate {p.Score:0.00}<{recipe.Template.MatchThreshold:0.00}");
    }

    private static Row Time(string method, string file, Func<JlRefineHit> work)
    {
        var sw = Stopwatch.StartNew();
        JlRefineHit hit;
        try
        {
            hit = work();
        }
        catch (Exception ex)
        {
            hit = JlRefineHit.Miss(ex.GetType().Name + ": " + ex.Message);
        }

        sw.Stop();
        return new Row
        {
            File = file,
            Method = method,
            Found = hit.Found,
            Cx = hit.Cx,
            Cy = hit.Cy,
            AngleDeg = hit.AngleDeg,
            Score = hit.Score,
            Ms = sw.Elapsed.TotalMilliseconds,
            Note = hit.Note.Replace('\n', ' ').Replace(',', ';'),
        };
    }

    private static void WriteCsv(string path, List<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("file,method,found,cx,cy,angle_deg,score,ms,note");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                r.File,
                r.Method,
                r.Found ? "yes" : "no",
                F(r.Cx),
                F(r.Cy),
                F(r.AngleDeg),
                F(r.Score),
                r.Ms.ToString("0.0", CultureInfo.InvariantCulture),
                '"' + r.Note.Replace("\"", "'", StringComparison.Ordinal) + '"'));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void PrintSummary(List<Row> rows, string split)
    {
        Console.WriteLine($"=== Phase 1 {split} summary ===");
        var files = rows.Select(r => r.File).Distinct().ToArray();
        var n = files.Length;
        Row? BaseOf(string file) => rows.FirstOrDefault(r => r.File == file && r.Method == "A_ShapeMatch");
        foreach (var method in new[] { "A_ShapeMatch", "B_JlShape", "C_JlMetrology", "D_JlMeasure", "E_JlNcc" })
        {
            var subset = rows.Where(r => r.Method == method).ToList();
            var ok = subset.Count(r => r.Found);
            var ms = subset.Select(r => r.Ms).OrderBy(x => x).ToArray();
            var p90 = ms.Length == 0 ? 0 : ms[(int)Math.Clamp(Math.Round(0.9 * (ms.Length - 1)), 0, ms.Length - 1)];
            var angles = subset.Where(r => r.Found && double.IsFinite(r.AngleDeg)).Select(r => r.AngleDeg).ToList();
            var sigma = AngleGeometry.CircularStdDeg(angles, 360);
            var flips = 0;
            foreach (var file in files)
            {
                var b = BaseOf(file);
                var m = subset.FirstOrDefault(r => r.File == file);
                if (b is { Found: true } && m is { Found: true }
                    && AngleGeometry.UndirectedDeltaDeg(b.AngleDeg, m.AngleDeg) < 2.0
                    && Math.Abs(AngleGeometry.NormalizeSignedDeg(m.AngleDeg - b.AngleDeg)) > 150)
                    flips++;
            }

            Console.WriteLine(
                $"{method}: found {ok}/{n} ({100.0 * ok / Math.Max(1, n):0.0}%)  flip_vs_A={flips}  σ={sigma:0.00}°  P90={p90:0.0}ms");
        }
    }

    private static List<(string File, string Split)> LoadSplitRows(string manifest, string? split)
    {
        var list = new List<(string File, string Split)>();
        foreach (var line in File.ReadAllLines(manifest).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var parts = line.Split(',');
            if (parts.Length < 2)
                continue;
            if (split is not null && !parts[1].Equals(split, StringComparison.OrdinalIgnoreCase))
                continue;
            list.Add((parts[0], parts[1]));
        }

        return list;
    }

    private static List<string> LoadSplit(string manifest, string split)
    {
        var list = new List<string>();
        foreach (var line in File.ReadAllLines(manifest).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var parts = line.Split(',');
            if (parts.Length < 2)
                continue;
            if (parts[1].Equals(split, StringComparison.OrdinalIgnoreCase))
                list.Add(parts[0]);
        }

        return list;
    }

    private static string F(double v) =>
        double.IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private static string EnvOr(string key, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "RobotVision.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        return Directory.Exists(@"E:\RobotVision") ? @"E:\RobotVision" : null;
    }

    private sealed class Row
    {
        public required string File { get; init; }
        public required string Method { get; init; }
        public bool Found { get; init; }
        public double Cx { get; set; }
        public double Cy { get; set; }
        public double AngleDeg { get; init; }
        public double Score { get; init; }
        public double Ms { get; init; }
        public string Note { get; init; } = "";

        public static Row Skip(string file, string note) => new()
        {
            File = file,
            Method = "segment",
            Found = false,
            Cx = double.NaN,
            Cy = double.NaN,
            AngleDeg = double.NaN,
            Note = note,
        };
    }
}
