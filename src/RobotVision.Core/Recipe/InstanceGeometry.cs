namespace RobotVision.Core.Recipe;

/// <summary>分割实例的面积/轴比门（示教窗口）。TeachAreaPx/TeachAspect 为 0 时不检查该项。</summary>
public static class InstanceGeometry
{
    public const double DefaultAreaRatioLo = 0.55;
    public const double DefaultAreaRatioHi = 1.8;
    public const double DefaultAspectRatioLo = 0.70;
    public const double DefaultAspectRatioHi = 1.45;

    public static void EnsureRatioDefaults(TemplateOptions t)
    {
        if (t.AreaRatioLo <= 0 || t.AreaRatioHi <= 0)
        {
            t.AreaRatioLo = DefaultAreaRatioLo;
            t.AreaRatioHi = DefaultAreaRatioHi;
        }

        if (t.AspectRatioLo <= 0 || t.AspectRatioHi <= 0)
        {
            t.AspectRatioLo = DefaultAspectRatioLo;
            t.AspectRatioHi = DefaultAspectRatioHi;
        }
    }

    public static bool Accepts(TemplateOptions t, double areaPx, double aspect)
    {
        if (t.TeachAreaPx > 1)
        {
            var lo = t.TeachAreaPx * t.AreaRatioLo;
            var hi = t.TeachAreaPx * t.AreaRatioHi;
            if (areaPx < lo || areaPx > hi)
                return false;
        }

        if (t.TeachAspect > 1e-3)
        {
            var lo = t.TeachAspect * t.AspectRatioLo;
            var hi = t.TeachAspect * t.AspectRatioHi;
            if (aspect < lo || aspect > hi)
                return false;
        }

        return true;
    }

    /// <summary>多边形面积（shoelace，px²）；点数不足返回 0。</summary>
    public static double PolygonArea(IReadOnlyList<(double X, double Y)> pts)
    {
        if (pts.Count < 3)
            return 0;
        var sum = 0.0;
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }

        return Math.Abs(sum) * 0.5;
    }

    /// <summary>由示教面积/轴比推导矩形长边、短边（px）。</summary>
    public static (double LongLenPx, double ShortLenPx) DeriveRectangleSides(double areaPx, double aspect)
    {
        if (areaPx <= 1 || aspect <= 1e-3)
            return (0, 0);
        var longLen = Math.Sqrt(areaPx * aspect);
        var shortLen = Math.Sqrt(areaPx / aspect);
        return (longLen, shortLen);
    }
}
