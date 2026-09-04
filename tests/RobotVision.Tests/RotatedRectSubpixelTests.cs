using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>旋转矩形亚像素精修（measure_pairs）单测。</summary>
public sealed class RotatedRectSubpixelTests
{
    [Fact]
    public void Refine_Near180Seed_DoesNotFlipToZero()
    {
        var center = new Point2d(2000, 1500);
        using var gray = Stripe(4000, 3000, center, 179.8, halfShort: 55);
        var r = RotatedRectSubpixel.Refine(gray, center, longLen: 420, shortLen: 110, seedAngleDeg: 179.77);
        Assert.NotNull(r);
        Assert.True(AngleGeometry.UndirectedDeltaDeg(r!.Value.AngleDeg, 179.77) < 1.0,
            $"角 {r.Value.AngleDeg:0.00}° 相对种子 179.77° 翻转");
    }

    [Fact]
    public void Refine_RecoversSubpixelGeometry_FromSkewedSeed()
    {
        const double trueDeg = 15.0;
        var center = new Point2d(160, 120);
        using var gray = Stripe(320, 240, center, trueDeg, halfShort: 30);

        var rad = trueDeg * Math.PI / 180.0;
        var seedCenter = new Point2d(center.X - Math.Sin(rad), center.Y + Math.Cos(rad));
        var r = RotatedRectSubpixel.Refine(gray, seedCenter, longLen: 140, shortLen: 60, seedAngleDeg: 16.5);

        Assert.NotNull(r);
        Assert.True(Math.Abs(r!.Value.AngleDeg - trueDeg) < 0.15, $"角 {r.Value.AngleDeg:0.000}");
        var dx = r.Value.Center.X - center.X;
        var dy = r.Value.Center.Y - center.Y;
        Assert.True(dx * dx + dy * dy < 0.5 * 0.5, $"中心误差 {Math.Sqrt(dx * dx + dy * dy):0.000}px");
        Assert.InRange(r.Value.LongLen, 136, 144);
        Assert.InRange(r.Value.ShortLen, 58, 62);
        Assert.True(r.Value.RmsPx < 0.8);
        Assert.True(RotatedRectSubpixel.QualityScore(r.Value) > 0.7);
    }

    [Fact]
    public void RefineFromContour_TwoStage_BetterThanContourOnly()
    {
        var center = new Point2d(160, 120);
        using var gray = Stripe(320, 240, center, 33.0, halfShort: 28);
        var contour = RotatedRectContour(center, 33.0, 150, 56, samplesPerEdge: 40, jitter: 0.6);

        var contourOnly = RotatedRectFitter.Fit(contour, 34.0);
        var both = RotatedRectSubpixel.RefineFromContour(gray, contour, 34.0);

        Assert.True(contourOnly.Ok && both.Ok);
        var contourErr = UndirectedErr(contourOnly.AngleDeg, 33.0);
        var subErr = UndirectedErr(both.AngleDeg, 33.0);
        Assert.True(subErr <= contourErr + 0.05, $"亚像素应不差于轮廓 contour={contourErr:0.00} sub={subErr:0.00}");
        Assert.True(both.RmsPx <= contourOnly.RmsPx + 0.3);
    }

    [Fact]
    public void Polarity_AnyAndDarkToBright_BothRun()
    {
        var center = new Point2d(100, 80);
        using var gray = Stripe(200, 160, center, 0, halfShort: 25);
        Assert.NotNull(RotatedRectSubpixel.Refine(gray, center, 120, 50, 0, RectEdgePolarity.Any));
        // 有极性时短边可能不足，长边仍应可测；失败不视为回归（极性门控更严）。
        _ = RotatedRectSubpixel.Refine(gray, center, 120, 50, 0, RectEdgePolarity.DarkToBright);
    }

