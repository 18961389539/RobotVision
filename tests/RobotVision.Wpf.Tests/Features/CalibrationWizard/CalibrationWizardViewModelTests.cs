using FluentAssertions;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.WpfHost.Features.CalibrationWizard;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

public sealed class CalibrationWizardViewModelTests : IDisposable
{
    private readonly CameraManager _cameras = new();
    private readonly CalibrationManager _calibration = new();
    private readonly AppConfig _cfg;

    public CalibrationWizardViewModelTests()
    {
        _cameras.Register(new VirtualCamera("cam_virtual", 1280, 960, "Chessboard"));
        _cfg = TestInfra.CreateAppConfig(Path.GetTempPath());
        _cfg.Cameras.Add(new CameraConfig { Id = "cam_virtual", Type = "Virtual" });
    }

    public void Dispose()
    {
        _cameras.Dispose();
        _calibration.Dispose();
    }

    private CalibrationWizardViewModel CreateVm() =>
        new(TestInfra.CameraFacade(_cameras), TestInfra.CalibrationFacade(_calibration),
            _cfg, new TestDialogService(), TestLog.Null<CalibrationWizardViewModel>());

    [Fact]
    public void Ctor_DefaultsToRecommendedPolynomialMode()
    {
        var vm = CreateVm();

        vm.Mode.Should().Be(WizardMode.Polynomial);
        vm.SelectedModeOption.IsRecommended.Should().BeTrue();
        vm.SelectedModeOption.Label.Should().Contain("快换标定");
        vm.SelectedCamera.Should().Be("cam_virtual");
    }

    [Fact]
    public void RefreshCameras_RaisesCameraOptionBindings()
    {
        var vm = CreateVm();
        _cameras.Register(new VirtualCamera("cam_extra", 64, 64, "Bars"));

        vm.RefreshCameras();

        vm.CameraIds.Should().Contain("cam_extra");
        vm.CameraOptions.Select(o => o.Id).Should().Contain("cam_extra");
    }

    [Fact]
    public void OnSelectedCameraChanged_SyncsVirtualChessboardSize()
    {
        var vm = CreateVm();
        vm.SelectedCamera = "";
        vm.Cols = 9;
        vm.Rows = 6;

        vm.SelectedCamera = "cam_virtual";

        vm.Cols.Should().Be(15);
        vm.Rows.Should().Be(11);
    }

    [Fact]
    public void ModeOptions_IncludeIntrinsicExtrinsicAndRotation()
    {
        var vm = CreateVm();

        vm.ModeOptions.Select(o => o.Value).Should().Contain(WizardMode.Intrinsic);
        vm.ModeOptions.Select(o => o.Value).Should().Contain(WizardMode.Extrinsic);
        vm.ModeOptions.Select(o => o.Value).Should().Contain(WizardMode.Rotation);
        vm.ModeOptions.Should().Contain(o => o.IsRecommended);
    }
}
