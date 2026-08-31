using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式三：关键点模型，单模型两个关键点的连线。
/// </summary>
public sealed class KeyPointLineStrategy(ModelManager models) : IAngleStrategy
{
    private const int MinBaselinePx = 3;

    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        using var roiOwned = RoiHelper.CropToVisionImage(undistorted, recipe.Roi, out var ox, out var oy);
        var input = roiOwned ?? undistorted;
        var session = models.Open(recipe.Models[0], InferenceTask.PoseEstimation);
        var results = session.Run(y => y.RunPoseEstimation(input, recipe.Confidence, recipe.Iou), ct);

        var poses = new List<PixelPose>();
        foreach (var estimation in results)
        {
            var keypoints = estimation.KeyPoints;
            if (keypoints.Count <= Math.Max(recipe.Keypoint.IndexA, recipe.Keypoint.IndexB))
                continue;

            var keyA = keypoints[recipe.Keypoint.IndexA];
            var keyB = keypoints[recipe.Keypoint.IndexB];
            if (keyA.Confidence < recipe.Keypoint.MinConfidence ||
                keyB.Confidence < recipe.Keypoint.MinConfidence)
                continue;

            var dx = keyB.X - keyA.X;
            var dy = keyB.Y - keyA.Y;
            if (dx * dx + dy * dy < MinBaselinePx * MinBaselinePx)
                continue;

            var (center, angleDeg) = AngleGeometry.FromTwoPoints(
                keyA.X + ox, keyA.Y + oy, keyB.X + ox, keyB.Y + oy);
            var box = estimation.Box;
            poses.Add(new PixelPose(center.X, center.Y, angleDeg, estimation.Confidence)
            {
                Overlay = new PoseOverlay
                {
                    Boxes = [new PixelRect(box.Left + ox, box.Top + oy, box.Width, box.Height)],
                    KeyPoints = keypoints.Select(k => new PixelPoint(k.X + ox, k.Y + oy)).ToArray(),
                    KeyPointConfidences = keypoints.Select(k => k.Confidence).ToArray(),
                    Label = estimation.Label,
                },
            });
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }
}
