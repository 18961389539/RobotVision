using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;

namespace RobotVision.Teach;

/// <summary>一种窗口边长下、对 180° 最不对称的特征框。</summary>
public sealed record FeatureRoiCandidate(int SizePx, double Gap, Roi Roi);

/// <summary>
/// 在转正图上、分割 mask 外接矩形内滑窗找对 180° 翻转最不对称的块，映射回全图相对 ROI。
/// 只在示教/试触发调用，不进 TRIGGER 热路径。
/// </summary>
public static class FeatureRoiAdvisor
{
    /// <summary>
    /// 建议特征框（相对全图像素 0~1）。对称件或窗口过小返回 null。
    /// 等价于 <see cref="Rank"/> 的最高分项。
    /// </summary>
    public static Roi? Suggest(
        UprightCropResult crop,
        int fullImageWidth,
        int fullImageHeight,
        IReadOnlyList<Point2f> contour,
        double originX = 0,
        double originY = 0)
    {
        var ranked = Rank(crop, fullImageWidth, fullImageHeight, contour, originX, originY);
        return ranked.Count > 0 ? ranked[0].Roi : null;
    }

    /// <summary>
    /// 按窗口边长（约短边 1/4、1/3、0.4）分别取最佳块，再按 0/180 分差排序。
    /// 不搜 8×8 / 16×16 这种绝对像素档。
    /// </summary>
    public static IReadOnlyList<FeatureRoiCandidate> Rank(
        UprightCropResult crop,
        int fullImageWidth,
        int fullImageHeight,
        IReadOnlyList<Point2f> contour,
        double originX = 0,
        double originY = 0)
    {
        if (fullImageWidth < 8 || fullImageHeight < 8 || contour.Count < 3)
            return [];
        var upright = crop.Upright;
        if (upright.Empty() || upright.Width < 24 || upright.Height < 24)
            return [];

        var bounds = MaskBoundsInUpright(crop, contour, upright.Width, upright.Height);
        if (bounds.Width < 16 || bounds.Height < 16)
            return [];

        using var gray = ToGray(upright);
        using var flipped = new Mat();
        Cv2.Rotate(gray, flipped, RotateFlags.Rotate180);

        var minSide = Math.Min(bounds.Width, bounds.Height);
        var ranked = new List<FeatureRoiCandidate>();
        foreach (var size in WindowSizes(minSide))
        {
            if (size > bounds.Width || size > bounds.Height)
                continue;
            var bestGap = 0.08;
            Rect? best = null;
            var step = Math.Max(4, size / 3);
            var yMax = bounds.Y + bounds.Height - size;
            var xMax = bounds.X + bounds.Width - size;
            for (var y = bounds.Y; y <= yMax; y += step)
            {
                for (var x = bounds.X; x <= xMax; x += step)
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
                    var ux = (x + size / 2.0 - bounds.X) / bounds.Width;
                    var uy = (y + size / 2.0 - bounds.Y) / bounds.Height;
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

            if (best is not { } win)
                continue;
            if (win.Width * win.Height > 0.55 * bounds.Width * bounds.Height)
                continue;
            var roi = MapWindow(crop, win, fullImageWidth, fullImageHeight, originX, originY);
            if (roi is not null)
                ranked.Add(new FeatureRoiCandidate(size, bestGap, roi));
        }

        return ranked.OrderByDescending(c => c.Gap).ToList();
    }

    /// <summary>
    /// 示教时用特征框与实例的像素交集挑目标。不要只看框中心是否落在 YOLO 盒内：
    /// 局部窗在齿脚/凸起上、或映射时按全图 2% 撑开，中心常落在盒外，框仍盖着目标。
    /// </summary>
    public static IReadOnlyList<InstanceSegmentation> PickOverlapping(
        IReadOnlyList<InstanceSegmentation> instances,
        Roi feature,
        int fullImageWidth,
        int fullImageHeight,
        double originX = 0,
        double originY = 0)
    {
        if (instances.Count == 0 || fullImageWidth < 1 || fullImageHeight < 1)
            return [];

        var fx = feature.X * fullImageWidth - originX;
        var fy = feature.Y * fullImageHeight - originY;
        var fw = Math.Max(1.0, feature.Width * fullImageWidth);
        var fh = Math.Max(1.0, feature.Height * fullImageHeight);
        var hits = new List<(InstanceSegmentation Seg, double Area)>(instances.Count);
        foreach (var seg in instances)
        {
            var area = OverlapAreaPx(fx, fy, fw, fh, seg);
            if (area > 0)
                hits.Add((seg, area));
        }

        return hits
            .OrderByDescending(h => h.Area)
            .ThenByDescending(h => h.Seg.Confidence)
            .Select(h => h.Seg)
            .ToList();
    }

    /// <summary>过小的占位框（勾选后 1×1）不能当示教裁剪。</summary>
    public static bool IsDrawable(Roi roi, int fullImageWidth, int fullImageHeight) =>
        roi.Width * fullImageWidth >= 8 && roi.Height * fullImageHeight >= 8;

    internal static double OverlapAreaPx(
        double fx, double fy, double fw, double fh, InstanceSegmentation seg)
    {
        var left = (double)seg.Box.Left;
        var top = (double)seg.Box.Top;
        var right = (double)seg.Box.Right;
        var bottom = (double)seg.Box.Bottom;
        foreach (var p in seg.ContourLocal)
        {
            var x = p.X + seg.Box.X;
            var y = p.Y + seg.Box.Y;
            if (x < left) left = x;
            if (y < top) top = y;
            if (x > right) right = x;
            if (y > bottom) bottom = y;
        }

        var overlapW = Math.Min(fx + fw, right) - Math.Max(fx, left);
        var overlapH = Math.Min(fy + fh, bottom) - Math.Max(fy, top);
        return overlapW > 0 && overlapH > 0 ? overlapW * overlapH : 0;
    }

    /// <summary>把分割轮廓映到转正图，取轴对齐外接矩形（与 mask 外接框同口径）。</summary>
    internal static Rect MaskBoundsInUpright(
        UprightCropResult crop,
        IReadOnlyList<Point2f> contour,
        int uprightWidth,
        int uprightHeight)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var p in contour)
        {
            var u = MaskTemplateMatcher.MapSourceToUpright(crop, new Point2d(p.X, p.Y));
            minX = Math.Min(minX, u.X);
            minY = Math.Min(minY, u.Y);
            maxX = Math.Max(maxX, u.X);
            maxY = Math.Max(maxY, u.Y);
        }

