using FluentAssertions;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using RobotVision.WpfHost.Features.Communication;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

public sealed class CommunicationViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_comm");
    private readonly TcpServerManager _tcp;
    private readonly VisionService _vision;
    private readonly AppConfig _cfg;

    public CommunicationViewModelTests()
    {
        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _vision = TestInfra.CreateVisionService(_cfg.RecipesFolder);
        _tcp = TestInfra.CreateTcp();
    }

    public void Dispose()
    {
        _tcp.Dispose();
        _dir.Dispose();
    }

    private CommunicationViewModel CreateVm() =>
        new(TestInfra.TcpFacade(_tcp), _vision, _cfg, new TestDialogService(), TestLog.Null<CommunicationViewModel>());

    [Fact]
    public void ToggleService_WhenRunningAndStopDeclined_KeepsServiceRunning()
    {
        _tcp.Start();
        var vm = CreateVm();
        vm.ConfirmStopServiceForTests = () => false;

        vm.ToggleServiceCommand.Execute(null);

        _tcp.IsRunning.Should().BeTrue();
        vm.ToggleButtonLabel.Should().Be("停止服务");
    }

    [Fact]
    public void ToggleService_WhenRunningAndStopConfirmed_StopsService()
    {
        _tcp.Start();
        var vm = CreateVm();
        vm.ConfirmStopServiceForTests = () => true;

        vm.ToggleServiceCommand.Execute(null);

        _tcp.IsRunning.Should().BeFalse();
        vm.ToggleButtonLabel.Should().Be("启动服务");
    }

    [Fact]
    public void RefreshCommand_UpdatesServiceStatus()
    {
        _tcp.Start();
        var vm = CreateVm();
        vm.ServiceStatus = "stale";

        vm.RefreshCommand.Execute(null);

        vm.ServiceStatus.Should().Contain("运行中");
    }
}
