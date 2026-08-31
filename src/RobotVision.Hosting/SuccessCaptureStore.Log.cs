using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class SuccessCaptureStoreLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "提交成功现场图留存时出错（不影响管线）")]
    public static partial void EnqueueFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "留存成功现场图时出错（不影响管线）")]
    public static partial void SaveFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "清理成功现场图目录失败: {Dir}")]
    public static partial void CleanupDirFailed(ILogger logger, Exception ex, string dir);
}
