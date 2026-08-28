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

    public static HousingFrame Fit(IReadOnlyList<Point2f> contour)
    {
        var pts = StripProtrusion(contour);
        var rect = Cv2.MinAreaRect(pts);
        var warp = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        return new HousingFrame(
            rect.Center,
            warp,
            Math.Max(rect.Size.Width, rect.Size.Height),
            Math.Min(rect.Size.Width, rect.Size.Height));
    }

    /// <summary>
    /// 沿短轴看长边宽度：壳体带宽、凸起窄。丢掉窄带上的点后再外接矩形。
    /// 对称矩形（含分割两侧一样胀）各带等宽，原样返回——那种情况只能靠原图卡尺。
    /// </summary>
    public static Point2f[] StripProtrusion(IReadOnlyList<Point2f> contour)
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
