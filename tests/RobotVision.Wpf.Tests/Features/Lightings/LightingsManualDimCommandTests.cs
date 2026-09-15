using FluentAssertions;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Lighting;
using RobotVision.WpfHost.Features.Lightings;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 光源页「手动调光」按钮的命令链路测试：直接执行应用真实的
/// <see cref="LightingsViewModel.TurnOnCommand"/> / <c>TurnOffCommand</c>，
/// 断言它们把什么送到了 <see cref="ILightController"/>。
///
/// 背景（2026-09-14）：现场报"能关灯、不能开灯"。真正的原因是三个缺陷叠加，
/// 其中一个（TurnOn 的 Commit 写在 try 之外 + TurnOff 吞掉发送失败）只在
/// "命令经过 ViewModel 这一层"时才会暴露 —— 光测 Manager 测不出来，
/// 所以这里刻意用真实 VM + 真实 Manager，只把控制器换成可记录的假件。
/// </summary>
public sealed class LightingsManualDimCommandTests : IDisposable
{
    /// <summary>可记录、可注入发送失败的假控制器（替代真串口）。</summary>
    private sealed class RecordingLight(string id) : ILightController
    {
        public string Id { get; } = id;

        public LightControllerKind Kind => LightControllerKind.Virtual;

        public List<LightingConfig> Applied { get; } = [];

        public int TurnOffCount { get; private set; }

        public bool ApplyResult { get; set; } = true;

        public bool TurnOffResult { get; set; } = true;

        public bool SendRawResult { get; set; } = true;

        public List<string> RawCommands { get; } = [];

        public bool Apply(LightingConfig lighting)
        {
            Applied.Add(lighting);
            return ApplyResult;
        }

        public bool TurnOff()
        {
            TurnOffCount++;
            return TurnOffResult;
        }

