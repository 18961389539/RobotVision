using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using YoloDotNet.Extensions;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式一：单分割模型 → 掩码轮廓最小外接矩形的长边方向。
/// 角度范围 [0,180)，存在 180° 歧义（矩形分不清头尾），需要非对称特征二次判向。
/// 极小掩码（包围盒面积 &lt; <see cref="MinMaskAreaPx"/> px²）的最小外接矩形角度对噪声
/// 敏感，直接跳过。
/// </summary>
public sealed class MaskMinAreaRectStrategy(ModelManager models) : IAngleStrategy
{
    /// <summary>掩码包围盒最小面积（px²）：过小的掩码角度几乎随机，视为噪声。</summary>
    private const double MinMaskAreaPx = 400;

    public List<PixelPose> Compute(Mat undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        using var bitmap = RoiHelper.ToBitmap(undistorted, recipe.Roi, out var ox, out var oy);
        var session = models.Open(recipe.Models[0], InferenceTask.Segmentation);
        var results = session.Run(y =>
            y.RunSegmentation(bitmap, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou), ct);

        var poses = new List<PixelPose>();
        foreach (var segmentation in results)
        {
            // GetContourPoints 返回的是相对 BoundingBox 的局部坐标，需偏移回图像坐标
            var contour = segmentation.GetContourPoints();
            if (contour.Length < 4)
                continue;

            var box = segmentation.BoundingBox;
            if ((double)box.Width * box.Height < MinMaskAreaPx)
                continue;

            var points = new Point2f[contour.Length];
            for (var i = 0; i < contour.Length; i++)
                points[i] = new Point2f(contour[i].X + box.Left, contour[i].Y + box.Top);

            var (center, angleDeg) = AngleGeometry.LongAxisFromMinAreaRect(points);
            // 中心坐标为 ROI 内坐标，偏移回全图坐标系
            poses.Add(new PixelPose(center.X + ox, center.Y + oy, angleDeg, segmentation.Confidence));
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }
}
