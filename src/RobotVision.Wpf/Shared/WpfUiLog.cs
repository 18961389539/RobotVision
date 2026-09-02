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

    // 删除失败图/元数据失败：此前完全静默，用户会以为已删除成功。
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete failure artifact {Path}")]
    public static partial void FailureDeleteFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Preview in-flight grab did not finish within {TimeoutMs} ms; disposing session anyway")]
    public static partial void PreviewDrainTimeout(ILogger logger, double timeoutMs);

    // P2-5：此前完全静默的 catch 补留痕（失败不影响功能，但须可观测）
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save model test prefs {Path}")]
    public static partial void ModelPrefsSaveFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load model test prefs {Path}; falling back to defaults")]
    public static partial void ModelPrefsLoadFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load recipe health hints for recipe {Name}")]
    public static partial void RecipeHealthHintFailed(ILogger logger, Exception ex, string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to clear calibration temp folder {Folder}")]
    public static partial void CalibTempClearFailed(ILogger logger, Exception ex, string folder);
}
