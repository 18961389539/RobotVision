using Microsoft.Extensions.Logging;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 文件日志测试：目录自动创建、日志条目落盘（消息/来源类别/异常堆栈）。
/// </summary>
public class FileLoggingTests
{
    [Fact]
    public void FileLogging_CreatesFolderAndWritesEntries()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_logs_" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.False(Directory.Exists(folder));

            using (var factory = LoggerFactory.Create(b => b.AddRobotVisionFileLogging(folder, retainedDays: 7)))
            {
                var logger = factory.CreateLogger("RobotVision.Tests");
                FileLoggingTestsLog.MarkerMessage(logger, 42);
                FileLoggingTestsLog.ErrorEntry(logger, new InvalidOperationException("演示异常"));
            } // dispose → 刷新并关闭文件

            var files = Directory.GetFiles(folder, "*.log");
            Assert.Single(files);

            var content = File.ReadAllText(files[0]);
            Assert.Contains("标记消息 42", content, StringComparison.Ordinal);
            Assert.Contains("错误条目", content, StringComparison.Ordinal);
            Assert.Contains("RobotVision.Tests", content, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException", content, StringComparison.Ordinal);
            Assert.Contains("演示异常", content, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void FileLogging_ConcurrentWritesFromMultipleLoggers_AllPersisted()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_logs_" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = LoggerFactory.Create(b => b.AddRobotVisionFileLogging(folder, retainedDays: 7));

            Parallel.For(0, 20, i =>
            {
                var logger = factory.CreateLogger($"Worker-{i}");
                FileLoggingTestsLog.ConcurrentEntry(logger, i);
            });

            factory.Dispose();

            var files = Directory.GetFiles(folder, "*.log");
            Assert.Single(files);
            var content = File.ReadAllText(files[0]);
            for (var i = 0; i < 20; i++)
                Assert.Contains($"并发条目 {i}", content, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

