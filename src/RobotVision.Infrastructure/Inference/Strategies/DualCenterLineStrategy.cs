using System.Diagnostics.CodeAnalysis;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式二：双检测模型，A、B 两个特征中心的连线。
/// </summary>
public sealed class DualCenterLineStrategy(ModelManager models) : IAngleStrategy
{
    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        if (recipe.Models.Count < 2)
            throw new VisionException(VisionErrorCode.InternalError, "双模型连线模式需要配置 2 个模型");

        using var roiOwned = RoiHelper.CropToVisionImage(undistorted, recipe.Roi, out var ox, out var oy);
        var input = roiOwned ?? undistorted;
        var sessionA = models.Open(recipe.Models[0], InferenceTask.ObjectDetection);
        var sessionB = models.Open(recipe.Models[1], InferenceTask.ObjectDetection);

        var listA = sessionA.Run(y => y.RunObjectDetection(input, recipe.Confidence, recipe.Iou), ct);
        if (listA.Count == 0)
            return [];

        return recipe.DualModel.CropWindowPairing
            ? PairByWindow(undistorted, recipe, sessionB, listA, ox, oy, ct)
            : PairGlobal(sessionB, input, recipe, listA, ox, oy, ct);
    }

    private static List<PixelPose> PairGlobal(
        ModelSession sessionB, VisionImage bitmap, RecipeConfig recipe,
        IReadOnlyList<ObjectDetectionResult> listA, double ox, double oy, CancellationToken ct)
    {
        var listB = sessionB.Run(y => y.RunObjectDetection(bitmap, recipe.Confidence, recipe.Iou), ct);
        if (listB.Count == 0)
            return [];

        var usedB = new HashSet<int>();
        var poses = new List<PixelPose>();

        foreach (var a in listA)
        {
            var centerA = Center(a.Box);

            var bestIndex = -1;
            double bestDistance = double.MaxValue;
            for (var i = 0; i < listB.Count; i++)
            {
                if (usedB.Contains(i))
                    continue;
                var distance = Distance(centerA, Center(listB[i].Box));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDistance > recipe.DualModel.PairingMaxDistancePx)
                continue;

            usedB.Add(bestIndex);
            var boxB = listB[bestIndex].Box;
            var centerB = Center(boxB);
            var (center, angleDeg) = AngleGeometry.FromTwoPoints(
                centerA.X + ox, centerA.Y + oy, centerB.X + ox, centerB.Y + oy);
            poses.Add(new PixelPose(
                center.X, center.Y, angleDeg,
                Math.Min(a.Confidence, listB[bestIndex].Confidence))
            {
                Overlay = new PoseOverlay
                {
                    Boxes =
                    [
                        new PixelRect(a.Box.Left + ox, a.Box.Top + oy, a.Box.Width, a.Box.Height),
                        new PixelRect(boxB.Left + ox, boxB.Top + oy, boxB.Width, boxB.Height),
                    ],
                    Baseline =
                    [
                        new PixelPoint(centerA.X + ox, centerA.Y + oy),
                        new PixelPoint(centerB.X + ox, centerB.Y + oy),
                    ],
                    Label = a.Label,
                },
            });
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Mat ROI view disposed in finally after inference window.")]
    private static List<PixelPose> PairByWindow(
        VisionImage undistorted, RecipeConfig recipe, ModelSession sessionB,
        IReadOnlyList<ObjectDetectionResult> listA, double ox, double oy, CancellationToken ct)
    {
        using var full = VisionImageCv.AsMat(undistorted);
        using var roiOwned = recipe.Roi is null ? null : RoiHelper.Crop(full, recipe.Roi, out _, out _);
        var roiView = roiOwned ?? full;

        var poses = new List<PixelPose>();
        foreach (var a in listA)
        {
            ct.ThrowIfCancellationRequested();
            var window = ExpandWindow(a.Box, recipe.DualModel.CropExpandRatio, roiView.Width, roiView.Height);

            Mat? windowView = null;
            try
            {
                windowView = new Mat(roiView, new Rect(window.Left, window.Top, window.Width, window.Height));
                using var windowImage = VisionImageCv.FromMat(windowView, ownsMat: false);
                var listB = sessionB.Run(
                    y => y.RunObjectDetection(windowImage, recipe.Confidence, recipe.Iou), ct);
                if (listB.Count == 0)
                    continue;

                var boxes = listB.Select(b => b.Box).ToList();
                var centerA = Center(a.Box);
                var bestIndex = SelectNearest(
                    centerA, boxes, window.Left, window.Top,
                    recipe.DualModel.PairingMaxDistancePx);
                if (bestIndex < 0)
                    continue;

                var boxB = listB[bestIndex].Box;
                var centerB = Center(boxB);
                var (center, angleDeg) = AngleGeometry.FromTwoPoints(
                    centerA.X + ox, centerA.Y + oy,
                    centerB.X + window.Left + ox, centerB.Y + window.Top + oy);
                windowView = null;
                poses.Add(new PixelPose(
                    center.X, center.Y, angleDeg,
                    Math.Min(a.Confidence, listB[bestIndex].Confidence))
                {
                    Overlay = new PoseOverlay
                    {
                        Boxes =
                        [
                            new PixelRect(a.Box.Left + ox, a.Box.Top + oy, a.Box.Width, a.Box.Height),
                            new PixelRect(boxB.Left + window.Left + ox, boxB.Top + window.Top + oy,
                                boxB.Width, boxB.Height),
                        ],
                        Baseline =
                        [
                            new PixelPoint(centerA.X + ox, centerA.Y + oy),
                            new PixelPoint(centerB.X + window.Left + ox, centerB.Y + window.Top + oy),
                        ],
                        Label = a.Label,
                    },
                });
            }
            finally
            {
                windowView?.Dispose();
            }
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }

    public static PixelBox ExpandWindow(PixelBox box, double ratio, int imageWidth, int imageHeight)
    {
        var dx = (int)Math.Round(box.Width * ratio);
        var dy = (int)Math.Round(box.Height * ratio);
        var left = Math.Max(0, box.Left - dx);
        var top = Math.Max(0, box.Top - dy);
        var right = Math.Min(imageWidth, box.Right + dx);
        var bottom = Math.Min(imageHeight, box.Bottom + dy);
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, bottom - top);
        return new PixelBox(left, top, width, height);
    }

    public static int SelectNearest(
        ImagePoint centerA, IReadOnlyList<PixelBox> boxes,
        double windowOffsetX, double windowOffsetY, double maxDistance)
    {
        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < boxes.Count; i++)
        {
            var c = Center(boxes[i]);
            var distance = Distance(centerA,
                new ImagePoint(c.X + windowOffsetX, c.Y + windowOffsetY));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex >= 0 && bestDistance <= maxDistance ? bestIndex : -1;
    }

    private static ImagePoint Center(PixelBox box) =>
        new((box.Left + box.Right) / 2.0, (box.Top + box.Bottom) / 2.0);

    private static double Distance(ImagePoint a, ImagePoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
