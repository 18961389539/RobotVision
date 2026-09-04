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
    public void CanonWarpDeg_MapsToMinus90To90()
    {
        Assert.Equal(-37, AngleGeometry.CanonWarpDeg(143), 1e-9);
        Assert.Equal(-8.7, AngleGeometry.CanonWarpDeg(171.3), 1e-9);
        Assert.Equal(37, AngleGeometry.CanonWarpDeg(37), 1e-9);
        Assert.Equal(-20, AngleGeometry.CanonWarpDeg(-20), 1e-9);
        Assert.Equal(-90, AngleGeometry.CanonWarpDeg(90), 1e-9);
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

    [Theory]
    [InlineData(10, 10, 10)]
    [InlineData(10, -170, -170)]
    [InlineData(0, 180, 180)]
    [InlineData(30, 29.5, 30)]
    public void FuseDirected_GeometryPlusTemplateOrientation(double geo, double tpl, double expected)
    {
        var fused = AngleGeometry.FuseDirected(geo, tpl);
        Assert.True(Math.Abs(AngleGeometry.NormalizeSignedDeg(fused - expected)) < 0.01,
            $"geo={geo} tpl={tpl} → {fused}，期望 {expected}");
    }

    [Theory]
    [InlineData(162, -18, 0)]
    [InlineData(22, 112, -90)]
    public void SignedDeltaHalfDeg_AvoidsLongAxisFlip(double to, double from, double expected)
    {
        Assert.Equal(expected, AngleGeometry.SignedDeltaHalfDeg(to, from), 1e-9);
    }

    [Fact]
    public void UndirectedDeltaDeg_CapsAt90()
    {
        Assert.Equal(0, AngleGeometry.UndirectedDeltaDeg(10, 10), 1e-9);
        Assert.Equal(10, AngleGeometry.UndirectedDeltaDeg(0, 10), 1e-9);
        Assert.Equal(20, AngleGeometry.UndirectedDeltaDeg(10, 170), 1e-9);
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

    [Fact]
    public void CircularStdDeg_IdenticalAngles_NearZero()
    {
        var std = AngleGeometry.CircularStdDeg([10, 10, 10.2, 9.8]);
        Assert.True(std < 0.3, $"σ={std:0.00}");
    }

    [Fact]
    public void CircularStdDeg_OppositeDirected_IsLarge()
    {
        var std = AngleGeometry.CircularStdDeg([0, 180], period: 360);
        Assert.True(std > 40, $"0°/180° 应对头，σ={std:0.00}");
    }

    [Fact]
    public void CircularStdDeg_Undirected_Treats0And180AsSame()
    {
        var std = AngleGeometry.CircularStdDeg([0, 179.5], period: 180);
        Assert.True(std < 2, $"无向 0/180 应接近，σ={std:0.00}");
    }
}