        var x = Math.Clamp((int)Math.Floor(minX), 0, Math.Max(0, uprightWidth - 1));
        var y = Math.Clamp((int)Math.Floor(minY), 0, Math.Max(0, uprightHeight - 1));
        var right = Math.Clamp((int)Math.Ceiling(maxX), x + 1, uprightWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(maxY), y + 1, uprightHeight);
        return new Rect(x, y, right - x, bottom - y);
    }

    private static Roi? MapWindow(
        UprightCropResult crop, Rect win, int fullImageWidth, int fullImageHeight,
        double originX, double originY)
    {
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
        const double minPx = 8.0;
        var cx = (minX + maxX) / 2.0;
        var cy = (minY + maxY) / 2.0;
        var w = Math.Max(maxX - minX, minPx);
        var h = Math.Max(maxY - minY, minPx);
        minX = Math.Clamp(cx - w / 2.0, 0, fullImageWidth);
        minY = Math.Clamp(cy - h / 2.0, 0, fullImageHeight);
        maxX = Math.Clamp(cx + w / 2.0, minX + 1, fullImageWidth);
        maxY = Math.Clamp(cy + h / 2.0, minY + 1, fullImageHeight);
        var nx = minX / fullImageWidth;
        var ny = minY / fullImageHeight;
        var nw = (maxX - minX) / fullImageWidth;
        var nh = (maxY - minY) / fullImageHeight;
        if (nw * fullImageWidth < minPx || nh * fullImageHeight < minPx)
            return null;
        return new Roi(
            Math.Clamp(nx, 0, 1),
            Math.Clamp(ny, 0, 1),
            Math.Clamp(nw, 0, 1 - nx),
            Math.Clamp(nh, 0, 1 - ny));
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
