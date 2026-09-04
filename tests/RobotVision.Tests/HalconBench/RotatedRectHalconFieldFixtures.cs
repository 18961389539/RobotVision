using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Vision;

namespace RobotVision.Tests.HalconBench;

/// <summary>现场留存图 HALCON 夹具（无合成真值，供真机 side-by-side）。</summary>
internal static class RotatedRectHalconFieldFixtures
{
    public static IReadOnlyList<RotatedRectHalconFixture> TryLoad(int maxCount = 8)
    {
        var captureDir = ResolveCaptureDir();
        if (captureDir is null)
            return [];

        var recipe = TryLoadProductRecipe(out var template);
        if (template is null)
            return [];

        var teachArea = template.TeachAreaPx > 1 ? template.TeachAreaPx : 285_172.0;
        var teachAspect = template.TeachAspect > 1e-3 ? template.TeachAspect : 2.14;
        var options = RectFitOptions.ForLineFit(template);

        var files = Directory.GetFiles(captureDir, "*_Product_OK.png", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .Take(maxCount)
            .ToArray();
        if (files.Length == 0)
            return [];

        var list = new List<RotatedRectHalconFixture>();
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = $"field_{SanitizeId(name)}";
            var bytes = File.ReadAllBytes(file);
            list.Add(new(
                id,
                "field_product",
                double.NaN,
                0,
                options,
                () => DecodeGray(bytes),
                () => ExtractContour(DecodeGray(bytes), teachArea, teachAspect)));
        }

        return list;
    }

    public static void ExportFieldSubset(string benchRoot, int maxCount = 8)
    {
        var fixtures = TryLoad(maxCount);
        if (fixtures.Count == 0)
            return;

        var dir = Path.Combine(RotatedRectHalconBenchIo.FixturesDir(benchRoot), "field");
        Directory.CreateDirectory(dir);
        var manifest = new List<object>();
        foreach (var fx in fixtures)
        {
            using var image = fx.CreateImage();
            var contour = fx.CreateContour();
            if (contour.Length < 16)
                continue;

            var housing = MaskHousing.Fit(contour);
            var seed = housing.LongAxisDeg;
            Cv2.ImWrite(Path.Combine(dir, $"{fx.Id}.png"), image);
            WriteContour(Path.Combine(dir, $"{fx.Id}.contour.csv"), contour);
            manifest.Add(new
            {
                id = fx.Id,
                scenario = fx.Scenario,
                seed_deg = seed,
                contour_points = contour.Length,
                image = $"{fx.Id}.png",
                contour = $"{fx.Id}.contour.csv",
            });
        }

        if (manifest.Count == 0)
            return;

        var json = System.Text.Json.JsonSerializer.Serialize(manifest,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, "manifest.json"), json);
    }

    private static string? ResolveCaptureDir()
    {
        var fromEnv = Environment.GetEnvironmentVariable("FIELD_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return fromEnv;

        var dataRoot = TestBuildPaths.ResolveRobotVisionDataRoot();
        if (dataRoot is null)
            return null;

        var captures = Path.Combine(dataRoot, "captures");
        if (!Directory.Exists(captures))
            return null;

        return Directory.GetDirectories(captures)
            .Select(d => new DirectoryInfo(d))
            .Where(d => Directory.GetFiles(d.FullName, "*_Product_OK.png").Length > 0)
            .OrderByDescending(d => d.Name, StringComparer.Ordinal)
            .Select(d => d.FullName)
            .FirstOrDefault();
    }

    private static TemplateOptions? TryLoadProductRecipe(out TemplateOptions template)
    {
        template = new TemplateOptions();
        foreach (var candidate in new[]
                 {
                     TestBuildPaths.ResolveRobotVisionDataRecipesDir(),
                     TestBuildPaths.ResolveRecipesDir(),
                 })
        {
            if (candidate is null || !File.Exists(Path.Combine(candidate, "Product.json")))
                continue;
            var recipe = new RecipeLoader(candidate).Get("Product");
            template = recipe.Template;
            return template;
        }
        return null;
    }

    private static Mat DecodeGray(byte[] bytes)
    {
        using var color = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (color.Empty())
            return new Mat();
        var gray = new Mat();
        Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static Point2f[] ExtractContour(Mat gray, double teachArea, double teachAspect)
    {
        try
        {
            return FieldCaptureRefineBenchTests.ExtractConnectorContour(gray, teachArea, teachAspect);
        }
        finally
        {
            gray.Dispose();
        }
    }

    private static string SanitizeId(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray();
        return new string(chars);
    }

    private static void WriteContour(string path, IReadOnlyList<Point2f> contour)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("x,y");
        foreach (var p in contour)
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.###},{1:0.###}", p.X, p.Y));
        File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
    }
}
