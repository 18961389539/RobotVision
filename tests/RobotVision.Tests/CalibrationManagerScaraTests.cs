using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using Xunit;

namespace RobotVision.Tests;

/// <summary>SCARA 场景：安装模式 / 方向自检 / 工具零位偏角。</summary>
public class CalibrationManagerScaraTests
{
    [Fact]
    public void ValidateExtrinsic_IllegalMountType_Throws()
    {
        var ex = Assert.Throws<VisionException>(() =>
        {
            var m = new CalibrationManager();
            m.LoadExtrinsic(new ExtrinsicProfile
            {
                StationId = "st1",
                CameraId = "cam1",
                Affine = [1, 0, 0, 0, 1, 0],
                MountType = "Flying",
            });
        });
        Assert.Contains("MountType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateExtrinsic_OnArmDefaultFields_Accepted()
    {
        var m = new CalibrationManager();
        m.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_fixed", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        m.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_arm", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MountType = "OnArm", TeachTcpX = 100.5, TeachTcpY = -20.25, TeachRzDeg = 45,
            CalibrationPlaneZ = 12.5,
        });

        Assert.Equal(2, m.ExtrinsicCount);
    }

    [Fact]
    public void VerifyRotationDirection_ConsistentAngles_Pass()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });

        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(a * Math.PI / 180.0)))
            .ToArray();

        manager.VerifyRotationDirection("st1", rc, points, angles);
    }

    [Fact]
    public void VerifyRotationDirection_ReversedAngles_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });

        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(-a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(-a * Math.PI / 180.0)))
            .ToArray();

        var ex = Assert.Throws<VisionException>(
            () => manager.VerifyRotationDirection("st1", rc, points, angles));
        Assert.Contains("方向自检失败", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyRotationDirection_MirroredExtrinsic_StillDetectsMismatch()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [-1, 0, 300, 0, 1, 0],
        });

        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(-a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(-a * Math.PI / 180.0)))
            .ToArray();

        manager.VerifyRotationDirection("st1", rc, points, angles);
    }

    [Fact]
    public void VerifyRotationDirection_WithoutExtrinsic_Throws()
    {
        var manager = new CalibrationManager();
        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };

        Assert.Throws<VisionException>(() =>
            manager.VerifyRotationDirection("st1", rc,
                [new(150, 100), new(100, 150), new(50, 100)], [0, 45, 90]));
    }

    [Fact]
    public void VerifyRotationDirection_WithPolynomial_Pass()
    {
        var manager = new CalibrationManager();
        manager.LoadPolynomial(new PolynomialProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Width = 200,
            Height = 200,
            Order = 2,
            CoefX = [100, 100, 0, 0, 0, 0],
            CoefY = [100, 0, 0, 100, 0, 0],
        });

        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(a * Math.PI / 180.0)))
            .ToArray();

        manager.VerifyRotationDirection("st1", rc, points, angles);
    }

    [Fact]
    public void CompensateRotation_ToolOffsetAppliedFromProfile()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        manager.LoadRotationCenter(new RotationCenterProfile
        {
            StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50,
            ToolOffsetDeg = 10,
        });

        var result = manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool,
            new RobotPose(150, 100, 90));

        Assert.Equal(80, result.AngleDeg, 6);
        Assert.Equal(108.68, result.X, 2);
        Assert.Equal(50.76, result.Y, 2);
    }
}
