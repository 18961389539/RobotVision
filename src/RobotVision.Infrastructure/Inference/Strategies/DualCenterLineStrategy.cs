using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using SkiaSharp;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式二：双检测模型，A、B 两个特征中心的连线。
/// A→B 的有序性天然消除 180° 歧义；基线越长角度越稳（σθ ≈ √2·σpx / L）。
/// 任一模型漏检时该目标不输出（不猜测配对）。
/// 注意：配对为贪心最近邻（按 A 顺序抢占最近的未用 B），多目标时可能非全局最优，
/// 建议单目标或目标间距远大于 PairingMaxDistancePx 的场景使用。
/// </summary>
public sealed class DualCenterLineStrategy(ModelManager models) : IAngleStrategy
{
    public List<PixelPose> Compute(Mat undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        if (recipe.Models.Count < 2)
            throw new VisionException(VisionErrorCode.InternalError, "双模型连线模式需要配置 2 个模型");

        using var bitmap = RoiHelper.ToBitmap(undistorted, recipe.Roi, out var ox, out var oy);
        var sessionA = models.Open(recipe.Models[0], InferenceTask.ObjectDetection);
        var sessionB = models.Open(recipe.Models[1], InferenceTask.ObjectDetection);

        var listA = sessionA.Run(y => y.RunObjectDetection(bitmap, recipe.Confidence, recipe.Iou), ct);
        var listB = sessionB.Run(y => y.RunObjectDetection(bitmap, recipe.Confidence, recipe.Iou), ct);
        if (listA.Count == 0 || listB.Count == 0)
            return [];

        var usedB = new HashSet<int>();
        var poses = new List<PixelPose>();

        foreach (var a in listA)
        {
            var centerA = Center(a.BoundingBox);

            var bestIndex = -1;
            double bestDistance = double.MaxValue;
            for (var i = 0; i < listB.Count; i++)
            {
                if (usedB.Contains(i))
                    continue;
                var distance = Distance(centerA, Center(listB[i].BoundingBox));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDistance > recipe.PairingMaxDistancePx)
                continue;

            usedB.Add(bestIndex);
            var centerB = Center(listB[bestIndex].BoundingBox);
            // 中心坐标为 ROI 内坐标，偏移回全图坐标系
            var (center, angleDeg) = AngleGeometry.FromTwoPoints(
                new Point2d(centerA.X + ox, centerA.Y + oy),
                new Point2d(centerB.X + ox, centerB.Y + oy));
            poses.Add(new PixelPose(
                center.X, center.Y, angleDeg,
                Math.Min(a.Confidence, listB[bestIndex].Confidence)));
        }

        // 与 KeyPoint/Mask 策略一致：按置信度降序，保证 TCP 应答位姿顺序稳定
        return poses.OrderByDescending(p => p.Score).ToList();
    }

    private static Point2d Center(SKRectI box) =>
        new((box.Left + box.Right) / 2.0, (box.Top + box.Bottom) / 2.0);

    private static double Distance(Point2d a, Point2d b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
