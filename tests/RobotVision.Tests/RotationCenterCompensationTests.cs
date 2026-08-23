using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using Xunit;

namespace RobotVision.Tests;

public class RotationCenterCompensationTests
{
    [Fact]
    public void Rotate_AroundOrigin90Deg_RotatesCounterClockwise()
    {
        var (x, y) = RotationCenterCompensation.Rotate(10, 0, 0, 0, 90);
        Assert.Equal(0, x, 6);
        Assert.Equal(10, y, 6);
    }

    [Fact]
    public void Rotate_NegativeAngle_RotatesClockwise()
    {
        var (x, y) = RotationCenterCompensation.Rotate(10, 0, 0, 0, -90);
        Assert.Equal(0, x, 6);
        Assert.Equal(-10, y, 6);
    }

    [Fact]
    public void Rotate_ArbitraryCenter180Deg()
    {
        var (x, y) = RotationCenterCompensation.Rotate(11, 12, 1, 2, 180);
        Assert.Equal(-9, x, 6);
        Assert.Equal(-8, y, 6);
    }

    [Fact]
    public void Rotate_ZeroDelta_IsIdentity()
    {
        var (x, y) = RotationCenterCompensation.Rotate(123.4, -56.7, 8, 9, 0);
        Assert.Equal(123.4, x, 9);
        Assert.Equal(-56.7, y, 9);
    }

    [Fact]
    public void Apply_EccentricTool_PositionCounterRotates_AngleKept()
    {
        // 零件在轴心右侧 50mm，角度 90°：命令位置应反转到轴心下方 50mm，角度仍为 90°
        var pose = new RobotPose(150, 100, 90);

        var result = RotationCenterCompensation.Apply(pose, 100, 100);

        Assert.Equal(100, result.X, 6);
        Assert.Equal(50, result.Y, 6);
        Assert.Equal(90, result.AngleDeg, 6);
    }

    [Fact]
    public void Apply_ZeroAngle_IsIdentity()
    {
        var pose = new RobotPose(150, 100, 0);

        var result = RotationCenterCompensation.Apply(pose, 100, 100);

        Assert.Equal(150, result.X, 9);
        Assert.Equal(100, result.Y, 9);
        Assert.Equal(0, result.AngleDeg, 9);
    }

    [Fact]
    public void Apply_PartOnAxis_PositionUnchanged()
    {
        // 零件恰好在轴心上：偏心补偿不改变位置
        var pose = new RobotPose(100, 100, 137.5);

        var result = RotationCenterCompensation.Apply(pose, 100, 100);

        Assert.Equal(100, result.X, 6);
        Assert.Equal(100, result.Y, 6);
        Assert.Equal(137.5, result.AngleDeg, 6);
    }
}
