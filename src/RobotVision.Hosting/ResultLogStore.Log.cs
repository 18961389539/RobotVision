using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class ResultLogStoreLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "提交结果日志失败（不影响管线）")]
    public static partial void EnqueueFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "写结果 JSONL 失败（不影响管线）")]
    public static partial void JsonlWriteFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "写结果 SQLite 失败（不影响管线）")]
    public static partial void SqliteWriteFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "清理结果日志失败: {File}")]
    public static partial void CleanupFailed(ILogger logger, Exception ex, string file);
}
