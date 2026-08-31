using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class FailureImageStoreLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "提交失败现场图像留存时出错（不影响产线管线）")]
    public static partial void EnqueueFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "已留存失败现场: {File}（{Code} {Message}）")]
    public static partial void Saved(ILogger logger, string file, int code, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "留存失败现场图像时出错（不影响产线管线）")]
    public static partial void SaveFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "清理失败现场文件出错: {Path}")]
    public static partial void CleanupFailed(ILogger logger, Exception ex, string path);
}
