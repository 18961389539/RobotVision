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

    [Fact]
    public void Apply_ToolOffset_CorrectsPositionAndOutputAngle()
    {
        // 工具零位偏角 δ=10°：第 4 轴转角 φ = θ − δ = 80°，
        // 位置 P' = C + R(δ−θ)·(P−C) = C + R(−80°)·(50,0)
        var pose = new RobotPose(150, 100, 90); // P=(150,100)，θ=90°，C=(100,100)

        var result = RotationCenterCompensation.Apply(pose, 100, 100, toolOffsetDeg: 10);

        // R(−80°)·(50,0) = (50cos80°, −50sin80°) ≈ (8.68, −49.24)
        Assert.Equal(108.68, result.X, 2);
        Assert.Equal(50.76, result.Y, 2);
        Assert.Equal(80, result.AngleDeg, 6); // 输出第 4 轴角 = θ − δ
    }

    [Fact]
    public void Apply_ToolOffset_PhysicalClosure_ToolTipLandsOnPart()
    {
        // 物理闭环验证：工具零位偏 δ、偏心距 r。第 4 轴在 0 位时工具尖 T0 = C + r·dir(δ)。
        // 机器人到 P'（补偿输出）并转 RZ 到 φ 后，工具尖应回到零件位置 P。
        // 用 P = T0 本身（第 4 轴零位时工具尖正对的位置，θ=δ）验证：
        var cx = 100.0;
        var cy = 100.0;
        var delta = 25.0; // δ
        var r = 40.0;
        var t0 = (X: cx + r * Math.Cos(delta * Math.PI / 180.0),
                  Y: cy + r * Math.Sin(delta * Math.PI / 180.0));
        var pose = new RobotPose(t0.X, t0.Y, delta); // 零件角 θ=δ → φ=0

        var result = RotationCenterCompensation.Apply(pose, cx, cy, delta);

        // φ=0：命令位置应等于 T0（不转）
        Assert.Equal(t0.X, result.X, 6);
        Assert.Equal(t0.Y, result.Y, 6);
        Assert.Equal(0, result.AngleDeg, 6);
    }

    [Fact]
    public void Apply_ToolOffsetNegative_WrapsOutputAngle()
    {
        // θ=10°、δ=170° → φ=−160°；输出角归一化到 (-180,180]
        var pose = new RobotPose(200, 100, 10);

        var result = RotationCenterCompensation.Apply(pose, 100, 100, toolOffsetDeg: 170);

        Assert.Equal(-160, result.AngleDeg, 6);
    }
}
