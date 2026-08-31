using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Infrastructure.Calibration;
using Xunit;

namespace RobotVision.Tests;

public class RotationCenterCalibratorTests
{
    private static Point2f[] CirclePoints(double cx, double cy, double r, params double[] anglesDeg) =>
        anglesDeg.Select(a =>
        {
            var rad = a * Math.PI / 180.0;
            return new Point2f(
                (float)(cx + r * Math.Cos(rad)),
                (float)(cy + r * Math.Sin(rad)));
        }).ToArray();

    [Fact]
    public void Calibrate_EightPointsOnCircle_RecoversCenter()
    {
        var points = CirclePoints(512, 384, 80, 0, 45, 90, 135, 180, 225, 270, 315);

        var profile = RotationCenterCalibrator.Calibrate("st1", "cam1", points);

        Assert.Equal(512, profile.Cx, 1);
        Assert.Equal(384, profile.Cy, 1);
        Assert.Equal(80, profile.RadiusPx, 1);
        Assert.InRange(profile.AxisRatio, 0.99, 1.01);
        Assert.True(profile.Rms < 0.1);
        Assert.Equal(8, profile.PointCount);
    }

    [Fact]
    public void Calibrate_ThreePoints120Apart_RecoversCircumcenter()
    {
        var points = CirclePoints(100, 80, 50, 0, 120, 240);

        var profile = RotationCenterCalibrator.Calibrate("st1", "cam1", points);

        Assert.Equal(100, profile.Cx, 2);
        Assert.Equal(80, profile.Cy, 2);
        Assert.Equal(50, profile.RadiusPx, 2);
        Assert.True(profile.Rms < 0.01, $"3 点精确外接圆残差应为 0，实际 {profile.Rms}");
    }

    [Fact]
    public void Calibrate_SmallArc_Throws()
    {
        // 角度跨度仅 10°：三点近共线，圆拟合病态
        var points = CirclePoints(200, 200, 100, -5, 0, 5);

        Assert.Throws<VisionException>(() =>
            RotationCenterCalibrator.Calibrate("st1", "cam1", points));
    }

    [Fact]
    public void Calibrate_IdenticalPoints_Throws()
    {
        var points = new[] { new Point2f(50, 50), new Point2f(50, 50), new Point2f(50, 50) };

        Assert.Throws<VisionException>(() =>
            RotationCenterCalibrator.Calibrate("st1", "cam1", points));
    }

    [Fact]
    public void Calibrate_TooFewPoints_Throws()
    {
        var points = new[] { new Point2f(10, 10), new Point2f(20, 20) };

        Assert.Throws<VisionException>(() =>
            RotationCenterCalibrator.Calibrate("st1", "cam1", points));
    }

    [Fact]
    public void Calibrate_TinyRadius_Rejected()
    {
        // 半径 3px 的"圆"：跨度 6px 通过绝对退化检查，但偏心量可忽略且轴心对噪声极敏感——拒绝
        var points = CirclePoints(100, 100, 3, 0, 45, 90, 135, 180);

        var ex = Assert.Throws<VisionException>(() =>
            RotationCenterCalibrator.Calibrate("st1", "cam1", points));
        Assert.Contains("半径", ex.Message, StringComparison.Ordinal);
        Assert.Contains("过小", ex.Message, StringComparison.Ordinal);
    }
}
