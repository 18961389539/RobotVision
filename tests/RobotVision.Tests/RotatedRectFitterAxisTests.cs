using OpenCvSharp;
using RobotVision.Tests.HalconBench;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>长轴消歧（warp vs warp+90°）与示教种子回归。</summary>
public sealed class RotatedRectFitterAxisTests
{
    [Theory]
    [InlineData(-18.0)]
    [InlineData(135.0)]
    [InlineData(88.0)]
    public void Fit_WithSeed_ResolvesLongAxis(double trueDeg)
    {
        var contour = RotatedRectContour(240, 180, 200, 50, trueDeg, 40, jitter: 0.35);
        var fit = RotatedRectFitter.Fit(contour, trueDeg);
        Assert.True(fit.Ok);
        var angErr = UndirectedErr(fit.AngleDeg, trueDeg);
        Assert.True(angErr < 0.35, $"θ={trueDeg}° 角误差 {angErr:0.000}");
        Assert.InRange(fit.Center.X, 238, 242);
        Assert.InRange(fit.Center.Y, 178, 182);
        Assert.InRange(fit.LongLen, 196, 204);
        Assert.InRange(fit.ShortLen, 48, 52);
    }

    [Fact]
    public void Fit_minus18_bench_jitter_center_stays_near_truth()
    {
        var contour = RotatedRectBenchSynth.RectContour(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, -18.0,
            RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter: 0.6);
        var fit = RotatedRectFitter.Fit(contour, -18.0, new RectFitOptions { ClipEndPoints = 2 });
        Assert.True(fit.Ok);
        var centerErr = Math.Sqrt(
            Math.Pow(fit.Center.X - RotatedRectBenchSynth.Cx, 2) +
            Math.Pow(fit.Center.Y - RotatedRectBenchSynth.Cy, 2));
        Assert.True(centerErr < 1.5, $"中心误差 {centerErr:0.###} px");
        var truth = RotatedRectSyntheticOracle.From(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, -18.0,
            RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
        var d = RotatedRectSyntheticOracle.Compare(fit, truth);
        Assert.True(d.LongPx < 0.08, $"长边 ΔL={d.LongPx:0.###} px");
        Assert.True(d.ShortPx < 0.08, $"短边 ΔS={d.ShortPx:0.###} px");
    }

    [Fact]
    public void Fit_WithoutSeed_Oblique135_StillRecovers()
    {
        var contour = RotatedRectContour(240, 180, 200, 50, 135.0, 40, jitter: 0.35);
        var fit = RotatedRectFitter.Fit(contour);
        Assert.True(fit.Ok);
        Assert.True(UndirectedErr(fit.AngleDeg, 135.0) < 0.5);
        Assert.True(Math.Abs(fit.Center.X - 240) < 2 && Math.Abs(fit.Center.Y - 180) < 2);
    }

    private static double UndirectedErr(double got, double truth)
    {
        var d = Math.Abs(got - truth);
        return Math.Min(d, 180 - d);
    }

    private static Point2f[] RotatedRectContour(
        double cx, double cy, double longLen, double shortLen, double deg, int samplesPerEdge, double jitter = 0)
    {
        var rng = new Random(17);
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
            for (var s = 0; s < samplesPerEdge; s++)
            {
                var t = (double)s / samplesPerEdge;
                pts.Add(new Point2f(
                    (float)(a.X + (b.X - a.X) * t + Gaussian(rng) * jitter),
                    (float)(a.Y + (b.Y - a.Y) * t + Gaussian(rng) * jitter)));
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
