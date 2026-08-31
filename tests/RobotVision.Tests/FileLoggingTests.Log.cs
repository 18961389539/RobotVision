using Microsoft.Extensions.Logging;

namespace RobotVision.Tests;

internal static partial class FileLoggingTestsLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "标记消息 {Value}")]
    public static partial void MarkerMessage(ILogger logger, int value);

    [LoggerMessage(Level = LogLevel.Error, Message = "错误条目")]
    public static partial void ErrorEntry(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "并发条目 {Index}")]
    public static partial void ConcurrentEntry(ILogger logger, int index);
}
