using FluentAssertions;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.WpfHost.Features.Calibration;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 标定档案管理页测试：五类档案（内参/外参/旋转中心/多项式/比例）的展示行映射、
/// 质量文本（优秀/可用/超标/近似）、空状态摘要，以及比例档案的校验与录入。
/// </summary>
public class CalibrationViewModelTests
{
    private static CalibrationViewModel CreateVm(CalibrationManager calibration)
    {
        var vm = new CalibrationViewModel(
            TestInfra.CalibrationFacade(calibration), TestInfra.CreateAppConfig(System.IO.Path.GetTempPath()),
            TestInfra.CameraFacade(new CameraManager()), new TestDialogService(), TestLog.Null<CalibrationViewModel>());
        vm.Refresh();
        return vm;
    }

    [Fact]
    public void Ctor_WithEmptyManager_ShowsEmptyState()
    {
        var vm = CreateVm(new CalibrationManager());

        vm.Intrinsics.Should().BeEmpty();
        vm.Extrinsics.Should().BeEmpty();
        vm.RotationCenters.Should().BeEmpty();
        vm.Polynomials.Should().BeEmpty();
        vm.Scales.Should().BeEmpty();
        vm.Message.Should().Contain("内参 0 · 外参 0 · 旋转中心 0 · 多项式 0 · 比例 0");
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

        var vm = CreateVm(calibration);

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

        var vm = CreateVm(calibration);

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

        var vm = CreateVm(calibration);

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

        var vm = CreateVm(calibration);

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

        var vm = CreateVm(calibration);

        vm.Intrinsics.Should().ContainSingle();
        vm.Intrinsics[0].Quality.Should().Be("超标"); // RMS 0.8 > 0.5
    }

    // ---- 比例标定（手动录入）：档案映射、校验、各向异性质量评估 ----

