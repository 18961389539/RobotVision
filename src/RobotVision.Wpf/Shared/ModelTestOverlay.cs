using System.Globalization;
using OpenCvSharp;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 模型测试推理的叠加绘制：检测框 / 分割轮廓 / 关键点骨架。
/// 与产线 OverlayDrawer 的区别：这里画的是"模型原始输出"（框/掩码/点），
/// 不是像素位姿（中心+角度箭头），用于验证模型本身的表现。
/// </summary>
public static class ModelTestOverlay
{
    public static void DrawDetections(Mat image, IReadOnlyList<ObjectDetectionResult> results)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var d = results[i];
            var rect = ToRect(d.Box);
            Cv2.Rectangle(image, rect, Scalar.OrangeRed, 2, LineTypes.AntiAlias);
            DrawLabel(image, rect.X, rect.Y - 6, $"#{i} {d.Label} {d.Confidence:F2}");
        }
    }

    public static void DrawSegmentations(Mat image, IReadOnlyList<InstanceSegmentation> results)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var s = results[i];
            var rect = ToRect(s.Box);
            Cv2.Rectangle(image, rect, Scalar.OrangeRed, 2, LineTypes.AntiAlias);

            if (s.ContourLocal.Count >= 3)
            {
                var points = s.ContourLocal
                    .Select(p => new Point((int)Math.Round(p.X + rect.X), (int)Math.Round(p.Y + rect.Y)))
                    .ToArray();
                Cv2.Polylines(image, [points], true, Scalar.Lime, 2, LineTypes.AntiAlias);
            }
            DrawLabel(image, rect.X, rect.Y - 6, $"#{i} {s.Label} {s.Confidence:F2}");
        }
    }

    /// <param name="keypointConfidence">关键点置信度阈值（低于该值画灰色点）；与 UI 的置信度参数保持一致。</param>
    public static void DrawPoses(Mat image, IReadOnlyList<PoseDetectionResult> results, double keypointConfidence = 0.3)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var p = results[i];
            var rect = ToRect(p.Box);
            Cv2.Rectangle(image, rect, Scalar.OrangeRed, 2, LineTypes.AntiAlias);

            if (p.KeyPoints.Count > 1)
            {
                var points = p.KeyPoints.Select(k => new Point((int)Math.Round(k.X), (int)Math.Round(k.Y))).ToArray();
                Cv2.Polylines(image, [points], false, Scalar.DodgerBlue, 2, LineTypes.AntiAlias);
                for (var k = 0; k < points.Length; k++)
                {
                    var color = p.KeyPoints[k].Confidence >= keypointConfidence ? Scalar.Lime : Scalar.Gray;
                    Cv2.Circle(image, points[k], 4, color, -1, LineTypes.AntiAlias);
                    DrawLabel(image, points[k].X + 6, points[k].Y - 4, k.ToString(CultureInfo.InvariantCulture));
                }
            }
            DrawLabel(image, rect.X, rect.Y - 6, $"#{i} {p.Label} {p.Confidence:F2}");
        }
    }

    private static Rect ToRect(PixelBox box) => new(box.X, box.Y, box.Width, box.Height);

    private static void DrawLabel(Mat image, int x, int y, string text)
    {
        if (y < 14)
            y = 14;
        if (x < 0)
            x = 0;
        var origin = new Point(x, y);
        Cv2.PutText(image, text, new Point(origin.X + 1, origin.Y + 1),
            HersheyFonts.HersheyPlain, 1.2, Scalar.Black, 3, LineTypes.AntiAlias);
        Cv2.PutText(image, text, origin,
            HersheyFonts.HersheyPlain, 1.2, Scalar.White, 1, LineTypes.AntiAlias);
    }
}
