using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>壳体框：剔除短轴一侧凸起后的外接矩形，用于粗角/转正中心与卡尺布置。</summary>
public readonly record struct HousingFrame(
    Point2f Center,
    double WarpAngleDeg,
    double LongLen,
    double ShortLen)
{
    /// <summary>无向长边角 [0,180)。</summary>
    public double LongAxisDeg => AngleGeometry.NormalizeDeg(WarpAngleDeg);
}

/// <summary>从分割轮廓估计壳体几何（OpenCV 轮廓几何，不依赖 RobotVision.Vision）。</summary>
public static class JlHousing
{
    public static int ProbeCount(double longLenPx) =>
        Math.Clamp((int)Math.Round(longLenPx / 14.0), 8, 24);

    public static double EndInsetRatio(double longLenPx, double shortLenPx)
    {
        var aspect = longLenPx / Math.Max(1.0, shortLenPx);
        return aspect < 2.0 ? 0.22 : 0.18;
    }

    public static double ProbeInsetRatio(double probeSpanPx, double searchSpanPx, bool probeAlongLong)
    {
        var inset = EndInsetRatio(probeSpanPx, searchSpanPx);
        if (probeAlongLong)
            return inset;
        return Math.Clamp(Math.Max(inset, 0.28), 0.22, 0.36);
    }

    public static RefineAngleWindow AdaptiveRefineBounds(RefineAngleWindow configured, HousingFrame housing)
    {
        var aspect = housing.LongLen / Math.Max(1.0, housing.ShortLen);
        var cap = aspect >= 2.5 ? 3.0 : aspect < 1.4 ? 45.0 : 5.0;
        var lo = Math.Clamp(configured.LoDeg, -cap, cap);
        var hi = Math.Clamp(configured.HiDeg, -cap, cap);
        if (lo > hi)
            (lo, hi) = (hi, lo);
        if (hi - lo < 1.0)
        {
            hi = Math.Min(cap, lo + 1.0);
            if (hi - lo < 1.0)
                lo = hi - 1.0;
        }

        return new RefineAngleWindow(lo, hi);
    }

    public static double AdaptiveRefineRange(double configuredDeg, HousingFrame housing) =>
        AdaptiveRefineBounds(RefineAngleWindow.Symmetric(configuredDeg), housing).MaxAbs;

    public static HousingFrame Fit(IReadOnlyList<Point2f> contour)
    {
        var pts = CorePoints(contour);
        var rect = Cv2.MinAreaRect(pts);
        var warp = WarpFromMinAreaRect(rect);
        return new HousingFrame(
            rect.Center,
            warp,
            Math.Max(rect.Size.Width, rect.Size.Height),
            Math.Min(rect.Size.Width, rect.Size.Height));
    }

    public static HousingFrame FitObb(IReadOnlyList<Point2f> contour)
    {
        var rect = Cv2.MinAreaRect(contour);
        var warp = WarpFromMinAreaRect(rect);
        return new HousingFrame(
            rect.Center,
            warp,
            Math.Max(rect.Size.Width, rect.Size.Height),
            Math.Min(rect.Size.Width, rect.Size.Height));
    }

    public static double Aspect(HousingFrame frame) =>
        frame.LongLen / Math.Max(1.0, frame.ShortLen);

    public static Point2f[] CorePoints(IReadOnlyList<Point2f> contour) =>
        TryOpenProtrusion(contour) ?? StripProtrusion(contour);

    public static Point2f[] StripProtrusion(IReadOnlyList<Point2f> contour)
    {
        var occupancy = StripSparseOccupancy(contour);
        if (occupancy is not null)
            return occupancy;
        return StripNarrowBands(contour);
    }

