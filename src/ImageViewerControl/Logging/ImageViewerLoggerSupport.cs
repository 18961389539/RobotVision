using System.Diagnostics.CodeAnalysis;
using ImageViewer.Abstractions;
using ImageViewer.Services;
using Microsoft.Extensions.Logging;

namespace ImageViewer.Logging;

internal static class ImageViewerLoggerSupport
{
    public static void PluginRegisterStarted(IImageViewerLogger? logger, string module)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.PluginRegisterStarted(mel, module);
        else
            logger?.LogInfo($"PluginDiscovery module={module} stage=Register result=Started");
    }

    public static void PluginRegisterSucceeded(IImageViewerLogger? logger, string module)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.PluginRegisterSucceeded(mel, module);
        else
            logger?.LogInfo($"PluginDiscovery module={module} stage=Register result=Succeeded");
    }

    public static void PluginRegisterFailed(IImageViewerLogger? logger, string module, Exception ex)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.PluginRegisterFailed(mel, module, ex);
        else
            logger?.LogError($"PluginDiscovery module={module} stage=Register result=Failed", ex);
    }

    public static void PluginAssemblySkipped(IImageViewerLogger? logger, string filePath, string reason)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.PluginAssemblySkipped(mel, filePath, reason);
        else
            logger?.LogWarning($"Skipping non-.NET assembly '{filePath}': {reason}");
    }

    public static void PluginAssemblyLoadFailed(IImageViewerLogger? logger, string filePath, Exception ex)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.PluginAssemblyLoadFailed(mel, filePath, ex);
        else
            logger?.LogError($"Failed to load plugin assembly '{filePath}'.", ex);
    }

    public static void UiOperationCanceled(IImageViewerLogger logger, string operationName)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.UiOperationCanceled(mel, operationName);
        else
            logger.LogInfo($"UI operation canceled: {operationName}; category=Cancellation");
    }

    public static void UiOperationFailed(IImageViewerLogger logger, string operationName, string category, Exception ex)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.UiOperationFailed(mel, operationName, category, ex);
        else
            logger.LogError($"UI operation failed: {operationName}; category={category}", ex);
    }

    public static void ShutdownOperationCanceled(IImageViewerLogger logger, string operationName)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.ShutdownOperationCanceled(mel, operationName);
        else
            logger.LogInfo($"Shutdown operation canceled: {operationName}; category=Cancellation");
    }

    public static void ShutdownOperationFailed(IImageViewerLogger logger, string operationName, string category, Exception ex)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.ShutdownOperationFailed(mel, operationName, category, ex);
        else
            logger.LogError($"Shutdown operation failed: {operationName}; category={category}", ex);
    }

    public static void PeriodicTaskFailed(IImageViewerLogger logger, Exception ex)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.PeriodicTaskFailed(mel, ex);
        else
            logger.LogError("Periodic image viewer task failed.", ex);
    }

    public static void NonCriticalError(IImageViewerLogger logger, string message, Exception ex)
    {
        if (TryGetMel(logger, out var mel))
            ImageViewerLog.NonCriticalError(mel, message, ex);
        else
            logger.LogError(message, ex);
    }

    private static bool TryGetMel(IImageViewerLogger? logger, [NotNullWhen(true)] out ILogger? mel)
    {
        if (logger is MicrosoftExtensionsImageViewerLogger adapter)
        {
            mel = adapter.Logger;
            return true;
        }

        mel = null;
        return false;
    }
}
