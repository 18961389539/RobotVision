using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 检测叠加层：在去畸变图像上绘制中心十字、角度方向箭头与序号/分数。
/// </summary>
public static class OverlayDrawer
{
    private const double ArrowLength = 60;

    /// <summary>mask 填充不透明度（0~1）：多目标重叠区不重复混合，统一一次 AddWeighted。</summary>
    private const double MaskFillAlpha = 0.3;

    /// <summary>关键点置信度灰显阈值（与模型测试页 ModelTestOverlay 默认口径一致）。</summary>
    private const double KeyPointConfidenceThreshold = 0.3;

    private static readonly Scalar MaskFillColor = Scalar.Lime;
    private static readonly Scalar ContourColor = Scalar.Lime;
    private static readonly Scalar BoxColor = Scalar.OrangeRed;
    private static readonly Scalar KeyPointLineColor = Scalar.DodgerBlue;
    private static readonly Scalar BaselineColor = Scalar.Yellow;

    public static void DrawPoses(VisionImage image, IReadOnlyList<PixelPose> poses)
    {
        using var mat = VisionImageCv.AsMat(image);
        DrawPoses(mat, poses);
    }

    public static void DrawPoses(Mat image, IReadOnlyList<PixelPose> poses)
    {
        DrawOverlays(image, poses);

        for (var i = 0; i < poses.Count; i++)
        {
            var pose = poses[i];
            var center = new Point(pose.Cx, pose.Cy);
            var rad = pose.AngleDeg * Math.PI / 180.0;
            var tip = new Point(center.X + ArrowLength * Math.Cos(rad), center.Y + ArrowLength * Math.Sin(rad));

            Cv2.ArrowedLine(image, center, tip, Scalar.OrangeRed, 2, LineTypes.AntiAlias, 0, 0.35);
            Cv2.DrawMarker(image, center, Scalar.Lime, MarkerTypes.Cross, 18, 2, LineTypes.AntiAlias);

            var label = $"#{i} {pose.Score:F2}";
            var origin = new Point(center.X + 10, center.Y - 10);
            // 黑色描边 + 白色文字，保证任意底色可读
            Cv2.PutText(image, label, new Point(origin.X + 1, origin.Y + 1),
                HersheyFonts.HersheyPlain, 1.3, Scalar.Black, 3, LineTypes.AntiAlias);
            Cv2.PutText(image, label, origin,
                HersheyFonts.HersheyPlain, 1.3, Scalar.White, 1, LineTypes.AntiAlias);
        }
    }

    /// <summary>检测叠加（位姿标记下层）：mask 半透明填充 + 轮廓线，检测框，关键点骨架。</summary>
    private static void DrawOverlays(Mat image, IReadOnlyList<PixelPose> poses)
    {
        // mask 填充先画到独立覆盖层，最后一次性混合：
        // 逐目标直接混合会让多目标重叠区反复变深，视觉口径不一致
        Mat? fillLayer = null;
        try
        {
            foreach (var pose in poses)
            {
                if (pose.Overlay?.Contour is not { Count: >= 3 } contour)
                    continue;
                fillLayer ??= image.Clone();
                Cv2.FillPoly(fillLayer, [ToPoints(contour)], MaskFillColor, LineTypes.AntiAlias);
            }
            if (fillLayer is not null)
                Cv2.AddWeighted(fillLayer, MaskFillAlpha, image, 1 - MaskFillAlpha, 0, image);
        }
        finally
        {
            fillLayer?.Dispose();
        }

        foreach (var pose in poses)
        {
            var overlay = pose.Overlay;
            if (overlay is null)
                continue;

            // 角度基线（主特征中心 → 次特征中心）：验证双特征配对关系，终点带方向小圆
            if (overlay.Baseline is { Count: 2 } baseline)
            {
                var from = ToPoint(baseline[0]);
                var to = ToPoint(baseline[1]);
                Cv2.Line(image, from, to, BaselineColor, 2, LineTypes.AntiAlias);
                Cv2.Circle(image, from, 4, BaselineColor, -1, LineTypes.AntiAlias);
                Cv2.Circle(image, to, 4, BaselineColor, -1, LineTypes.AntiAlias);
            }

            if (overlay.Contour is { Count: >= 3 } contour)
                Cv2.Polylines(image, [ToPoints(contour)], true, ContourColor, 2, LineTypes.AntiAlias);

            if (overlay.Boxes is not null)
            {
                foreach (var box in overlay.Boxes)
                    Cv2.Rectangle(image, ToRect(box), BoxColor, 2, LineTypes.AntiAlias);
            }

            if (overlay.KeyPoints is { Count: > 1 } keyPoints)
            {
                var points = ToPoints(keyPoints);
                Cv2.Polylines(image, [points], false, KeyPointLineColor, 2, LineTypes.AntiAlias);
                for (var k = 0; k < points.Length; k++)
                {
                    var confidence = overlay.KeyPointConfidences is { } confidences && k < confidences.Count
                        ? confidences[k]
                        : 1.0;
                    var color = confidence >= KeyPointConfidenceThreshold ? Scalar.Lime : Scalar.Gray;
                    Cv2.Circle(image, points[k], 4, color, -1, LineTypes.AntiAlias);
                }
            }
        }
    }

    private static Point ToPoint(PixelPoint point) =>
        new((int)Math.Round(point.X), (int)Math.Round(point.Y));

    private static Point[] ToPoints(IReadOnlyList<PixelPoint> points)
    {
        var result = new Point[points.Count];
        for (var i = 0; i < points.Count; i++)
            result[i] = new Point((int)Math.Round(points[i].X), (int)Math.Round(points[i].Y));
        return result;
    }

    private static Rect ToRect(PixelRect rect) => new(
        (int)Math.Round(rect.X), (int)Math.Round(rect.Y),
        (int)Math.Round(rect.Width), (int)Math.Round(rect.Height));
}
