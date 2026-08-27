using FluentAssertions;
using RobotVision.Hosting;
using RobotVision.WpfHost;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 日志页测试：Serilog 文件行解析（LogRow.ParseAll）与 LogsViewModel 行为
/// （文件列表/加载/级别过滤/关键词过滤/清空/跟随增量）。
/// </summary>
public class LogsViewModelTests
{
    // ---------- LogRow.ParseAll：纯解析 ----------

    [Fact]
    public void ParseAll_StandardLine_ParsesFields()
    {
        var rows = LogRow.ParseAll(
            ["2026-08-25 12:34:56.789 [INF] RobotVision.Hosting.VisionService: 配方 A01 检出 2 个目标"]);

        rows.Should().ContainSingle();
        rows[0].Time.Should().Be("12:34:56.789"); // 日期在文件名上，只保留时分秒
        rows[0].Level.Should().Be("INF");
        rows[0].Source.Should().Be("RobotVision.Hosting.VisionService");
        rows[0].Message.Should().Be("配方 A01 检出 2 个目标");
    }

    [Fact]
    public void ParseAll_ContinuationLines_MergedIntoPreviousMessage()
    {
        var rows = LogRow.ParseAll(
        [
            "2026-08-25 12:34:56.789 [ERR] A.B: boom",
            "   at RobotVision.Core.VisionException..ctor()",
            "   at RobotVision.Hosting.VisionService.RunAsync()",
        ]);

        rows.Should().ContainSingle();
        rows[0].Message.Should()
            .StartWith("boom").And
            .Contain("RobotVision.Core.VisionException").And
            .Contain("RunAsync");
    }

    [Fact]
    public void ParseAll_LeadingContinuation_WithoutPreviousLine_IsDropped()
    {
        var rows = LogRow.ParseAll(["  at Some.Stack: line", "2026-08-25 00:00:00.000 [INF] X: hello"]);

        rows.Should().ContainSingle();
        rows[0].Message.Should().Be("hello");
    }

    [Fact]
    public void ParseAll_EmptyInput_ReturnsEmpty()
    {
        LogRow.ParseAll([]).Should().BeEmpty();
    }

    [Theory]
    [InlineData("2026-08-25 00:00:00.000 [DBG] C: x", true)]
    [InlineData("2026-08-25 00:00:00.000 [WRN] C: x", true)]
    [InlineData("2026-08-25 00:00:00.000 [FTL] C: x", true)]
    [InlineData("  indented continuation", false)]
    [InlineData("random garbage", false)]
    [InlineData("2026-08-25 00:00:00.000 [TOOLONG] C: x", false)] // 级别最多 3 字符
    [InlineData("2026-08-25 00:00 [INF] C: x", false)] // 毫秒缺失
    public void IsHeaderLine_RecognizesFormat(string line, bool expected) =>
        LogRow.IsHeaderLine(line).Should().Be(expected);

    // ---------- LogsViewModel：文件列表与加载 ----------

    [Fact]
    public void Ctor_WhenLogFolderMissing_SetsStatus()
    {
        using var dir = new TestInfra.TempDir("rv_logs_missing");
        var cfg = TestInfra.CreateAppConfig(dir.Path);
        cfg.FileLogging.Folder = System.IO.Path.Combine(dir.Path, "no_such_logs");

        var vm = new LogsViewModel(cfg);

        vm.Files.Should().BeEmpty();
        vm.Status.Should().Contain("日志目录不存在");
    }

    [Fact]
    public async Task RefreshFiles_ListsLogFiles_SortedNewestFirst()
    {
        using var dir = new TestInfra.TempDir("rv_logs_files");
        var logs = dir.CreateSub("logs");
        WriteLogFile(logs, "robotvision-20260825.log");
        WriteLogFile(logs, "robotvision-20260824.log");
        var cfg = TestInfra.CreateAppConfig(dir.Path);
        cfg.FileLogging.Folder = logs;

        var vm = new LogsViewModel(cfg);

        vm.Files.Should().HaveCount(2);
        vm.Files[0].Name.Should().Be("robotvision-20260825.log"); // 时间倒序
        vm.Files[1].Name.Should().Be("robotvision-20260824.log");
        vm.SelectedFile.Should().Be(vm.Files[0]);
        // 注：Status 会被 SelectedFile 变更触发的异步 ReloadAsync 覆盖（"xxx.log · 0 行"），
        // 此处不断言 Status，避免与异步加载竞态
    }

