using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class SqliteResultStoreLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "关闭结果库连接")]
    public static partial void CloseConnection(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "清理结果库超期行失败（不影响管线）")]
    public static partial void CleanupFailed(ILogger logger, Exception ex);
}