    internal static Point2f[]? TryOpenProtrusion(IReadOnlyList<Point2f> contour)
    {
        if (contour.Count < 16)
            return null;

        var obb = Cv2.MinAreaRect(contour);
        var shortLen = Math.Min(obb.Size.Width, obb.Size.Height);
        if (shortLen < 16)
            return null;

        var k = (int)Math.Round(0.18 * shortLen);
        if ((k & 1) == 0)
            k++;
        k = Math.Clamp(k, 3, Math.Max(3, (int)(0.30 * shortLen) | 1));
        if (k < 5)
            return null;

        var bounds = Cv2.BoundingRect(contour);
        if (bounds.Width < 8 || bounds.Height < 8)
            return null;

        using var mask = new Mat(bounds.Height, bounds.Width, MatType.CV_8UC1, Scalar.All(0));
        var local = new Point[contour.Count];
        for (var i = 0; i < contour.Count; i++)
        {
            local[i] = new Point(
                (int)Math.Round(contour[i].X - bounds.X),
                (int)Math.Round(contour[i].Y - bounds.Y));
        }

        Cv2.FillPoly(mask, new[] { local }, Scalar.All(255));
        var area0 = Cv2.CountNonZero(mask);
        if (area0 < 32)
            return null;

        using var tightened = new Mat();
        using var tightenKernel = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(3, 3));
        Cv2.Erode(mask, tightened, tightenKernel);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(k, k));
        using var opened = new Mat();
        Cv2.MorphologyEx(tightened, opened, MorphTypes.Open, kernel);
        var area1 = Cv2.CountNonZero(opened);
        var ratio = area1 / (double)area0;
        if (ratio < 0.50 || ratio > 0.96)
            return null;

        Cv2.FindContours(opened, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        if (contours.Length == 0)
            return null;
        var best = contours.MaxBy(c => Cv2.ContourArea(c))!;
        if (best.Length < 12)
            return null;

        var pts = new Point2f[best.Length];
        for (var i = 0; i < best.Length; i++)
            pts[i] = new Point2f(best[i].X + bounds.X, best[i].Y + bounds.Y);
        return pts;
    }

    private static Point2f[]? StripSparseOccupancy(IReadOnlyList<Point2f> contour)
    {
        var n = contour.Count;
        if (n < 16)
            return null;

        var rect = Cv2.MinAreaRect(contour);
        var theta0 = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        var rad = theta0 * Math.PI / 180.0;
        var sx = -Math.Sin(rad);
        var sy = Math.Cos(rad);
        var cx = rect.Center.X;
        var cy = rect.Center.Y;
        var shortLen = Math.Min(rect.Size.Width, rect.Size.Height);
        if (shortLen < 8)
            return null;

        var bounds = Cv2.BoundingRect(contour);
        if (bounds.Width < 8 || bounds.Height < 8)
            return null;

        using var mask = new Mat(bounds.Height, bounds.Width, MatType.CV_8UC1, Scalar.All(0));
        var local = new Point[n];
        for (var i = 0; i < n; i++)
        {
            local[i] = new Point(
                (int)Math.Round(contour[i].X - bounds.X),
                (int)Math.Round(contour[i].Y - bounds.Y));
        }

        Cv2.FillPoly(mask, new[] { local }, Scalar.All(255));
        using var nz = new Mat();
        Cv2.FindNonZero(mask, nz);
        if (nz.Empty() || nz.Rows < 32)
            return null;

        const int bins = 16;
        var counts = new int[bins];
        var sMin = -shortLen / 2.0;
        var span = shortLen;
        if (span < 8)
            return null;

        for (var i = 0; i < nz.Rows; i++)
        {
            var p = nz.At<Point>(i);
            var x = p.X + bounds.X;
            var y = p.Y + bounds.Y;
            var s = (x - cx) * sx + (y - cy) * sy;
            var b = (int)Math.Clamp(Math.Floor((s - sMin) / span * bins), 0, bins - 1);
            counts[b]++;
        }

        var maxFill = 0;
        var used = 0;
        for (var b = 0; b < bins; b++)
        {
            if (counts[b] < 4)
                continue;
            used++;
            if (counts[b] > maxFill)
                maxFill = counts[b];
        }

        if (maxFill < 8 || used < 4)
            return null;

        var dense = new bool[bins];
        var denseCount = 0;
        var filled = 0;
        for (var b = 0; b < bins; b++)
        {
            if (counts[b] < 4)
                continue;
            filled++;
            dense[b] = counts[b] >= 0.62 * maxFill;
            if (dense[b])
                denseCount++;
        }

        if (denseCount < 3 || denseCount == filled)
            return null;

        var keep = new List<Point2f>(n);
        for (var i = 0; i < n; i++)
        {
            var dx = contour[i].X - cx;
            var dy = contour[i].Y - cy;
            var s = dx * sx + dy * sy;
            var b = (int)Math.Clamp(Math.Floor((s - sMin) / span * bins), 0, bins - 1);
            if (dense[b])
                keep.Add(contour[i]);
        }

        if (keep.Count < Math.Max(12, n * 2 / 5))
            return null;
        return keep.ToArray();
    }

    private static Point2f[] StripNarrowBands(IReadOnlyList<Point2f> contour)
    {
        var n = contour.Count;
        if (n < 16)
            return ToArray(contour);

        var rect = Cv2.MinAreaRect(contour);
        var theta0 = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        var rad = theta0 * Math.PI / 180.0;
        var lx = Math.Cos(rad);
        var ly = Math.Sin(rad);
        var sx = -Math.Sin(rad);
        var sy = Math.Cos(rad);
        var cx = rect.Center.X;
        var cy = rect.Center.Y;

        var t = new double[n];
        var s = new double[n];
        var sMin = double.PositiveInfinity;
        var sMax = double.NegativeInfinity;
        for (var i = 0; i < n; i++)
        {
            var dx = contour[i].X - cx;
            var dy = contour[i].Y - cy;
            t[i] = dx * lx + dy * ly;
            s[i] = dx * sx + dy * sy;
            if (s[i] < sMin) sMin = s[i];
            if (s[i] > sMax) sMax = s[i];
        }

        var span = sMax - sMin;
        if (span < 8)
            return ToArray(contour);

        const int bins = 16;
        var wMin = new double[bins];
        var wMax = new double[bins];
        var wCount = new int[bins];
        Array.Fill(wMin, double.PositiveInfinity);
        Array.Fill(wMax, double.NegativeInfinity);
        for (var i = 0; i < n; i++)
        {
            var b = (int)Math.Clamp(Math.Floor((s[i] - sMin) / span * bins), 0, bins - 1);
            wCount[b]++;
            if (t[i] < wMin[b]) wMin[b] = t[i];
            if (t[i] > wMax[b]) wMax[b] = t[i];
        }

        var maxWidth = 0.0;
        for (var b = 0; b < bins; b++)
        {
            if (wCount[b] < 2)
                continue;
            var w = wMax[b] - wMin[b];
            if (w > maxWidth)
                maxWidth = w;
        }

        if (maxWidth < 8)
            return ToArray(contour);

        var wide = new bool[bins];
        var wideCount = 0;
        for (var b = 0; b < bins; b++)
        {
            if (wCount[b] < 2)
                continue;
            wide[b] = wMax[b] - wMin[b] >= 0.62 * maxWidth;
            if (wide[b])
                wideCount++;
        }

        if (wideCount < 3 || wideCount == bins)
            return ToArray(contour);

        var keep = new List<Point2f>(n);
        for (var i = 0; i < n; i++)
        {
            var b = (int)Math.Clamp(Math.Floor((s[i] - sMin) / span * bins), 0, bins - 1);
            if (wide[b])
                keep.Add(contour[i]);
        }

        if (keep.Count < Math.Max(12, n * 2 / 5))
            return ToArray(contour);
        return keep.ToArray();
    }

    internal static double WarpFromMinAreaRect(RotatedRect rect)
    {
        var raw = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        return AngleGeometry.CanonWarpDeg(raw);
    }

    private static Point2f[] ToArray(IReadOnlyList<Point2f> contour)
    {
        if (contour is Point2f[] arr)
            return arr;
        var copy = new Point2f[contour.Count];
        for (var i = 0; i < copy.Length; i++)
            copy[i] = contour[i];
        return copy;
    }
}