        public bool SendRaw(string command)
        {
            RawCommands.Add(command);
            return SendRawResult;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>自包含的日志捕获 sink：断言"开关灯失败必须留档"。</summary>
    private sealed class CapturingLogger : ILogger<LightingsViewModel>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private readonly TestInfra.TempDir _dir = new("rv_lightcmd");
    private readonly AppConfig _cfg;
    private readonly LightingManager _lighting = new();
    private readonly RecordingLight _record = new("dongguan");
    private readonly LightingConfigStore _store;
    private readonly RecipeLoader _recipes;

    public LightingsManualDimCommandTests()
    {
        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _cfg.LightControllers.Add(new LightControllerConfig
        {
            Id = "dongguan",
            Type = "Serial",
            Port = "COM5",
            BaudRate = 9600,
            ChannelCount = 2,
        });
        _store = new LightingConfigStore(_cfg, Path.Combine(_dir.Path, "appsettings.json"));
        _recipes = new RecipeLoader(_dir.CreateSub("recipes"));
        _lighting.Register(_record);
    }

    public void Dispose()
    {
        _lighting.Dispose();
        _dir.Dispose();
    }

    private LightingsViewModel CreateVm(ILogger<LightingsViewModel>? log = null)
    {
        var vm = new LightingsViewModel(_cfg, TestInfra.LightingFacade(_lighting), _store, _recipes,
            LightControllerTypeRegistry.CreateDefault(), new TestDialogService(),
            log ?? TestLog.Null<LightingsViewModel>());
        vm.Refresh();
        return vm;
    }

    [Fact]
    public void TurnOn_Command_SendsSelectedChannelAndBrightness_StayOnUntilTurnOff()
    {
        var vm = CreateVm();
        vm.Selected!.Id.Should().Be("dongguan");
        vm.Channel = 2;
        vm.Brightness = 200;

        vm.TurnOnCommand.Execute(null);

        _record.Applied.Should().HaveCount(1);
        var config = _record.Applied[0];
        config.Channels.Should().ContainSingle();
        config.Channels[0].Channel.Should().Be(2);
        config.Channels[0].Brightness.Should().Be(200);
        config.TurnOffAfterGrab.Should().BeFalse("手动开灯必须保持点亮，不能被取图后的自动熄灯收走");
        _record.TurnOffCount.Should().Be(0);
        vm.Message.Should().Contain("已点亮").And.Contain("通道 2");
    }

    [Fact]
    public void TurnOff_Command_ReportsFailure_WhenControllerCannotSend()
    {
        var vm = CreateVm();
        _record.TurnOffResult = false;

        vm.TurnOffCommand.Execute(null);

        // 曾经这里会显示"已熄灯"（假成功），而开灯如实报错 —— 正是"能关不能开"的来源
        vm.Message.Should().StartWith("熄灯失败");
    }

    [Fact]
    public void TurnOn_Command_WhenApplyFails_ReportsFailure_NotFakeSuccess()
    {
        var vm = CreateVm();
        _record.ApplyResult = false;

        vm.TurnOnCommand.Execute(null);

        vm.Message.Should().StartWith("开灯失败");
        _record.Applied.Should().HaveCount(1);
    }

    /// <summary>
    /// Commit 抛异常时必须被命令自己接住并报错，不得逃逸到 DispatcherUnhandledException。
    /// 原先 TurnOn 的 Commit 写在 try 之外，一旦刷写失败命令就静默中断（灯不亮且无提示）。
    /// </summary>
    [Fact]
    public void TurnOn_Command_CommitThrows_IsReportedNotEscaped()
    {
        var vm = CreateVm();
        vm.FlushPendingEdits = () => throw new InvalidOperationException("刷写失败");

        var act = () => vm.TurnOnCommand.Execute(null);

        act.Should().NotThrow();
        vm.Message.Should().StartWith("开灯失败").And.Contain("刷写失败");
        _record.Applied.Should().BeEmpty("提交失败时不得发出点亮指令");
    }

    /// <summary>未选中/未注册时必须拒绝，且不得触碰控制器。</summary>
    [Fact]
    public void TurnOn_Command_WithoutRegisteredSelection_IsRejected()
    {
        var vm = CreateVm();
        _lighting.Unregister("dongguan");
        vm.Refresh();
        vm.Selected = null;

        vm.TurnOnCommand.Execute(null);

        vm.Message.Should().Contain("请选择已注册的光源控制器");
        _record.Applied.Should().BeEmpty();
    }

    /// <summary>
    /// 开灯失败必须留档。原先失败只写进页面 Message —— 用户一切走就再无线索，
    /// 而"灯不亮"是现场最高频的报障（2026-09-14 排查里正是缺一份可回看的记录）。
    /// </summary>
    [Fact]
    public void TurnOn_Command_WhenApplyFails_LeavesWarningInLog()
    {
        var logger = new CapturingLogger();
        var vm = CreateVm(logger);
        _record.ApplyResult = false;

        vm.TurnOnCommand.Execute(null);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Failed to turn ON lighting", StringComparison.Ordinal));
        logger.Entries[0].Message.Should().Contain("dongguan");
    }

    /// <summary>熄灯失败同样必须留档（曾经连"失败"都不显示，是假成功）。</summary>
    [Fact]
    public void TurnOff_Command_WhenControllerCannotSend_LeavesWarningInLog()
    {
        var logger = new CapturingLogger();
        var vm = CreateVm(logger);
        _record.TurnOffResult = false;

        vm.TurnOffCommand.Execute(null);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Failed to turn OFF lighting", StringComparison.Ordinal));
    }

    /// <summary>成功路径不得产生 Warning 噪声。</summary>
    [Fact]
    public void TurnOn_Command_Success_LogsNoWarning()
    {
        var logger = new CapturingLogger();
        var vm = CreateVm(logger);

        vm.TurnOnCommand.Execute(null);

        logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// 协议调试框是排障时最不能骗人的一步：串口没打开时它曾无脑显示「已发送」，
    /// 把排查方向直接带到协议/硬件上（2026-09-14 排查光源时实际被它带偏过）。
    /// </summary>
    [Fact]
    public void SendDebug_WhenControllerCannotSend_ReportsFailure_NotFakeSuccess()
    {
        var logger = new CapturingLogger();
        var vm = CreateVm(logger);
        _record.SendRawResult = false;
        vm.DebugCommand = "00 13 FF FF FF";

        vm.SendDebugCommand.Execute(null);

        vm.Message.Should().StartWith("发送失败");
        vm.DebugResult.Should().BeEmpty("未发出时不得把它记成已发送过的指令");
        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains("Failed to send raw command", StringComparison.Ordinal));
    }

    /// <summary>发送成功时保持原有行为（把指令透传下去并回显）。</summary>
    [Fact]
    public void SendDebug_WhenSendSucceeds_PassesCommandThrough()
    {
        var vm = CreateVm();
        vm.DebugCommand = "00 13 FF FF FF";

        vm.SendDebugCommand.Execute(null);

        _record.RawCommands.Should().ContainSingle().Which.Should().Be("00 13 FF FF FF");
        vm.DebugResult.Should().Be("00 13 FF FF FF");
        vm.Message.Should().Contain("已发送到");
    }
}
