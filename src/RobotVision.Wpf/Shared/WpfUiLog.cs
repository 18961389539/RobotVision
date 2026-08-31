using Microsoft.Extensions.Logging;

namespace RobotVision.WpfHost.Shared;

internal static partial class WpfUiLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "UI background work failed: {Operation}")]
    public static partial void UiAsyncFailed(ILogger logger, Exception ex, string operation);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to load failure preview for {Path}")]
    public static partial void FailurePreviewFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save lighting controller {Id}")]
    public static partial void LightingSaveFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to add lighting controller {Id}")]
    public static partial void LightingAddFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Monitor snapshot overlay failed for recipe {Recipe}")]
    public static partial void MonitorSnapshotOverlayFailed(ILogger logger, Exception ex, string recipe);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load analysis snapshot")]
    public static partial void AnalysisLoadFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chat probe failed")]
    public static partial void ChatProbeFailed(ILogger logger, Exception ex);
}
