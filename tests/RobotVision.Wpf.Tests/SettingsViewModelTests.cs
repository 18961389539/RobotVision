using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using RobotVision.WpfHost;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 服务参数设置页测试：运行时加载映射、脏标记（HasUnsavedChanges）、
/// 恢复出厂默认值、空闲超时快捷键、保存（成功/热重启/失败）路径。
/// </summary>
public class SettingsViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_settings");
    private readonly AppConfig _cfg;
    private readonly TcpServerManager _tcp;
    private readonly VisionService _vision;
    private readonly FailureImageStore _failures;
    private readonly AppSettingsStore _store;
    private readonly string _settingsPath;

    public SettingsViewModelTests()
    {
        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _tcp = TestInfra.CreateTcp();
        _vision = TestInfra.CreateVisionService(_cfg.RecipesFolder);
        _failures = new FailureImageStore(
            new FailureImageConfig { Folder = _cfg.FailureImage.Folder, RetainedCount = 200 },
            NullLogger<FailureImageStore>.Instance);
        _settingsPath = System.IO.Path.Combine(_dir.Path, "appsettings.json");
        _store = new AppSettingsStore(_cfg, _settingsPath);
    }

    public void Dispose()
    {
        _tcp.Dispose();
        _dir.Dispose();
    }

    private SettingsViewModel CreateVm() =>
        new(_cfg, _tcp, _vision, _failures, _store);

    [Fact]
    public void Ctor_LoadsRuntimeValues()
    {
        _tcp.TimeoutMs = 7000;
        _tcp.MaxConnections = 5;
        _vision.MaxQueueDepth = 8;

        var vm = CreateVm();

        vm.TimeoutMs.Should().Be(7000);
        vm.MaxConnections.Should().Be(5);
        vm.MaxQueueDepth.Should().Be(8);
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void DirtyTracking_DetectsEdits_AndClearsAfterSave()
    {
        var vm = CreateVm();
        vm.HasUnsavedChanges.Should().BeFalse();

        vm.TimeoutMs = 6000;
        vm.HasUnsavedChanges.Should().BeTrue();

        SetValidPort(vm);
        vm.SaveCommand.Execute(null);
        vm.HasUnsavedChanges.Should().BeFalse();
        vm.Message.Should().Contain("已保存并应用");
    }

    [Fact]
    public void DirtyTracking_IpAddressCaseInsensitive()
    {
        var vm = CreateVm();
        vm.IpAddress = "127.0.0.1";
        vm.HasUnsavedChanges.Should().BeFalse(); // 大小写不敏感（基线同值）
    }

    [Fact]
    public void RestoreDefaults_FillsFactoryDefaults()
    {
        var vm = CreateVm();
        vm.TimeoutMs = 12345;
        vm.MaxQueueDepth = 99;
        vm.TcpPort = 8888;

        vm.RestoreDefaultsCommand.Execute(null);

        vm.TimeoutMs.Should().Be(5000);
        vm.MaxQueueDepth.Should().Be(4);
        vm.TcpPort.Should().Be(9999);
        vm.IpAddress.Should().Be("0.0.0.0");
        vm.Message.Should().Contain("出厂默认值");
    }

    [Fact]
    public void IdleShortcuts_SetNeverAndThirtyDays()
    {
        var vm = CreateVm();

        vm.SetIdleNeverCommand.Execute(null);
        vm.IdleTimeoutMs.Should().Be(0);

        vm.SetIdleThirtyDaysCommand.Execute(null);
        vm.IdleTimeoutMs.Should().Be(TcpServerManager.IdleTimeoutThirtyDaysMs);
    }

    [Fact]
    public void Save_WithEndpointChange_HotRestartsListener()
    {
        var port = FreeTcpPort();
        var vm = CreateVm();
        vm.IpAddress = "127.0.0.1";
        vm.TcpPort = port;

        vm.SaveCommand.Execute(null);

        vm.Message.Should().Contain("已保存并应用").And.Contain($"127.0.0.1:{port}");
        _tcp.IsRunning.Should().BeTrue();
        _tcp.ListenEndPoint.Should().Be($"127.0.0.1:{port}");
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void Save_WithInvalidPort_ShowsSaveError_WithoutStartingListener()
    {
        var vm = CreateVm();
        vm.IpAddress = "127.0.0.1";
        vm.TcpPort = 0; // 非法端口（1~65535）

        vm.SaveCommand.Execute(null);

        vm.Message.Should().Contain("保存失败").And.Contain("端口");
        _tcp.IsRunning.Should().BeFalse(); // 未启动监听
    }

    [Fact]
    public void Save_WhitelistParsesMultiLine()
    {
        var vm = CreateVm();
        vm.WhitelistText = $"192.168.1.10{Environment.NewLine}192.168.*";
        SetValidPort(vm);

        vm.SaveCommand.Execute(null);

        _tcp.IpWhitelist.Should().Equal("192.168.1.10", "192.168.*");
    }

    [Fact]
    public void Save_MaxConcurrentOrBacklogChange_FlagsRestartNeeded()
    {
        var vm = CreateVm();
        vm.MaxConcurrent = 3; // ≠ 基线 1，且 ≤ 队列深度 4
        SetValidPort(vm);

        vm.SaveCommand.Execute(null);

        vm.Message.Should().Contain("需重启程序生效");
    }

    [Fact]
    public void Reload_ReappliesRuntimeState_ResetsUnsavedChanges()
    {
        var vm = CreateVm();
        vm.TimeoutMs = 9999;
        vm.HasUnsavedChanges.Should().BeTrue();

        vm.ReloadCommand.Execute(null);

        vm.TimeoutMs.Should().Be(_tcp.TimeoutMs);
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    /// <summary>获取一个当前空闲的 TCP 端口（绑定 0 后立即释放）。</summary>
    private static int FreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>保存前设置合法端口（基线端口 0 无法通过 Store 校验）。</summary>
    private static void SetValidPort(SettingsViewModel vm) => vm.TcpPort = FreeTcpPort();
}
