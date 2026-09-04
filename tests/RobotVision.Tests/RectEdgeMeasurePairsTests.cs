using OpenCvSharp;
using RobotVision.Tests.HalconBench;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>measure_pairs / measure_pos 剖面取边单测（Vision InternalsVisibleTo）。</summary>
public sealed class RectEdgeMeasurePairsTests
{
    [Fact]
    public void MeasurePair_RecoversWidth_OnBrightStripeProfile()
    {
        using var gray = Stripe(320, 240, new Point2d(160, 120), 25.0, halfShort: 30);
        var rad = 25.0 * Math.PI / 180.0;
        var nx = -Math.Sin(rad);
        var ny = Math.Cos(rad);
        var bx = 160.0;
        var by = 120.0;
        var dirX = Math.Cos(rad);
        var dirY = Math.Sin(rad);

        Assert.True(RectEdgeSampler.TryMeasurePair(
            gray, bx, by, dirX, dirY, nx, ny, halfSpan: 30, search: 8,
            RectEdgePolarity.Any, RectEdgeMeasureMode.Sharp,
            out var offNeg, out var offPos));

        var width = offPos - offNeg;
        Assert.InRange(width, 58, 62);
        Assert.True(offNeg < -20 && offPos > 20);
    }

    [Fact]
    public void MeasurePair_Fuzzy_BetterOnBlurredStripe()
    {
        using var sharp = Stripe(320, 240, new Point2d(160, 120), 0, halfShort: 28);
        using var gray = new Mat();
        Cv2.GaussianBlur(sharp, gray, new Size(7, 7), 0);

        var rad = 0.0;
        var nx = -Math.Sin(rad);
        var ny = Math.Cos(rad);
        var bx = 160.0;
        var by = 120.0;

        var sharpOk = RectEdgeSampler.TryMeasurePair(
            gray, bx, by, 1, 0, nx, ny, 28, 10,
            RectEdgePolarity.Any, RectEdgeMeasureMode.Sharp,
            out var sNeg, out var sPos);
        var fuzzyOk = RectEdgeSampler.TryMeasurePair(
            gray, bx, by, 1, 0, nx, ny, 28, 10,
            RectEdgePolarity.Any, RectEdgeMeasureMode.Fuzzy,
            out var fNeg, out var fPos);

        Assert.True(sharpOk && fuzzyOk);
        var sharpWidth = sPos - sNeg;
        var fuzzyWidth = fPos - fNeg;
        Assert.InRange(fuzzyWidth, 54, 58);
        Assert.True(Math.Abs(fuzzyWidth - 56) <= Math.Abs(sharpWidth - 56) + 1.0);
    }

    [Fact]
    public void FullPipeline_MatchesSyntheticOracle()
    {
        const double trueDeg = 22.0;
        var truth = RotatedRectSyntheticOracle.From(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, trueDeg,
            RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
        using var gray = RotatedRectBenchSynth.Rectangle(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, trueDeg,
            RotatedRectBenchSynth.Long / 2, RotatedRectBenchSynth.Short / 2);
        var contour = RotatedRectBenchSynth.RectContour(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, trueDeg,
            RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter: 0.5);
        var opt = new RectFitOptions { StripTabProtrusion = false };
        var full = RotatedRectPipeline.Fit(contour, gray, trueDeg, opt);
        var delta = RotatedRectSyntheticOracle.Compare(full, truth);
        RotatedRectSyntheticOracle.AssertHalconGrade(delta, "full_pipeline");
    }

    private static Mat Stripe(int w, int h, Point2d c, double trueDeg, double halfShort)
    {
        var mat = new Mat(h, w, MatType.CV_8UC1, new Scalar(20));
        var rad = trueDeg * Math.PI / 180.0;
        var nx = -Math.Sin(rad);
        var ny = Math.Cos(rad);
        const double ramp = 3.0;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var across = Math.Abs((x - c.X) * nx + (y - c.Y) * ny);
            var t = Math.Clamp((halfShort - across) / ramp + 0.5, 0, 1);
            mat.Set(y, x, (byte)Math.Round(20 + 180 * t));
        }
        return mat;
    }
}
