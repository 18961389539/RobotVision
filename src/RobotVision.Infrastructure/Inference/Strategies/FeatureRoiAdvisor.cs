using OpenCvSharp;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 在转正图上滑窗找对 180° 翻转最不对称的块，映射回全图相对 ROI。
/// 只在示教/试触发调用，不进 TRIGGER 热路径。
/// </summary>
public static class FeatureRoiAdvisor
{
    /// <summary>
    /// 建议特征框（相对全图像素 0~1）。对称件或窗口过小返回 null。
    /// <paramref name="originX"/>/<paramref name="originY"/> 为转正所用图相对全图的原点（检测 ROI）。
    /// </summary>
    public static Roi? Suggest(
        UprightCropResult crop,
        int fullImageWidth,
        int fullImageHeight,
        double originX = 0,
        double originY = 0)
    {
        if (fullImageWidth < 8 || fullImageHeight < 8)
            return null;
        var upright = crop.Upright;
        if (upright.Empty() || upright.Width < 24 || upright.Height < 24)
            return null;

        using var gray = ToGray(upright);
        using var flipped = new Mat();
        Cv2.Rotate(gray, flipped, RotateFlags.Rotate180);

        var minSide = Math.Min(gray.Width, gray.Height);
        var bestGap = 0.08;
        Rect? best = null;

        foreach (var size in WindowSizes(minSide))
        {
            var step = Math.Max(4, size / 3);
            for (var y = 0; y <= gray.Height - size; y += step)
            {
                for (var x = 0; x <= gray.Width - size; x += step)
                {
                    var rect = new Rect(x, y, size, size);
                    using var patch = gray[rect];
                    using var counterpart = flipped[rect];
                    Cv2.MeanStdDev(patch, out _, out var std);
                    if (std.Val0 < 8)
                        continue;

                    using var ncc = patch.MatchTemplate(counterpart, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(ncc, out _, out var maxVal, out _, out _);
                    var gap = Math.Clamp(1.0 - maxVal, 0, 1);
                    var ux = (x + size / 2.0) / gray.Width;
                    var uy = (y + size / 2.0) / gray.Height;
                    if (ux < 0.28 || ux > 0.72)
                        gap *= 1.08;
                    if (uy < 0.35 || uy > 0.65)
                        gap *= 1.12;
                    if (gap > bestGap)
                    {
                        bestGap = gap;
                        best = rect;
                    }
                }
            }
        }

        if (best is not { } win)
            return null;
        if (win.Width * win.Height > 0.55 * gray.Width * gray.Height)
            return null;

        var corners = new[]
        {
            MaskTemplateMatcher.MapUprightToSource(crop, new Point2d(win.X, win.Y)),
            MaskTemplateMatcher.MapUprightToSource(crop, new Point2d(win.X + win.Width, win.Y)),
            MaskTemplateMatcher.MapUprightToSource(crop, new Point2d(win.X + win.Width, win.Y + win.Height)),
            MaskTemplateMatcher.MapUprightToSource(crop, new Point2d(win.X, win.Y + win.Height)),
        };
        var minX = corners.Min(p => p.X) + originX;
        var minY = corners.Min(p => p.Y) + originY;
        var maxX = corners.Max(p => p.X) + originX;
        var maxY = corners.Max(p => p.Y) + originY;
        var nx = Math.Clamp(minX / fullImageWidth, 0, 1);
        var ny = Math.Clamp(minY / fullImageHeight, 0, 1);
        var nw = Math.Clamp((maxX - minX) / fullImageWidth, 0.02, 1 - nx);
        var nh = Math.Clamp((maxY - minY) / fullImageHeight, 0.02, 1 - ny);
        if (nw < 0.02 || nh < 0.02)
            return null;
        return new Roi(nx, ny, nw, nh);
    }

    private static IEnumerable<int> WindowSizes(int minSide)
    {
        var a = Math.Max(16, minSide / 4);
        var b = Math.Max(20, minSide / 3);
        var c = Math.Max(24, (int)(minSide * 0.40));
        return new[] { a, b, c }.Where(s => s < minSide - 4).Distinct();
    }

    private static Mat ToGray(Mat src)
    {
        if (src.Channels() == 1)
            return src.Clone();
        var gray = new Mat();
        Cv2.CvtColor(src, gray, src.Channels() == 4
            ? ColorConversionCodes.BGRA2GRAY
            : ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
