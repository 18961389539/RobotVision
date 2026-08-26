using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Geometry;
using Xunit;

namespace RobotVision.Tests;

public class AngleGeometryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(70)]
    [InlineData(120)]
    public void MinAreaRect_ReturnsLongAxisAngle(double rotateDeg)
    {
        var points = RotatedRectPoints(500, 400, 300, 100, rotateDeg);
        var (center, angle) = MinAreaRectGeometry.LongAxis(points);

        Assert.Equal(500, center.X, 1);
        Assert.Equal(400, center.Y, 1);

        // 长边方向，180° 等价（30 与 210 视为同一朝向）
        var delta = Math.Abs(AngleGeometry.NormalizeSignedDeg(angle - AngleGeometry.NormalizeDeg(rotateDeg)));
        Assert.True(delta < 1.0, $"期望 {rotateDeg}(±180 等价)，实际 {angle}");
    }

    [Fact]
    public void FromTwoPoints_CenterIsMidpoint()
    {
        var (center, _) = AngleGeometry.FromTwoPoints(new ImagePoint(10, 20), new ImagePoint(30, 60));
        Assert.Equal(20, center.X, 1e-9);
        Assert.Equal(40, center.Y, 1e-9);
    }

    [Theory]
    [InlineData(0, 0, 10, 0, 0)]      // +X 方向
    [InlineData(0, 0, 0, 10, 90)]     // +Y 方向（图像 y 轴向下）
    [InlineData(0, 0, -10, 0, 180)]   // -X 方向
    [InlineData(10, 10, 0, 0, -135)]  // 对角线
    public void FromTwoPoints_AngleDirection(double ax, double ay, double bx, double by, double expected)
    {
        var (_, angle) = AngleGeometry.FromTwoPoints(new ImagePoint(ax, ay), new ImagePoint(bx, by));
        Assert.Equal(expected, angle, 1e-6);
    }

    [Fact]
    public void NormalizeDeg_MapsTo0To180()
    {
        Assert.Equal(150, AngleGeometry.NormalizeDeg(-30), 1e-9);
        Assert.Equal(90, AngleGeometry.NormalizeDeg(270), 1e-9);
        Assert.Equal(0, AngleGeometry.NormalizeDeg(180), 1e-9);
    }

    [Fact]
    public void NormalizeSignedDeg_MapsToMinus180To180()
    {
        Assert.Equal(-170, AngleGeometry.NormalizeSignedDeg(190), 1e-9);
        Assert.Equal(180, AngleGeometry.NormalizeSignedDeg(180), 1e-9);
        // 约定为 (-180, 180]：-180 与 +180 同方向，归一到 +180
        Assert.Equal(180, AngleGeometry.NormalizeSignedDeg(-180), 1e-9);
        Assert.Equal(-179.5, AngleGeometry.NormalizeSignedDeg(180.5), 1e-9);
    }

    private static Point2f[] RotatedRectPoints(double cx, double cy, double w, double h, double deg)
    {
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        var corners = new (double X, double Y)[]
        {
            (-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2),
        };

        return corners.Select(c => new Point2f(
            (float)(cx + c.X * cos - c.Y * sin),
            (float)(cy + c.X * sin + c.Y * cos))).ToArray();
    }
}
