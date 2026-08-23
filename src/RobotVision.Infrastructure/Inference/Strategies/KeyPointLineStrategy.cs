using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式三：关键点模型，单模型两个关键点（kpt_shape=[2,2]）的连线。
/// 一个模型同时输出位置与方向，精度与消歧兼得。
/// 注意：YoloDotNet 的 KeyPoint 坐标为整型像素（无子像素），角度精度依赖较长基线；
/// 基线过短（&lt; <see cref="MinBaselinePx"/> px）时角度不可信，该目标直接跳过。
/// </summary>
public sealed class KeyPointLineStrategy(ModelManager models) : IAngleStrategy
{
    /// <summary>两点连线的最小基线（px）：整型关键点角度量化误差 ~1/基线，过短时角度几乎随机。</summary>
    private const int MinBaselinePx = 3;

    public List<PixelPose> Compute(Mat undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        using var bitmap = RoiHelper.ToBitmap(undistorted, recipe.Roi, out var ox, out var oy);
        var session = models.Open(recipe.Models[0], InferenceTask.PoseEstimation);
        var results = session.Run(y => y.RunPoseEstimation(bitmap, recipe.Confidence, recipe.Iou), ct);

        var poses = new List<PixelPose>();
        foreach (var estimation in results)
        {
            // KeyPoints 可能为 null（模型未输出关键点/输出异常），防御性跳过
            var keypoints = estimation.KeyPoints;
            if (keypoints is null || keypoints.Length <= Math.Max(recipe.KeypointIndexA, recipe.KeypointIndexB))
                continue;

            var keyA = keypoints[recipe.KeypointIndexA];
            var keyB = keypoints[recipe.KeypointIndexB];
            if (keyA.Confidence < recipe.KeypointMinConfidence ||
                keyB.Confidence < recipe.KeypointMinConfidence)
                continue;

            var dx = keyB.X - keyA.X;
            var dy = keyB.Y - keyA.Y;
            if (dx * dx + dy * dy < MinBaselinePx * MinBaselinePx)
                continue;

            // 关键点坐标为 ROI 内坐标，偏移回全图坐标系后再计算
            var (center, angleDeg) = AngleGeometry.FromTwoPoints(
                new Point2d(keyA.X + ox, keyA.Y + oy), new Point2d(keyB.X + ox, keyB.Y + oy));
            poses.Add(new PixelPose(center.X, center.Y, angleDeg, estimation.Confidence));
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }
}
