using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.WpfHost;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 标定档案管理页测试：四类档案（内参/外参/旋转中心/多项式）的展示行映射、
/// 质量文本（优秀/可用/超标）、空状态摘要。
/// </summary>
public class CalibrationViewModelTests
{
    [Fact]
    public void Ctor_WithEmptyManager_ShowsEmptyState()
    {
        var calibration = new CalibrationManager();
        var cfg = TestInfra.CreateAppConfig(System.IO.Path.GetTempPath());

        var vm = new CalibrationViewModel(calibration, cfg);

        vm.Intrinsics.Should().BeEmpty();
        vm.Extrinsics.Should().BeEmpty();
        vm.RotationCenters.Should().BeEmpty();
        vm.Polynomials.Should().BeEmpty();
        vm.Message.Should().Contain("内参 0 · 外参 0 · 旋转中心 0 · 多项式 0");
    }

    [Fact]
    public void Refresh_MapsIntrinsicProfiles()
    {
        var calibration = new CalibrationManager();
        calibration.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 1280,
            Height = 960,
            CameraMatrix = [100, 0, 640, 0, 100, 480, 0, 0, 1],
            DistCoeffs = [0.1, 0, 0, 0, 0],
            Rms = 0.12,
            CalibratedAt = new DateTime(2026, 8, 1, 10, 30, 0),
        });
        var cfg = TestInfra.CreateAppConfig(System.IO.Path.GetTempPath());

        var vm = new CalibrationViewModel(calibration, cfg);

        vm.Intrinsics.Should().ContainSingle();
        var row = vm.Intrinsics[0];
        row.CameraId.Should().Be("cam1");
        row.Resolution.Should().Be("1280×960");
        row.Rms.Should().Be("0.120 px");
        row.Quality.Should().Be("优秀"); // RMS 0.12 ≤ 0.3
        row.CalibratedAt.Should().Contain("2026-08-01");
    }

    [Fact]
    public void Refresh_MapsExtrinsicProfiles()
    {
        var calibration = new CalibrationManager();
        calibration.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Affine = [1, 0, 10, 0, 1, 20],
            Rms = 0.05,
            MaxResidual = 0.08,
            CalibratedAt = new DateTime(2026, 8, 2, 9, 0, 0),
        });
        var cfg = TestInfra.CreateAppConfig(System.IO.Path.GetTempPath());

        var vm = new CalibrationViewModel(calibration, cfg);

        vm.Extrinsics.Should().ContainSingle();
        vm.Extrinsics[0].StationId.Should().Be("st1");
        vm.Extrinsics[0].Quality.Should().Be("优秀"); // 残差 ≤ 0.1
    }

    [Fact]
    public void Refresh_MapsRotationCenterProfiles()
    {
        var calibration = new CalibrationManager();
        calibration.LoadRotationCenter(new RotationCenterProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Cx = 640.5,
            Cy = 480.5,
            RadiusPx = 42.1,
            Rms = 0.35,
            AxisRatio = 1.05,
            PointCount = 8,
            CalibratedAt = new DateTime(2026, 8, 3, 14, 0, 0),
        });
        var cfg = TestInfra.CreateAppConfig(System.IO.Path.GetTempPath());

        var vm = new CalibrationViewModel(calibration, cfg);

        vm.RotationCenters.Should().ContainSingle();
        vm.RotationCenters[0].Center.Should().Be("(640.5, 480.5) px");
        vm.RotationCenters[0].Radius.Should().Be("42.1 px");
        vm.RotationCenters[0].AxisRatio.Should().Be("1.050");
        vm.RotationCenters[0].PointCount.Should().Be("8");
    }

    [Fact]
    public void Refresh_MapsPolynomialProfiles_WithCoordinateSpace()
    {
        var calibration = new CalibrationManager();
        calibration.LoadPolynomial(new PolynomialProfile
        {
            StationId = "st_poly",
            CameraId = "cam1",
            Width = 1280,
            Height = 960,
            Order = 2,
            CoefX = [1, 0, 0, 0, 1, 0],
            CoefY = [0, 1, 0, 1, 0, 0],
            PointCount = 12,
            CoordinateSpace = PolynomialCoordinateSpace.Image,
            Rms = 0.08,
            MaxResidual = 0.15,
            CalibratedAt = new DateTime(2026, 8, 4, 16, 0, 0),
        });
        var cfg = TestInfra.CreateAppConfig(System.IO.Path.GetTempPath());

        var vm = new CalibrationViewModel(calibration, cfg);

        vm.Polynomials.Should().ContainSingle();
        vm.Polynomials[0].StationId.Should().Be("st_poly");
        vm.Polynomials[0].Resolution.Should().Be("1280×960");
        vm.Polynomials[0].Order.Should().Be("2 阶");
        vm.Polynomials[0].Space.Should().Be("棋盘毫米系");
        vm.Polynomials[0].Quality.Should().Be("可用"); // MaxResidual 0.15 ≤ 0.5 可用上限
    }

    [Fact]
    public void Refresh_MarksPoorQualityIntrinsic()
    {
        var calibration = new CalibrationManager();
        calibration.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam_bad",
            Width = 640,
            Height = 480,
            CameraMatrix = [100, 0, 320, 0, 100, 240, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
            Rms = 0.8,
        });
        var cfg = TestInfra.CreateAppConfig(System.IO.Path.GetTempPath());

        var vm = new CalibrationViewModel(calibration, cfg);

        vm.Intrinsics.Should().ContainSingle();
        vm.Intrinsics[0].Quality.Should().Be("超标"); // RMS 0.8 > 0.5
    }
}