    [Fact]
    public async Task ReloadAsync_LoadsRows_AndAppliesFilter()
    {
        using var dir = new TestInfra.TempDir("rv_logs_load");
        var logs = dir.CreateSub("logs");
        File.WriteAllLines(System.IO.Path.Combine(logs, "robotvision-20260825.log"),
        [
            "2026-08-25 10:00:00.000 [INF] A: info line",
            "2026-08-25 10:00:01.000 [ERR] B: error line",
            "2026-08-25 10:00:02.000 [WRN] C: warning line",
        ]);
        var cfg = TestInfra.CreateAppConfig(dir.Path);
        cfg.FileLogging.Folder = logs;

        var vm = new LogsViewModel(cfg);
        vm.SelectedFile.Should().NotBeNull();
        await vm.ReloadCommand.ExecuteAsync(null);

        vm.Rows.Should().HaveCount(3);
        vm.Status.Should().Contain("3 行");

        // 级别过滤：关掉 Info → 剩 2 行
        vm.IncludeInfo = false;
        vm.Rows.Should().HaveCount(2);
        vm.Rows.Should().OnlyContain(r => r.Level != "INF");

        // 关掉 Warning → 剩 1 行
        vm.IncludeWarning = false;
        vm.Rows.Should().ContainSingle();
        vm.Rows[0].Level.Should().Be("ERR");

        // 关掉 Error → 0 行
        vm.IncludeError = false;
        vm.Rows.Should().BeEmpty();
    }

    [Fact]
    public void ReloadAsync_KeywordFilter_MatchesMessageOrSource()
    {
        // 关键词过滤经 300ms 防抖 DispatcherTimer 应用：STA 线程 + PushFrame 消息循环才能可靠触发
        TestInfra.RunSta(() =>
        {
            using var dir = new TestInfra.TempDir("rv_logs_kw");
            var logs = dir.CreateSub("logs");
            File.WriteAllLines(System.IO.Path.Combine(logs, "robotvision-20260825.log"),
            [
                "2026-08-25 10:00:00.000 [INF] VisionService: recipe A01 ok",
                "2026-08-25 10:00:01.000 [INF] Camera: frame grabbed",
                "2026-08-25 10:00:02.000 [INF] VisionService: recipe B02 fail",
            ]);
            var cfg = TestInfra.CreateAppConfig(dir.Path);
            cfg.FileLogging.Folder = logs;

            var vm = new LogsViewModel(cfg);
            vm.ReloadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            vm.Rows.Should().HaveCount(3);

            vm.Keyword = "recipe";
            PumpUntil(() => vm.Rows.Count == 2);
            vm.Rows.Should().HaveCount(2);
            vm.Rows.Should().OnlyContain(r => r.Message.Contains("recipe"));

            vm.Keyword = "Camera"; // 匹配来源（大小写不敏感）
            PumpUntil(() => vm.Rows.Count == 1);
            vm.Rows.Should().ContainSingle();
            vm.Rows[0].Source.Should().Be("Camera");
        });
    }

    /// <summary>pump 当前线程 Dispatcher 队列直到条件成立（触发 DispatcherTimer 防抖回调）。</summary>
    private static void PumpUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until && !condition())
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        }
    }

    [Fact]
    public async Task ReloadAsync_SucceedsWhileFileIsBeingWritten()
    {
        using var dir = new TestInfra.TempDir("rv_logs_share");
        var logs = dir.CreateSub("logs");
        var path = System.IO.Path.Combine(logs, "robotvision-20260826.log");
        File.WriteAllText(path, "2026-08-26 10:00:00.000 [INF] A: first\n");
        var cfg = TestInfra.CreateAppConfig(dir.Path);
        cfg.FileLogging.Folder = logs;

        await using var writer = new FileStream(
            path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var vm = new LogsViewModel(cfg);
        await vm.ReloadCommand.ExecuteAsync(null);

        vm.Rows.Should().ContainSingle();
        vm.Status.Should().NotContain("读取失败");
    }

    [Fact]
    public void ClearFilter_ResetsAllLevelsAndKeyword()
    {
        using var dir = new TestInfra.TempDir("rv_logs_clear");
        var cfg = TestInfra.CreateAppConfig(dir.Path);

        var vm = new LogsViewModel(cfg);
        vm.IncludeDebug = true;
        vm.IncludeInfo = false;
        vm.IncludeWarning = false;
        vm.IncludeError = false;
        vm.Keyword = "xxx";

        vm.ClearFilterCommand.Execute(null);

        vm.IncludeDebug.Should().BeFalse();
        vm.IncludeInfo.Should().BeTrue();
        vm.IncludeWarning.Should().BeTrue();
        vm.IncludeError.Should().BeTrue();
        vm.Keyword.Should().BeEmpty();
    }

    [Fact]
    public void LevelFilters_DefaultState_KeepsInfoWarningError()
    {
        // 级别过滤默认契约：Info/Warning/Error 打开，Debug 关闭（生产日志默认不显示调试）
        using var dir = new TestInfra.TempDir("rv_logs_level");
        var cfg = TestInfra.CreateAppConfig(dir.Path);
        var vm = new LogsViewModel(cfg);

        vm.IncludeInfo.Should().BeTrue();
        vm.IncludeWarning.Should().BeTrue();
        vm.IncludeError.Should().BeTrue();
        vm.IncludeDebug.Should().BeFalse();
    }

    private static void WriteLogFile(string folder, string name) =>
        File.WriteAllText(System.IO.Path.Combine(folder, name), "");
}
