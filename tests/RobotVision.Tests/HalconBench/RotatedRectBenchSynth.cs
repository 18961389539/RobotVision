using OpenCvSharp;

namespace RobotVision.Tests.HalconBench;

/// <summary>
/// HALCON rectangle2 对标用合成矩形（与 <see cref="RotatedRectHalconBenchReportTests"/> 同参数）。
/// </summary>
internal static class RotatedRectBenchSynth
{
    public const int W = 480;
    public const int H = 360;
    public const double Cx = 240;
    public const double Cy = 180;
    public const double Long = 200;
    public const double Short = 50;
    public const int ContourSeed = 11;
    public const double EdgeRamp = 3.0;

    /// <summary>无限长条纹（仅短边有梯度，用于宽度/极性单测）。</summary>
    public static Mat Stripe(double cx, double cy, double deg, double halfShort)
    {
        var mat = new Mat(H, W, MatType.CV_8UC1, new Scalar(20));
        var rad = deg * Math.PI / 180.0;
        var nx = -Math.Sin(rad);
        var ny = Math.Cos(rad);
        for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
        {
            var across = Math.Abs((x - cx) * nx + (y - cy) * ny);
            var t = Math.Clamp((halfShort - across) / EdgeRamp + 0.5, 0, 1);
            mat.Set(y, x, (byte)Math.Round(20 + 180 * t));
        }
        return mat;
    }

    /// <summary>
    /// 有限旋转矩形灰度图：四边均有可测梯度（对标 gen_measure_rectangle2 全边卡尺）。
    /// 50% 灰度过渡落在几何半长/半宽处。
    /// </summary>
    public static Mat Rectangle(double cx, double cy, double deg, double halfLong, double halfShort, double ramp = EdgeRamp)
    {
        var mat = new Mat(H, W, MatType.CV_8UC1, new Scalar(20));
        var rad = deg * Math.PI / 180.0;
        var dirX = Math.Cos(rad);
        var dirY = Math.Sin(rad);
        var nrmX = -dirY;
        var nrmY = dirX;
        for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
        {
            var dx = x - cx;
            var dy = y - cy;
            var along = Math.Abs(dx * dirX + dy * dirY);
            var across = Math.Abs(dx * nrmX + dy * nrmY);
            var tAlong = Math.Clamp((halfLong - along) / ramp + 0.5, 0, 1);
            var tAcross = Math.Clamp((halfShort - across) / ramp + 0.5, 0, 1);
            var t = Math.Min(tAlong, tAcross);
            mat.Set(y, x, (byte)Math.Round(20 + 180 * t));
        }
        return mat;
    }

    public static Point2f[] RectContour(
        double cx, double cy, double deg, double longLen, double shortLen, double jitter, int seed = ContourSeed)
    {
        var rng = new Random(seed);
        var hw = longLen / 2.0;
        var hh = shortLen / 2.0;
        var corners = new[]
        {
            new Point2d(cx - hw, cy - hh), new Point2d(cx + hw, cy - hh),
            new Point2d(cx + hw, cy + hh), new Point2d(cx - hw, cy + hh),
        };
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2d Rot(Point2d p) => new(
            cx + (p.X - cx) * cos - (p.Y - cy) * sin,
            cy + (p.X - cx) * sin + (p.Y - cy) * cos);
        var rotated = corners.Select(Rot).ToArray();
        var pts = new List<Point2f>();
        for (var e = 0; e < 4; e++)
        {
            var a = rotated[e];
            var b = rotated[(e + 1) % 4];
            for (var s = 0; s < 40; s++)
            {
                var t = (double)s / 40;
                pts.Add(new Point2f(
                    (float)(a.X + (b.X - a.X) * t + Gaussian(rng) * jitter),
                    (float)(a.Y + (b.Y - a.Y) * t + Gaussian(rng) * jitter)));
            }
        }
        return pts.ToArray();
    }

    public static Point2f[] PartialEdgeContour(double cx, double cy, double deg, double longLen, double shortLen, double jitter)
    {
        var full = RectContour(cx, cy, deg, longLen, shortLen, jitter);
        return full.Where((_, i) => i % 40 >= 14 || i % 40 < 10).ToArray();
    }

    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
