using System.Globalization;
using System.Text;
using OpenCvSharp;
using RobotVision.Core.Inference;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using RobotVision.Vision;

namespace RobotVision.OsfpJlvisionBakeoff;

/// <summary>全精度 Chamfer 对照（基线 CSV 曾把坐标写成整数）。</summary>
internal static class ChamferFullPrec
{
    private const int MinMaskAreaPx = 400;

    public static int Run(
        string repo,
        string captureDir,
        string recipeDir,
        string modelsDir,
        IReadOnlyList<(string File, string Split)> items)
    {
        var recipe = new RecipeLoader(recipeDir).Get("Product");
        using var models = new ModelManager(modelsDir);
        var rvShape = MaskShapeMatch.GetOrCreate(recipe);
        var rows = new List<string> { "file,split,usable,cx,cy,angle_deg,score,note" };
        try
        {
            foreach (var (name, split) in items)
            {
                Console.WriteLine($"-- {name}");
                var path = Path.Combine(captureDir, name);
                using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
                if (mat.Empty())
                {
                    rows.Add($"{name},{split},no,,,,decode_fail");
                    continue;
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
                    Point2f[]? points = null;
                    foreach (var seg in segs)
                    {
                        var box = seg.Box;
                        if ((double)box.Width * box.Height < MinMaskAreaPx || seg.ContourLocal.Count < 4)
                            continue;
                        var pts = new Point2f[seg.ContourLocal.Count];
                        for (var i = 0; i < seg.ContourLocal.Count; i++)
                            pts[i] = new Point2f(
                                (float)(seg.ContourLocal[i].X + box.Left),
                                (float)(seg.ContourLocal[i].Y + box.Top));
                        points = pts;
                        break;
                    }

                    if (points is null)
                    {
                        rows.Add($"{name},{split},no,,,,no_segment");
                        continue;
                    }

                    var housing = MaskHousing.Fit(points);
                    var range = MaskHousing.AdaptiveRefineRange(recipe.Template.RefineRangeDeg, housing);
                    var attempt = MaskShapeMatch.TryRefine(
                        roiView, points, rvShape, range,
                        noFlip: recipe.Template.NoFlipConstraint,
                        options: ShapeMatchOptions.From(recipe.Template));
                    if (attempt.Pose is not { } p || p.Score < recipe.Template.MatchThreshold)
                    {
                        rows.Add($"{name},{split},no,,,,miss");
                        continue;
                    }

                    var cx = (p.Center.X + ox).ToString("0.###", CultureInfo.InvariantCulture);
                    var cy = (p.Center.Y + oy).ToString("0.###", CultureInfo.InvariantCulture);
                    var ang = p.AngleDeg.ToString("0.###", CultureInfo.InvariantCulture);
                    var sc = p.Score.ToString("0.###", CultureInfo.InvariantCulture);
                    rows.Add($"{name},{split},yes,{cx},{cy},{ang},{sc},ok");
                }
                finally
                {
                    roiOwned?.Dispose();
                }
            }
        }
        finally
        {
            MaskShapeMatch.Release(rvShape);
        }

        var outPath = Path.Combine(repo, "benchmarks", "osfp-jlvision", "chamfer_fullprec.csv");
        File.WriteAllLines(outPath, rows, Encoding.UTF8);
        Console.WriteLine($"wrote {outPath} usable={rows.Count(r => r.Contains(",yes,", StringComparison.Ordinal))}");
        return 0;
    }
}
