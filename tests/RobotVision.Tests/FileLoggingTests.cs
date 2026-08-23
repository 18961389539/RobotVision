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
                logger.LogInformation("标记消息 {Value}", 42);
                logger.LogError(new InvalidOperationException("演示异常"), "错误条目");
            } // dispose → 刷新并关闭文件

            var files = Directory.GetFiles(folder, "*.log");
            Assert.Single(files);

            var content = File.ReadAllText(files[0]);
            Assert.Contains("标记消息 42", content);
            Assert.Contains("错误条目", content);
            Assert.Contains("RobotVision.Tests", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("演示异常", content);
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
                logger.LogInformation("并发条目 {Index}", i);
            });

            factory.Dispose();

            var files = Directory.GetFiles(folder, "*.log");
            Assert.Single(files);
            var content = File.ReadAllText(files[0]);
            for (var i = 0; i < 20; i++)
                Assert.Contains($"并发条目 {i}", content);
        }
        finally
        {
            try { Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

