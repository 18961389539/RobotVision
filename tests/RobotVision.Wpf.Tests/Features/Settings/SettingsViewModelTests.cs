using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using RobotVision.WpfHost.Features.Settings;
using RobotVision.WpfHost.Shared;

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
    private readonly ResultLogStore _results;
    private readonly SuccessCaptureStore _captures;
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
        _results = new ResultLogStore(
            new ResultLogConfig { Folder = Path.Combine(_dir.Path, "results") },
            NullLogger<ResultLogStore>.Instance);
        _captures = new SuccessCaptureStore(
            new CaptureSuccessConfig { Folder = Path.Combine(_dir.Path, "captures") },
            NullLogger<SuccessCaptureStore>.Instance);
        _settingsPath = System.IO.Path.Combine(_dir.Path, "appsettings.json");
        _store = new AppSettingsStore(_cfg, _settingsPath);
    }

    public void Dispose()
    {
        _results.Dispose();
        _tcp.Dispose();
        _dir.Dispose();
    }

    private SettingsViewModel CreateVm() =>
        new(_cfg, TestInfra.TcpFacade(_tcp), _vision, _failures, _results, _captures, _store, new TestDialogService(), TestLog.Null<SettingsViewModel>());

    [Fact]
    public void Ctor_LoadsRuntimeValues()
    {
        _tcp.MaxConnections = 5;
        _vision.MaxQueueDepth = 8;

        var vm = CreateVm();

        vm.MaxConnections.Should().Be(5);
        vm.MaxQueueDepth.Should().Be(8);
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void DirtyTracking_DetectsEdits_AndClearsAfterSave()
    {
        var vm = CreateVm();
        vm.HasUnsavedChanges.Should().BeFalse();

        vm.MaxQueueDepth = 6;
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
        vm.MaxQueueDepth = 99;
        vm.TcpPort = 8888;

        vm.RestoreDefaultsCommand.Execute(null);

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
        vm.IdleTimeoutMs.Should().Be(ITcpRuntime.IdleTimeoutThirtyDaysMs);
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
    public void Save_ValidationFailure_KeepsUserEdits_AndLocatesOffendingField()
    {
        var vm = CreateVm();
        vm.TcpPort = 0;          // 非法
        vm.MaxQueueDepth = 6;    // 同一个未保存批次里的合法改动

        vm.SaveCommand.Execute(null);

        // 表单保留用户输入（旧行为是 LoadFromRuntime 整屏清空，改完端口要从头再填）
        vm.TcpPort.Should().Be(0);
        vm.MaxQueueDepth.Should().Be(6);
        vm.HasUnsavedChanges.Should().BeTrue();

        // 错误定位到具体字段与分组
        vm.ErrorField.Should().Be(SettingsField.TcpPort);
        vm.ErrorGroup.Should().Be(SettingsGroup.Endpoint);
        vm["TcpPort"].Should().BeTrue("出错字段的标签应转为红字");
        vm["MaxQueueDepth"].Should().BeFalse();
        vm.SelectedAnchor!.Key.Should().Be(SettingsGroup.Endpoint, "出错分组应被自动定位");
        vm.SelectedAnchor!.HasError.Should().BeTrue();
    }

    [Fact]
    public void Save_SucceedsAfterFixingLocatedField()
    {
        var vm = CreateVm();
        vm.TcpPort = 0;
        vm.SaveCommand.Execute(null);
        vm.HasError.Should().BeTrue();

        vm.TcpPort = FreeTcpPort(); // 直接改掉出错项重试
        vm.SaveCommand.Execute(null);

        vm.HasError.Should().BeFalse($"字段={vm.ErrorField} 原因={vm.ErrorMessage}");
        vm.HasUnsavedChanges.Should().BeFalse();
        vm.Message.Should().Contain("已保存并应用");
    }

    [Fact]
    public void Save_ResultLogEnabledWithoutSink_LocatesCheckboxes()
    {
        var vm = CreateVm();
        vm.ResultLogEnabled = true;
        vm.ResultLogJsonl = false;
        vm.ResultLogSqlite = false;

        vm.SaveCommand.Execute(null);

        vm.ErrorField.Should().Be(SettingsField.ResultLogSink);
        vm.ErrorGroup.Should().Be(SettingsGroup.ResultLog);
        vm["ResultLogSink"].Should().BeTrue();
    }

    [Fact]
    public void DirtySummary_TracksFieldCount_AndAnchors()
    {
        var vm = CreateVm();
        vm.IsDirty.Should().BeFalse();
        vm.DirtySummary.Should().Be("无未保存改动");
        vm.CanDiscard.Should().BeFalse();

        vm.MaxQueueDepth = 6;   // runparams
        vm.IsDirty.Should().BeTrue();
        vm.DirtySummary.Should().Be("1 项未保存");
        vm.CanDiscard.Should().BeTrue();
        vm.IsRunParamsDirty.Should().BeTrue();
        vm.IsWhitelistDirty.Should().BeFalse();
        vm.Anchors.Single(a => a.Key == SettingsGroup.RunParams).IsDirty.Should().BeTrue();

        vm.TcpPort = FreeTcpPort();  // endpoint
        vm.DirtySummary.Should().Be("2 项未保存");
        vm.IsWhitelistDirty.Should().BeFalse("端口属「网络端点」组，不是「IP 白名单」");
        vm.IsEndpointDirty.Should().BeTrue();
    }

    [Fact]
    public void PendingRestartSummary_ShowsBeforeSaveAndPersistsAfterSave()
    {
        var vm = CreateVm();
        vm.PendingRestartSummary.Should().BeEmpty();

        vm.TcpBacklog = 32; // 需重启项
        vm.PendingRestartSummary.Should().Contain("监听 backlog");
        vm.PendingRestartSummary.Should().Contain("保存后须重启");

        SetValidPort(vm);
        vm.SaveCommand.Execute(null);

        // 保存后 Message 里那句会被后续轮询冲掉，底栏留痕必须自己撑住
        vm.PendingRestartSummary.Should().Contain("监听 backlog");
        vm.PendingRestartSummary.Should().Contain("已保存");
    }

    [Fact]
    public void Discard_AfterConfirm_RevertsToBaseline()
    {
        var vm = CreateVm();
        vm.MaxQueueDepth = 6;
        vm.HasUnsavedChanges.Should().BeTrue();

        vm.DiscardCommand.Execute(null);

        vm.MaxQueueDepth.Should().Be(_vision.MaxQueueDepth);
        vm.HasUnsavedChanges.Should().BeFalse();
        vm.Message.Should().Contain("放弃");
    }

    [Fact]
    public void Discard_WhenUserDeclines_KeepsEdits()
    {
        var dialogs = new TestDialogService { ConfirmDiscardResult = false };
        var vm = new SettingsViewModel(
            _cfg, TestInfra.TcpFacade(_tcp), _vision, _failures, _results, _captures, _store,
            dialogs, TestLog.Null<SettingsViewModel>());
        vm.MaxQueueDepth = 6;

        vm.DiscardCommand.Execute(null);

        vm.MaxQueueDepth.Should().Be(6);
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void RestoreDefaults_WhenUserDeclines_KeepsEdits()
    {
        var dialogs = new TestDialogService { ConfirmYesNoResult = false };
        var vm = new SettingsViewModel(
            _cfg, TestInfra.TcpFacade(_tcp), _vision, _failures, _results, _captures, _store,
            dialogs, TestLog.Null<SettingsViewModel>());
        vm.MaxQueueDepth = 99;

        vm.RestoreDefaultsCommand.Execute(null);

        vm.MaxQueueDepth.Should().Be(99);
        dialogs.Warnings.Should().BeEmpty();
    }

    /// <summary>每个校验字段都必须能映射到页面上的一个分组，否则错误提示点会静默丢失。</summary>
    [Fact]
    public void EverySettingsField_MapsToAKnownGroup()
    {
        string[] groups =
        [
            SettingsGroup.Appearance, SettingsGroup.RunParams, SettingsGroup.Pose,
            SettingsGroup.PlcDebug, SettingsGroup.Retention, SettingsGroup.ResultLog,
            SettingsGroup.FileLog, SettingsGroup.Health, SettingsGroup.Whitelist,
            SettingsGroup.Endpoint,
        ];

        foreach (var field in SettingsField.All)
        {
            var group = SettingsViewModel.GroupOf(field);
            group.Should().NotBeNull($"字段 {field} 未登记分组");
            groups.Should().Contain(group!);
        }
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
        vm.MaxQueueDepth = 99;
        vm.HasUnsavedChanges.Should().BeTrue();

        vm.ReloadCommand.Execute(null);

        vm.MaxQueueDepth.Should().Be(_vision.MaxQueueDepth);
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void Save_InferenceProviderChange_FlagsRestartNeeded()
    {
        var vm = CreateVm();
        vm.InferenceProvider = "OpenVinoCpu";
        SetValidPort(vm);

        vm.SaveCommand.Execute(null);

        vm.Message.Should().Contain("需重启程序生效");
    }

    [Fact]
    public void Save_EndpointRestartFailure_RollsBackRuntimeButKeepsUserInput()
    {
        var vm = CreateVm();
        vm.IpAddress = "127.0.0.1";
        var savedPort = FreeTcpPort();
        vm.TcpPort = savedPort;
        vm.SaveCommand.Execute(null);
        vm.Message.Should().Contain("已保存并应用");
        _cfg.IpAddress.Should().Be("127.0.0.1");
        _cfg.TcpPort.Should().Be(savedPort);

        var blocker = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        blocker.Start();
        var busyPort = ((System.Net.IPEndPoint)blocker.LocalEndpoint).Port;
        try
        {
            vm.TcpPort = busyPort;
            vm.SaveCommand.Execute(null);

            vm.Message.Should().Contain("未保存");
            vm.Message.Should().NotContain("已保存并应用");

            // 运行时与磁盘都回滚到旧端点
            _tcp.Port.Should().Be(savedPort);
            _tcp.IsRunning.Should().BeTrue();
            using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_settingsPath)))
                doc.RootElement.GetProperty("TcpPort").GetInt32().Should().Be(savedPort);

            // 但表单保留用户填的端口（旧行为是回滚表单，用户要从头再填一遍）
            vm.TcpPort.Should().Be(busyPort);
            vm.HasUnsavedChanges.Should().BeTrue("仍处于未保存状态，便于直接改端口重试");
            vm.ErrorField.Should().Be(SettingsField.TcpPort);
        }
        finally
        {
            blocker.Stop();
        }
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
