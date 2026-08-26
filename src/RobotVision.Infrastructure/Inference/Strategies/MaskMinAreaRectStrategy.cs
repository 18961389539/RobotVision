using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Geometry;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式一：单分割模型 → 掩码轮廓最小外接矩形的长边方向。
/// </summary>
public sealed class MaskMinAreaRectStrategy(ModelManager models) : IAngleStrategy
{
    private const double MinMaskAreaPx = 400;

    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        using var roiOwned = RoiHelper.CropToVisionImage(undistorted, recipe.Roi, out var ox, out var oy);
        var input = roiOwned ?? undistorted;
        var session = models.Open(recipe.Models[0], InferenceTask.Segmentation);
        var results = session.Run(y =>
            y.RunSegmentation(input, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou), ct);

        var poses = new List<PixelPose>();
        foreach (var segmentation in results)
        {
            var contour = segmentation.ContourLocal;
            if (contour.Count < 4)
                continue;

            var box = segmentation.Box;
            if ((double)box.Width * box.Height < MinMaskAreaPx)
                continue;

            var points = new Point2f[contour.Count];
            for (var i = 0; i < contour.Count; i++)
                points[i] = new Point2f((float)(contour[i].X + box.Left), (float)(contour[i].Y + box.Top));

            var (center, angleDeg) = MinAreaRectGeometry.LongAxis(points);
            poses.Add(new PixelPose(center.X + ox, center.Y + oy, angleDeg, segmentation.Confidence)
            {
                Overlay = new PoseOverlay
                {
                    Contour = points.Select(p => new PixelPoint(p.X + ox, p.Y + oy)).ToArray(),
                    Boxes = [new PixelRect(box.Left + ox, box.Top + oy, box.Width, box.Height)],
                    Label = segmentation.Label,
                },
            });
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }
}
