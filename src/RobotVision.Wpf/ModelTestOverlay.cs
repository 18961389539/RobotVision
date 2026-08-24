using OpenCvSharp;
using RobotVision.Core.Models;
using SkiaSharp;
using YoloDotNet.Extensions;
using YoloDotNet.Models;

namespace RobotVision.WpfHost;

/// <summary>
/// 模型测试推理的叠加绘制：检测框 / 分割轮廓 / 关键点骨架。
/// 与产线 OverlayDrawer 的区别：这里画的是"模型原始输出"（框/掩码/点），
/// 不是像素位姿（中心+角度箭头），用于验证模型本身的表现。
/// </summary>
public static class ModelTestOverlay
{
    public static void DrawDetections(Mat image, IReadOnlyList<ObjectDetection> results)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var d = results[i];
            var rect = ToRect(d.BoundingBox);
            Cv2.Rectangle(image, rect, Scalar.OrangeRed, 2, LineTypes.AntiAlias);
            DrawLabel(image, rect.X, rect.Y - 6, $"#{i} {d.Label} {d.Confidence:F2}");
        }
    }

    public static void DrawSegmentations(Mat image, IReadOnlyList<Segmentation> results)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var s = results[i];
            var rect = ToRect(s.BoundingBox);
            Cv2.Rectangle(image, rect, Scalar.OrangeRed, 2, LineTypes.AntiAlias);

            // GetContourPoints 返回相对 BoundingBox 的局部坐标
            var contour = s.GetContourPoints();
            if (contour.Length >= 3)
            {
                var points = contour.Select(p => new Point(p.X + rect.X, p.Y + rect.Y)).ToArray();
                Cv2.Polylines(image, [points], true, Scalar.Lime, 2, LineTypes.AntiAlias);
            }
            DrawLabel(image, rect.X, rect.Y - 6, $"#{i} {s.Label} {s.Confidence:F2}");
        }
    }

    /// <param name="keypointConfidence">关键点置信度阈值（低于该值画灰色点）；与 UI 的置信度参数保持一致。</param>
    public static void DrawPoses(Mat image, IReadOnlyList<PoseEstimation> results, double keypointConfidence = 0.3)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var p = results[i];
            var rect = ToRect(p.BoundingBox);
            Cv2.Rectangle(image, rect, Scalar.OrangeRed, 2, LineTypes.AntiAlias);

            var keypoints = p.KeyPoints;
            if (keypoints.Length > 1)
            {
                var points = keypoints.Select(k => new Point(k.X, k.Y)).ToArray();
                Cv2.Polylines(image, [points], false, Scalar.DodgerBlue, 2, LineTypes.AntiAlias);
                for (var k = 0; k < points.Length; k++)
                {
                    var color = keypoints[k].Confidence >= keypointConfidence ? Scalar.Lime : Scalar.Gray;
                    Cv2.Circle(image, points[k], 4, color, -1, LineTypes.AntiAlias);
                    DrawLabel(image, points[k].X + 6, points[k].Y - 4, k.ToString());
                }
            }
            DrawLabel(image, rect.X, rect.Y - 6, $"#{i} {p.Label} {p.Confidence:F2}");
        }
    }

    private static Rect ToRect(SKRectI box) => new(box.Left, box.Top, box.Width, box.Height);

    private static void DrawLabel(Mat image, int x, int y, string text)
    {
        if (y < 14)
            y = 14;
        if (x < 0)
            x = 0;
        var origin = new Point(x, y);
        // 黑色描边 + 白色文字，保证任意底色可读
        Cv2.PutText(image, text, new Point(origin.X + 1, origin.Y + 1),
            HersheyFonts.HersheyPlain, 1.2, Scalar.Black, 3, LineTypes.AntiAlias);
        Cv2.PutText(image, text, origin,
            HersheyFonts.HersheyPlain, 1.2, Scalar.White, 1, LineTypes.AntiAlias);
    }
}
