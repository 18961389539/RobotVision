using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class VisionServiceLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "解除联锁后落盘失败")]
    public static partial void ClearInhibitPersistFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "配方 {Recipe}: {Message}，总耗时 {Elapsed:0}ms（{Stages}）")]
    public static partial void ProcessMessage(ILogger logger, string recipe, string message, double elapsed, string stages);

    [LoggerMessage(Level = LogLevel.Information, Message = "配方 {Recipe}: 检出 {Count} 个目标，总耗时 {Elapsed:0}ms（{Stages}）")]
    public static partial void ProcessDetections(ILogger logger, string recipe, int count, double elapsed, string stages);

    [LoggerMessage(Level = LogLevel.Error, Message = "配方 {Recipe} 处理异常")]
    public static partial void ProcessFailed(ILogger logger, Exception ex, string recipe);

    [LoggerMessage(Level = LogLevel.Warning, Message = "快照克隆失败")]
    public static partial void SnapshotCloneFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FrameProcessed 订阅者处理快照异常")]
    public static partial void FrameProcessedHandlerFailed(ILogger logger, Exception ex);
}
