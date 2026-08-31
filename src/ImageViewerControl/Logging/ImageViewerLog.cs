using Microsoft.Extensions.Logging;

namespace ImageViewer.Logging;

internal static partial class ImageViewerLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "{ImageViewerMessage}")]
    public static partial void Information(ILogger logger, string imageViewerMessage);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "{ImageViewerMessage}")]
    public static partial void Warning(ILogger logger, string imageViewerMessage);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "{ImageViewerMessage}")]
    public static partial void Error(ILogger logger, string imageViewerMessage, Exception? exception);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "PluginDiscovery module={Module} stage=Register result=Started")]
    public static partial void PluginRegisterStarted(ILogger logger, string module);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "PluginDiscovery module={Module} stage=Register result=Succeeded")]
    public static partial void PluginRegisterSucceeded(ILogger logger, string module);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Error, Message = "PluginDiscovery module={Module} stage=Register result=Failed")]
    public static partial void PluginRegisterFailed(ILogger logger, string module, Exception ex);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "Skipping non-.NET assembly '{FilePath}': {Reason}")]
    public static partial void PluginAssemblySkipped(ILogger logger, string filePath, string reason);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Error, Message = "Failed to load plugin assembly '{FilePath}'")]
    public static partial void PluginAssemblyLoadFailed(ILogger logger, string filePath, Exception ex);

    [LoggerMessage(EventId = 1110, Level = LogLevel.Information, Message = "UI operation canceled: {OperationName}; category=Cancellation")]
    public static partial void UiOperationCanceled(ILogger logger, string operationName);

    [LoggerMessage(EventId = 1111, Level = LogLevel.Error, Message = "UI operation failed: {OperationName}; category={Category}")]
    public static partial void UiOperationFailed(ILogger logger, string operationName, string category, Exception ex);

    [LoggerMessage(EventId = 1112, Level = LogLevel.Information, Message = "Shutdown operation canceled: {OperationName}; category=Cancellation")]
    public static partial void ShutdownOperationCanceled(ILogger logger, string operationName);

    [LoggerMessage(EventId = 1113, Level = LogLevel.Error, Message = "Shutdown operation failed: {OperationName}; category={Category}")]
    public static partial void ShutdownOperationFailed(ILogger logger, string operationName, string category, Exception ex);

    [LoggerMessage(EventId = 1120, Level = LogLevel.Error, Message = "Periodic image viewer task failed")]
    public static partial void PeriodicTaskFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1121, Level = LogLevel.Error, Message = "Image viewer non-critical error: {Message}")]
    public static partial void NonCriticalError(ILogger logger, string message, Exception ex);
}
