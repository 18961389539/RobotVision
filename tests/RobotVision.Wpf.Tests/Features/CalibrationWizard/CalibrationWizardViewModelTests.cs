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
            TestInfra.CalibrationWizard(TestInfra.CameraFacade(_cameras), TestInfra.CalibrationFacade(_calibration)),
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

    [Fact]
    public async Task OnPageUnloading_CancelsInFlightGrab_AndClearsFrame()
    {
        SlowPreviewCameraFactory.GrabAfterDisposeCount = 0;
        var slowConfig = new CameraConfig
        {
            Id = "cam_slow",
            Type = "SlowPreview",
            Width = 64,
            Height = 64,
            IntervalMs = 800,
        };
        _cameras.Register(new SlowPreviewCameraFactory().Create(slowConfig));
        _cfg.Cameras.Add(slowConfig);

        var vm = CreateVm();
        vm.SelectedCamera = "cam_slow";

        var grabTask = vm.GrabCommand.ExecuteAsync(null);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!vm.IsBusy && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        vm.IsBusy.Should().BeTrue();

        vm.OnPageUnloading();

        await grabTask.WaitAsync(TimeSpan.FromSeconds(5));

        vm.FrameImage.Should().BeNull();
        vm.IsBusy.Should().BeFalse();
        SlowPreviewCameraFactory.GrabAfterDisposeCount.Should().Be(0);
    }

    [Fact]
    public void OperationCommands_AreDisabled_WhenIsBusy()
    {
        var vm = CreateVm();
        vm.IsBusy = true;

        vm.GrabCommand.CanExecute(null).Should().BeFalse();
        vm.ComputeCommand.CanExecute(null).Should().BeFalse();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
        vm.ClearPointsCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task GrabAsync_UsesSnapshottedCameraId_WhenSelectionChangesDuringGrab()
    {
        SlowPreviewCameraFactory.GrabAfterDisposeCount = 0;
        _cameras.Register(new VirtualCamera("cam_other", 64, 64, "Bars"));
        _cfg.Cameras.Add(new CameraConfig { Id = "cam_other", Type = "Virtual" });

        var slowConfig = new CameraConfig
        {
            Id = "cam_slow",
            Type = "SlowPreview",
            Width = 64,
            Height = 64,
            IntervalMs = 800,
        };
        _cameras.Register(new SlowPreviewCameraFactory().Create(slowConfig));
        _cfg.Cameras.Add(slowConfig);

        var vm = CreateVm();
        vm.SelectedCamera = "cam_slow";

        var grabTask = vm.GrabCommand.ExecuteAsync(null);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!vm.IsBusy && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        vm.IsBusy.Should().BeTrue();

        vm.SelectedCamera = "cam_other";
        await grabTask.WaitAsync(TimeSpan.FromSeconds(5));

        vm.Message.Should().Contain("cam_slow");
        vm.Message.Should().NotContain("cam_other");
    }

    [Fact]
    public void WizardMode_MapsToHostingCalibrationWizardMode()
    {
        foreach (WizardMode mode in Enum.GetValues<WizardMode>())
        {
            var hosting = CalibrationWizardModeMapping.ToHosting(mode);
            CalibrationWizardModeMapping.ToWizard(hosting).Should().Be(mode);
        }
    }

    [Fact]
    public async Task ComputeAsync_WhenExtrinsicWithoutIntrinsic_LeavesNothingToSave()
    {
        var vm = CreateVm();
        vm.Mode = WizardMode.Extrinsic;
        vm.StationId = "st1";
        for (var i = 0; i < 9; i++)
        {
            vm.Points.Add(new CalibPointItem
            {
                PixelX = i,
                PixelY = i,
                RobotX = i,
                RobotY = i,
                RobotEntered = true,
            });
        }

        await vm.ComputeCommand.ExecuteAsync(null);

        vm.Message.Should().Contain("内参");
        vm.SaveCommand.Execute(null);
        vm.Message.Should().Be("请先计算");
    }
}