    [Fact]
    public void Refresh_MapsScaleProfiles_WithFieldOfView()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.05,
            Width = 2448,
            Height = 2048,
            CalibratedAt = new DateTime(2026, 8, 5, 8, 0, 0),
        });

        var vm = CreateVm(calibration);

        vm.Scales.Should().ContainSingle();
        var row = vm.Scales[0];
        row.StationId.Should().Be("st1");
        row.CameraId.Should().Be("cam1");
        row.ScaleX.Should().BeApproximately(0.05, 1e-12);
        row.ScaleY.Should().BeApproximately(0.05, 1e-12);
        row.Resolution.Should().Be("2448×2048");
        row.FieldOfView.Should().Be("122.4 × 102.4 mm"); // 2448×0.05 × 2048×0.05
        row.Quality.Should().Be("可用"); // X=Y 无各向异性
    }

    [Fact]
    public void Refresh_MarksAnisotropicScaleAsApproximate()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st_skew",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.06, // 差 20%，疑似旋转/透视/畸变
            Width = 2448,
            Height = 2048,
        });

        var vm = CreateVm(calibration);

        vm.Scales[0].Quality.Should().Be("近似");
        calibration.QualityWarnings.Should().Contain(w => w.Contains("各向异性", StringComparison.Ordinal));
    }

    [Fact]
    public void Refresh_ScaleWithoutResolution_ShowsUnrecorded()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.05,
        });

        var vm = CreateVm(calibration);

        vm.Scales[0].Resolution.Should().Be("未记录");
        vm.Scales[0].FieldOfView.Should().Be("-");
    }

    [Theory]
    [InlineData("", "cam1", 0.05, 0.05)]   // 空 StationId
    [InlineData("st1", "", 0.05, 0.05)]    // 空 CameraId
    [InlineData("st1", "cam1", 0, 0.05)]   // 比例 0
    [InlineData("st1", "cam1", 0.05, -1)]  // 负比例
    [InlineData("st1", "cam1", double.NaN, 0.05)]
    public void ValidateScale_RejectsInvalidProfiles(string stationId, string cameraId, double scaleX, double scaleY)
    {
        var profile = new ScaleProfile { StationId = stationId, CameraId = cameraId, ScaleX = scaleX, ScaleY = scaleY };

        var act = () => CalibrationManager.ValidateScale(profile);

        act.Should().Throw<RobotVision.Core.VisionException>();
    }

    [Fact]
    public void GetScale_ReturnsNullForMissingOrEmptyStation()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile { StationId = "st1", CameraId = "cam1", ScaleX = 0.05, ScaleY = 0.05 });

        calibration.GetScale("st1").Should().NotBeNull();
        calibration.GetScale("nope").Should().BeNull();
        calibration.GetScale(null).Should().BeNull();
        calibration.GetScale("").Should().BeNull();
    }

    [Fact]
    public void SaveScale_FlushesPendingEditsBeforeValidate()
    {
        using var dir = new TestInfra.TempDir("scale_flush");
        var calibration = new CalibrationManager();
        var calibFolder = System.IO.Path.Combine(dir.Path, "calibration");
        calibration.LoadDirectory(calibFolder);
        var vm = new CalibrationViewModel(
            TestInfra.CalibrationFacade(calibration), TestInfra.CreateAppConfig(dir.Path),
            TestInfra.CameraFacade(new CameraManager()), new TestDialogService(), TestLog.Null<CalibrationViewModel>());

        vm.ScaleStationId = "st1";
        vm.ScaleCameraId = "cam1";
        vm.ScaleWidth = 5484;
        vm.ScaleHeight = 3660;
        vm.ScaleX = 0;
        vm.ScaleY = 0;
        vm.FlushPendingEdits = () =>
        {
            vm.ScaleX = 0.1;
            vm.ScaleY = 0.1;
        };

        vm.SaveScaleCommand.Execute(null);

        vm.ScaleFormMessage.Should().Contain("已保存");
        calibration.GetScale("st1")!.ScaleX.Should().BeApproximately(0.1, 1e-12);
    }

    [Fact]
    public void SaveScale_WritesProfileFile_AndLoads()
    {
        using var dir = new TestInfra.TempDir("scale_save");
        var calibration = new CalibrationManager();
        var calibFolder = System.IO.Path.Combine(dir.Path, "calibration");
        calibration.LoadDirectory(calibFolder);
        var cfg = TestInfra.CreateAppConfig(dir.Path);
        var vm = new CalibrationViewModel(
            TestInfra.CalibrationFacade(calibration), cfg,
            TestInfra.CameraFacade(new CameraManager()), new TestDialogService(), TestLog.Null<CalibrationViewModel>());

        vm.ScaleStationId = "st_manual";
        vm.ScaleCameraId = "cam1";
        vm.ScaleX = 0.05;
        vm.ScaleY = 0.05;
        vm.ScaleWidth = 2448;
        vm.ScaleHeight = 2048;
        vm.SaveScaleCommand.Execute(null);

        vm.ScaleFormMessage.Should().Contain("已保存");
        vm.Scales.Should().ContainSingle().Which.StationId.Should().Be("st_manual");
        System.IO.File.Exists(System.IO.Path.Combine(calibFolder, "st_manual.scale.json"))
            .Should().BeTrue("档案应按 {工位}.scale.json 落盘");
    }

    [Fact]
    public async Task GrabScalePreview_WithoutCamera_ShowsError()
    {
        var vm = CreateVm(new CalibrationManager());

        await vm.GrabScalePreviewCommand.ExecuteAsync(null);

        vm.ScaleFormMessageIsError.Should().BeTrue();
        vm.ScaleFormMessage.Should().Contain("请先选择相机");
        vm.ScalePreviewImage.Should().BeNull();
        vm.CanUseScaleGrab.Should().BeFalse();
    }

    [Fact]
    public async Task ReadCameraResolution_FileCamera_PopulatesResolution()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rv_scale_file_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        using (var img = new OpenCvSharp.Mat(800, 600, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(90)))
            OpenCvSharp.Cv2.ImWrite(System.IO.Path.Combine(folder, "f.bmp"), img);

        var cameras = new CameraManager();
        cameras.Register(new FileCamera("cam_file", folder));
        var vm = new CalibrationViewModel(
            TestInfra.CalibrationFacade(new CalibrationManager()),
            TestInfra.CreateAppConfig(System.IO.Path.GetTempPath()),
            TestInfra.CameraFacade(cameras), new TestDialogService(), TestLog.Null<CalibrationViewModel>());
        vm.ScaleCameraId = "cam_file";

        await vm.ReadCameraResolutionCommand.ExecuteAsync(null);

        vm.ScaleWidth.Should().Be(600);
        vm.ScaleHeight.Should().Be(800);
        vm.ScaleFormMessage.Should().Contain("600×800");
        vm.CanUseScaleGrab.Should().BeTrue();
    }

    [Fact]
    public void FailedFileCamera_ShowsGrabHint_AndDisablesButtons()
    {
        var cameras = new CameraManager();
        cameras.Register(new FailedCamera("cam_file", CameraKind.File, "回放目录中没有图片: data/replay"));
        var vm = new CalibrationViewModel(
            TestInfra.CalibrationFacade(new CalibrationManager()),
            TestInfra.CreateAppConfig(System.IO.Path.GetTempPath()),
            TestInfra.CameraFacade(cameras), new TestDialogService(), TestLog.Null<CalibrationViewModel>());
        vm.ScaleCameraId = "cam_file";

        vm.ScaleCameraGrabHint.Should().Contain("回放目录中没有图片");
        vm.CanUseScaleGrab.Should().BeFalse();
    }

    [Fact]
    public async Task GrabScalePreview_PopulatesImageAndResolution()
    {
        var cameras = new CameraManager();
        cameras.Register(new VirtualCamera("cam_v", 640, 480, "Bars"));
        var vm = new CalibrationViewModel(
            TestInfra.CalibrationFacade(new CalibrationManager()),
            TestInfra.CreateAppConfig(System.IO.Path.GetTempPath()),
            TestInfra.CameraFacade(cameras), new TestDialogService(), TestLog.Null<CalibrationViewModel>());
        vm.ScaleCameraId = "cam_v";

        await vm.GrabScalePreviewCommand.ExecuteAsync(null);

        vm.ScalePreviewImage.Should().NotBeNull();
        vm.ScaleWidth.Should().Be(640);
        vm.ScaleHeight.Should().Be(480);
        vm.ScalePreviewCaption.Should().Contain("640×480");
        vm.ScaleFormMessage.Should().Contain("已取图");
    }

    [Fact]
    public void SelectedScale_LoadsFormFields()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.06,
            Width = 2448,
            Height = 2048,
        });
        var vm = CreateVm(calibration);

        vm.SelectedScale = vm.Scales[0];

        vm.ScaleStationId.Should().Be("st1");
        vm.ScaleCameraId.Should().Be("cam1");
        vm.ScaleX.Should().BeApproximately(0.05, 1e-12);
        vm.ScaleY.Should().BeApproximately(0.06, 1e-12);
    }

    [Fact]
    public void LoadScaleToForm_FillsTopFormFromCard()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.06,
            Width = 2448,
            Height = 2048,
        });
        var vm = CreateVm(calibration);

        vm.LoadScaleToFormCommand.Execute(vm.Scales[0]);

        vm.ScaleStationId.Should().Be("st1");
        vm.ScaleCameraId.Should().Be("cam1");
        vm.ScaleWidth.Should().Be(2448);
        vm.ScaleHeight.Should().Be(2048);
        vm.ScaleX.Should().BeApproximately(0.05, 1e-12);
        vm.ScaleY.Should().BeApproximately(0.06, 1e-12);
        vm.ScaleFormMessage.Should().Contain("已载入");
    }

    [Fact]
    public void QuickSaveScale_UpdatesRatioFromCard()
    {
        using var dir = new TestInfra.TempDir("scale_quick");
        var calibration = new CalibrationManager();
        var calibFolder = System.IO.Path.Combine(dir.Path, "calibration");
        calibration.LoadDirectory(calibFolder);
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.05,
            Width = 2448,
            Height = 2048,
        });
        var vm = CreateVm(calibration);

        vm.Scales[0].ScaleX = 0.051;
        vm.Scales[0].ScaleY = 0.051;
        vm.QuickSaveScaleCommand.Execute(vm.Scales[0]);

        vm.ScaleFormMessage.Should().Contain("已更新");
        calibration.GetScale("st1")!.ScaleX.Should().BeApproximately(0.051, 1e-12);
        calibration.GetScale("st1")!.Width.Should().Be(2448);
    }

    [Fact]
    public void DeleteScale_WithEmptyStationId_DoesNothing()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.05,
        });
        var vm = CreateVm(calibration);

        vm.DeleteScaleCommand.Execute(null);
        vm.DeleteScaleCommand.Execute("");

        vm.Scales.Should().ContainSingle();
        calibration.GetScale("st1").Should().NotBeNull();
    }

    [Fact]
    public void LoadScaleToForm_WhenAlreadySelected_RefreshesFormFromCardEdits()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.05,
            Width = 2448,
            Height = 2048,
        });
        var vm = CreateVm(calibration);
        vm.SelectedScale = vm.Scales[0];
        vm.Scales[0].ScaleX = 0.08;
        vm.Scales[0].ScaleY = 0.09;

        vm.LoadScaleToFormCommand.Execute(vm.Scales[0]);

        vm.ScaleX.Should().BeApproximately(0.08, 1e-12);
        vm.ScaleY.Should().BeApproximately(0.09, 1e-12);
        vm.ScaleFormMessage.Should().Contain("已载入");
    }

    [Fact]
    public void ApplyRef_ComputesRatioFromLengthAndPixels()
    {
        var vm = CreateVm(new CalibrationManager());

        vm.RefLengthMm = 50;
        vm.RefLengthPx = 1000;
        vm.ApplyRefToXCommand.Execute(null);
        vm.ScaleX.Should().BeApproximately(0.05, 1e-12);

        vm.ApplyRefToYCommand.Execute(null);
        vm.ScaleY.Should().BeApproximately(0.05, 1e-12);

        vm.ScaleFormMessage.Should().Contain("1 px = X 0.05 / Y 0.05 mm");
    }

    [Fact]
    public void ApplyRef_WithInvalidInputs_ShowsError()
    {
        var vm = CreateVm(new CalibrationManager());

        vm.RefLengthMm = 0;
        vm.RefLengthPx = 1000;
        vm.ApplyRefToXCommand.Execute(null);

        vm.ScaleFormMessageIsError.Should().BeTrue();
        vm.ScaleFormMessage.Should().Contain("正数");
        vm.ScaleX.Should().Be(0);
    }
}