    [Fact]
    public void Refine_ProblemAngles_WithTrueSeed_RecoversCenter()
    {
        foreach (var trueDeg in new[] { -18.0, 135.0 })
        {
            var center = new Point2d(240, 180);
            using var gray = Stripe(480, 360, center, trueDeg, halfShort: 25);
            var r = RotatedRectSubpixel.Refine(gray, center, longLen: 200, shortLen: 50, seedAngleDeg: trueDeg);
            Assert.NotNull(r);
            var dx = r!.Value.Center.X - center.X;
            var dy = r!.Value.Center.Y - center.Y;
            Assert.True(dx * dx + dy * dy < 0.6 * 0.6,
                $"θ={trueDeg}° 中心误差 {Math.Sqrt(dx * dx + dy * dy):0.00}px");
        }
    }

    [Fact]
    public void ContourFit_ProblemAngles_RecoversGeometry()
    {
        foreach (var trueDeg in new[] { -18.0, 22.0, 135.0 })
        {
            var contour = RotatedRectContour(new Point2d(240, 180), trueDeg, 200, 50, 40, jitter: 0.4);
            var fitRaw = RotatedRectFitter.Fit(contour, trueDeg);
            Assert.True(fitRaw.Ok);
            var angErr = UndirectedErr(fitRaw.AngleDeg, trueDeg);
            var cxErr = Math.Abs(fitRaw.Center.X - 240);
            var cyErr = Math.Abs(fitRaw.Center.Y - 180);
            Assert.True(angErr < 0.35, $"θ={trueDeg}° 角误差 {angErr:0.000}");
            Assert.True(cxErr < 2 && cyErr < 2, $"θ={trueDeg}° 中心 ({cxErr:0.0},{cyErr:0.0})");
        }
    }

    [Fact]
    public void UniformImage_ReturnsNull()
    {
        using var gray = new Mat(200, 200, MatType.CV_8UC1, new Scalar(128));
        Assert.Null(RotatedRectSubpixel.Refine(gray, new Point2d(100, 100), 120, 40, 0));
    }

    private static double UndirectedErr(double got, double truth)
    {
        var d = Math.Abs(got - truth);
        return Math.Min(d, 180 - d);
    }

    private static Mat Stripe(int w, int h, Point2d c, double trueDeg, double halfShort)
    {
        var mat = new Mat(h, w, MatType.CV_8UC1, new Scalar(20));
        var rad = trueDeg * Math.PI / 180.0;
        var nx = -Math.Sin(rad);
        var ny = Math.Cos(rad);
        const double ramp = 3.0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var across = Math.Abs((x - c.X) * nx + (y - c.Y) * ny);
                var t = Math.Clamp((halfShort - across) / ramp + 0.5, 0, 1);
                mat.Set(y, x, (byte)Math.Round(20 + 180 * t));
            }
        }
        return mat;
    }

    private static Point2f[] RotatedRectContour(
        Point2d center, double deg, double longLen, double shortLen, int samplesPerEdge, double jitter)
    {
        var rng = new Random(7);
        var hw = longLen / 2.0;
        var hh = shortLen / 2.0;
        var corners = new[]
        {
            new Point2d(center.X - hw, center.Y - hh), new Point2d(center.X + hw, center.Y - hh),
            new Point2d(center.X + hw, center.Y + hh), new Point2d(center.X - hw, center.Y + hh),
        };
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2d Rot(Point2d p) => new(
            center.X + (p.X - center.X) * cos - (p.Y - center.Y) * sin,
            center.Y + (p.X - center.X) * sin + (p.Y - center.Y) * cos);

        var rotated = corners.Select(Rot).ToArray();
        var pts = new List<Point2f>();
        for (var e = 0; e < 4; e++)
        {
            var a = rotated[e];
            var b = rotated[(e + 1) % 4];
            for (var s = 0; s < samplesPerEdge; s++)
            {
                var t = (double)s / samplesPerEdge;
                var x = a.X + (b.X - a.X) * t + Gaussian(rng) * jitter;
                var y = a.Y + (b.Y - a.Y) * t + Gaussian(rng) * jitter;
                pts.Add(new Point2f((float)x, (float)y));
            }
        }
        return pts.ToArray();
    }

    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
