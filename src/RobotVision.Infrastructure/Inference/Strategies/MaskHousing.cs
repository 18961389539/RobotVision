using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>壳体框：剔除短轴一侧凸起后的外接矩形，用于粗角/转正中心，避免分割把凸起画肥时中心被拖偏。</summary>
public readonly record struct HousingFrame(
    Point2f Center,
    double WarpAngleDeg,
    double LongLen,
    double ShortLen)
{
    /// <summary>无向长边角 [0,180)。</summary>
    public double LongAxisDeg => AngleGeometry.NormalizeDeg(WarpAngleDeg);
}

/// <summary>从分割轮廓估计壳体几何，并给出卡尺探针数量、精修搜索范围等自适应值。</summary>
public static class MaskHousing
{
    /// <summary>沿长边每约 14px 一根探针，限制在 [8,24]。</summary>
    public static int ProbeCount(double longLenPx) =>
        Math.Clamp((int)Math.Round(longLenPx / 14.0), 8, 24);

    /// <summary>细长件端头 inset 18%；偏方的圆角件略加大，躲开圆角。</summary>
    public static double EndInsetRatio(double longLenPx, double shortLenPx)
    {
        var aspect = longLenPx / Math.Max(1.0, shortLenPx);
        return aspect < 2.0 ? 0.22 : 0.18;
    }

    /// <summary>
    /// 卡尺探针沿布置轴的端头内缩。抓左右短边时加大内缩，躲开上齿列/下缺口。
    /// </summary>
    public static double ProbeInsetRatio(double probeSpanPx, double searchSpanPx, bool probeAlongLong)
    {
        var inset = EndInsetRatio(probeSpanPx, searchSpanPx);
        if (probeAlongLong)
            return inset;
        return Math.Clamp(Math.Max(inset, 0.28), 0.22, 0.36);
    }

    /// <summary>细长壳体粗角更稳，收紧旋转搜索；近方形保留配方上限。</summary>
    public static double AdaptiveRefineRange(double configuredDeg, HousingFrame housing)
    {
        var aspect = housing.LongLen / Math.Max(1.0, housing.ShortLen);
        if (aspect >= 2.5)
            return Math.Clamp(Math.Min(configuredDeg, 3.0), 1.0, 45.0);
        if (aspect < 1.4)
            return Math.Clamp(configuredDeg, 1.0, 45.0);
        return Math.Clamp(Math.Min(configuredDeg, 5.0), 1.0, 45.0);
    }

    public static HousingFrame Fit(IReadOnlyList<Point2f> contour) =>
        FromRect(Cv2.MinAreaRect(StripProtrusion(contour)));

    /// <summary>分割外接矩形（不剔凸起）。示教轴比/过门与掩码本身比，不能跟壳体剥离走。</summary>
    public static HousingFrame FitObb(IReadOnlyList<Point2f> contour) =>
        FromRect(Cv2.MinAreaRect(ToArray(contour)));

    public static double Aspect(HousingFrame h) =>
        h.LongLen / Math.Max(1.0, h.ShortLen);

    private static HousingFrame FromRect(RotatedRect rect)
    {
        var warp = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        return new HousingFrame(
            rect.Center,
            warp,
            Math.Max(rect.Size.Width, rect.Size.Height),
            Math.Min(rect.Size.Width, rect.Size.Height));
    }

    /// <summary>壳体核心点（剔凸起后），供直线拟合等与 Fit 同源。</summary>
    public static Point2f[] CorePoints(IReadOnlyList<Point2f> contour) =>
        StripProtrusion(contour);

    /// <summary>
    /// 沿短轴看长边宽度：壳体带宽、凸起窄。丢掉窄带上的点后再外接矩形。
    /// 对称矩形（含分割两侧一样胀）各带等宽，原样返回——那种情况只能靠原图卡尺。
    /// 齿列间隙会让该带占空比下降，即使外接宽度仍接近壳体也会被丢掉。
    /// </summary>
    public static Point2f[] StripProtrusion(IReadOnlyList<Point2f> contour)
    {
        var occupancy = StripSparseOccupancy(contour);
        if (occupancy is not null)
            return occupancy;
        return StripNarrowBands(contour);
    }

    /// <summary>短轴分带：占空比明显低于壳体的齿列/缺口带丢掉。</summary>
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

        Cv2.DrawContours(mask, new[] { local }, 0, Scalar.All(255), thickness: -1);
        const int bins = 16;
        var counts = new int[bins];
        var sMin = -shortLen / 2.0;
        var span = shortLen;
        var indexer = mask.GetGenericIndexer<byte>();
        for (var y = 0; y < mask.Rows; y++)
        {
            for (var x = 0; x < mask.Cols; x++)
            {
                if (indexer[y, x] == 0)
                    continue;
                var s = (x + bounds.X - cx) * sx + (y + bounds.Y - cy) * sy;
                var b = (int)Math.Clamp(Math.Floor((s - sMin) / span * bins), 0, bins - 1);
                counts[b]++;
            }
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

        // 两端 bin 常因栅格切边半空，实心矩形也会被当成齿列。只认内部带的明显空洞。
        var interiorSparse = 0;
        var interiorFilled = 0;
        var interiorMin = int.MaxValue;
        for (var b = 1; b < bins - 1; b++)
        {
            if (counts[b] < 4)
                continue;
            interiorFilled++;
            if (counts[b] < interiorMin)
                interiorMin = counts[b];
            if (counts[b] < 0.75 * maxFill)
                interiorSparse++;
        }

        if (interiorFilled < 4 || interiorSparse < 2 || interiorMin >= 0.80 * maxFill)
            return null;

        var dense = new bool[bins];
        var denseCount = 0;
        var filled = 0;
        for (var b = 0; b < bins; b++)
        {
            if (counts[b] < 4)
                continue;
            filled++;
            dense[b] = counts[b] >= 0.88 * maxFill;
            if (dense[b])
                denseCount++;
        }

        if (denseCount < 3 || denseCount == filled)
            return null;

        var bestLo = 0;
        var bestHi = -1;
        var bin = 0;
        while (bin < bins)
        {
            if (!dense[bin])
            {
                bin++;
                continue;
            }

            var j = bin;
            while (j + 1 < bins && dense[j + 1])
                j++;
            if (j - bin > bestHi - bestLo)
            {
                bestLo = bin;
                bestHi = j;
            }

            bin = j + 1;
        }

        var run = bestHi - bestLo + 1;
        if (run < 5 || run < filled * 0.4)
            return null;

        var keepBand = new bool[bins];
        for (var b = bestLo; b <= bestHi; b++)
            keepBand[b] = true;

        var keep = new List<Point2f>(n);
        for (var p = 0; p < n; p++)
        {
            var dx = contour[p].X - cx;
            var dy = contour[p].Y - cy;
            var s = dx * sx + dy * sy;
            var b = (int)Math.Clamp(Math.Floor((s - sMin) / span * bins), 0, bins - 1);
            if (keepBand[b])
                keep.Add(contour[p]);
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
            wide[b] = wMax[b] - wMin[b] >= 0.88 * maxWidth;
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
