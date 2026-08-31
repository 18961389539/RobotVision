using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class ProcessHealthStoreLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "恢复过程能力统计失败（将从零开始）")]
    public static partial void RestoreFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "过程能力状态落盘失败")]
    public static partial void StatePersistFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "过程能力落盘失败（不影响管线）")]
    public static partial void PersistFailed(ILogger logger, Exception ex);
}
