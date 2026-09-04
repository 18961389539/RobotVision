using OpenCvSharp;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>LineFit 亚像素卡尺重拟合单测（经 <see cref="LineFitRefine"/> 内部入口）。</summary>
public sealed class LineFitSubpixelTests
{
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

    [Fact]
    public void RecoversTrueAngle_FromSkewedSeed()
    {
        const double trueDeg = 20.0;
        var center = new Point2d(160, 120);
        using var gray = Stripe(320, 240, center, trueDeg, halfShort: 30);

        var rad = trueDeg * Math.PI / 180.0;
        var seedCenter = new Point2d(center.X - Math.Sin(rad), center.Y + Math.Cos(rad));
        var r = LineFitRefine.TrySubpixel(gray, seedCenter, longLen: 140, shortLen: 60, seedAngleDeg: 21.5);

        Assert.NotNull(r);
        Assert.True(Math.Abs(r!.Value.AngleDeg - trueDeg) < 0.2, $"角误差 {r.Value.AngleDeg}");
        var cx = r.Value.Center.X - center.X;
        var cy = r.Value.Center.Y - center.Y;
        Assert.True(cx * cx + cy * cy < 0.6 * 0.6, $"中心误差 {Math.Sqrt(cx * cx + cy * cy):0.000}");
        Assert.True(r.Value.MaxParallelDeg < 1.0);
    }

    [Fact]
    public void ImprovesPrecision_VersusSeed()
    {
        var center = new Point2d(160, 120);
        using var gray = Stripe(320, 240, center, 168.0, halfShort: 28);
        var r = LineFitRefine.TrySubpixel(gray, center, longLen: 150, shortLen: 56, seedAngleDeg: 169.5);
        Assert.NotNull(r);
        Assert.True(Math.Abs(((r!.Value.AngleDeg - 168.0 + 90) % 180) - 90) < 0.3);
    }

    [Fact]
    public void UniformImage_NoEdge_ReturnsNull()
    {
        using var gray = new Mat(200, 200, MatType.CV_8UC1, new Scalar(128));
        Assert.Null(LineFitRefine.TrySubpixel(gray, new Point2d(100, 100), 120, 40, 0));
    }

    [Fact]
    public void TooSmallTarget_ReturnsNull()
    {
        using var gray = Stripe(200, 200, new Point2d(100, 100), 0, 12);
        Assert.Null(LineFitRefine.TrySubpixel(gray, new Point2d(100, 100), longLen: 16, shortLen: 24, seedAngleDeg: 0));
    }

    [Fact]
    public void NonGrayInput_ReturnsNull()
    {
        using var bgr = new Mat(100, 100, MatType.CV_8UC3, new Scalar(10, 20, 30));
        Assert.Null(LineFitRefine.TrySubpixel(bgr, new Point2d(50, 50), 60, 30, 0));
    }
}
