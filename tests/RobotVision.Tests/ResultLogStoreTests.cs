using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 结果日志（ResultLogStore）测试：JSON Lines 按天落盘、字段完整性、
/// 成功/失败同一格式（失败行 Code=错误码）、开关、按天清理。
/// 写入为后台异步追加，断言前轮询等待文件出现。
/// </summary>
public class ResultLogStoreTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "rv_results_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static ResultLogConfig Config(string folder, bool enabled = true, int retainedDays = 30) => new()
    {
        Enabled = enabled,
        Folder = folder,
        RetainedDays = retainedDays,
        Sqlite = false,
    };

    private static string WaitForFile(string dir, string pattern, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var file = Directory.Exists(dir) ? Directory.GetFiles(dir, pattern).FirstOrDefault() : null;
            if (file is not null)
                return file;
            Thread.Sleep(50);
        }
        return "";
    }

    [Fact]
    public void Record_SuccessResult_AppendsJsonLine_WithCoordinates()
    {
        using var store = new ResultLogStore(Config(_folder), NullLogger<ResultLogStore>.Instance);
        var success = VisionResult.Success("A03",
            [new RobotPose(15.023, 20.117, 0.12)], 87.5, [0.92]);

        store.Record(success, ("cam_file", "S1"));

        var file = WaitForFile(_folder, "results-*.jsonl");
        Assert.NotEqual("", file);
        var line = File.ReadAllLines(file).First();
        var entry = JsonSerializer.Deserialize<ResultLogEntry>(line);
        Assert.NotNull(entry);
        Assert.Equal("A03", entry!.Recipe);
        Assert.Equal("S1", entry.Station);
        Assert.Equal("cam_file", entry.Camera);
        Assert.Equal(15.023, entry.X!.Value, 3);
        Assert.Equal(20.117, entry.Y!.Value, 3);
        Assert.Equal(0.12, entry.Angle!.Value, 3);
        Assert.Equal(0.92, entry.Confidence!.Value, 3);
        Assert.Equal(1, entry.Count);
        Assert.Equal(87.5, entry.ElapsedMs, 1);
        Assert.Equal(0, entry.Code); // 成功 = 0
        Assert.Equal("A03", entry.Recipe);
        Assert.True(DateTime.TryParse(entry.T, out _), "时间戳应为 ISO 格式");
    }

    [Fact]
    public void Record_FailureResult_UsesErrorCode_NoCoordinates()
    {
        using var store = new ResultLogStore(Config(_folder), NullLogger<ResultLogStore>.Instance);
        var fail = VisionResult.Fail("A03", VisionErrorCode.NoTargetFound, "未检出目标", 45.0);

        store.Record(fail);

        var file = WaitForFile(_folder, "results-*.jsonl");
        var entry = JsonSerializer.Deserialize<ResultLogEntry>(File.ReadAllLines(file).First());
        Assert.Equal((int)VisionErrorCode.NoTargetFound, entry!.Code);
        Assert.Null(entry.X);
        Assert.Null(entry.Y);
        Assert.Equal(0, entry.Count);
        Assert.Contains("未检出目标", entry.Message);
    }

    [Fact]
    public void Record_MultipleResults_AppendMultipleLines()
    {
        using var store = new ResultLogStore(Config(_folder), NullLogger<ResultLogStore>.Instance);
        store.Record(VisionResult.Success("A", [new RobotPose(1, 2, 0)], 10));
        store.Record(VisionResult.Success("A", [new RobotPose(3, 4, 0)], 11));
        store.Record(VisionResult.Fail("B", VisionErrorCode.Busy, "busy", 0));

        var file = WaitForFile(_folder, "results-*.jsonl");
        var deadline = DateTime.UtcNow.AddSeconds(3);
        var lines = Array.Empty<string>();
        while (DateTime.UtcNow < deadline)
        {
            lines = File.ReadAllLines(file);
            if (lines.Length >= 3)
                break;
            Thread.Sleep(50);
        }
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void Record_Disabled_NoFileWritten()
    {
        using var store = new ResultLogStore(Config(_folder, enabled: false), NullLogger<ResultLogStore>.Instance);

        store.Record(VisionResult.Success("A", [new RobotPose(1, 2, 0)], 10));
        Thread.Sleep(300);

        Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder).Length > 0);
    }

    [Fact]
    public void Record_NullContext_StillWrites()
    {
        using var store = new ResultLogStore(Config(_folder), NullLogger<ResultLogStore>.Instance);

        store.Record(VisionResult.Success("A", [new RobotPose(1, 2, 0)], 10));

        var file = WaitForFile(_folder, "results-*.jsonl");
        var entry = JsonSerializer.Deserialize<ResultLogEntry>(File.ReadAllLines(file).First());
        Assert.Equal("", entry!.Station);
        Assert.Equal("", entry.Camera);
    }

    [Fact]
    public void Cleanup_RemovesExpiredDayFiles()
    {
        using var store = new ResultLogStore(Config(_folder, retainedDays: 7), NullLogger<ResultLogStore>.Instance);
        Directory.CreateDirectory(_folder);
        // 造一个 10 天前的日志文件(应在清理范围内)
        var oldFile = Path.Combine(_folder, "results-" + DateTime.Now.AddDays(-10).ToString("yyyy-MM-dd") + ".jsonl");
        File.WriteAllText(oldFile, "{}\n");

        store.Record(VisionResult.Success("A", [new RobotPose(1, 2, 0)], 10));

        WaitForFile(_folder, "results-*.jsonl");
        // 写盘为后台异步线程,CI 负载高时可能延迟,放宽到 10s
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && File.Exists(oldFile))
            Thread.Sleep(50);

        Assert.False(File.Exists(oldFile), "超期日志文件应被清理");
        Assert.True(Directory.GetFiles(_folder, "results-*.jsonl").Length >= 1, "今天的日志应保留");
    }

    [Fact]
    public void Cleanup_RetainedDaysZero_KeepsAll()
    {
        using var store = new ResultLogStore(Config(_folder, retainedDays: 0), NullLogger<ResultLogStore>.Instance);
        Directory.CreateDirectory(_folder);
        var oldFile = Path.Combine(_folder, "results-" + DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd") + ".jsonl");
        File.WriteAllText(oldFile, "{}\n");

        store.Record(VisionResult.Success("A", [new RobotPose(1, 2, 0)], 10));

        WaitForFile(_folder, "results-*.jsonl");
        Thread.Sleep(300);
        Assert.True(File.Exists(oldFile), "RetainedDays=0 不应清理");
    }
}
