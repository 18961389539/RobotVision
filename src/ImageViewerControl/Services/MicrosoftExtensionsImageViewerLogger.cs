using ImageViewer.Abstractions;
using ImageViewer.Logging;
using Microsoft.Extensions.Logging;

namespace ImageViewer.Services;

public sealed class MicrosoftExtensionsImageViewerLogger : IImageViewerLogger
{
    private readonly ILogger<MicrosoftExtensionsImageViewerLogger> _logger;

    public MicrosoftExtensionsImageViewerLogger(ILogger<MicrosoftExtensionsImageViewerLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal ILogger Logger => _logger;

    public void LogInfo(string message) => ImageViewerLog.Information(_logger, message);

    public void LogWarning(string message) => ImageViewerLog.Warning(_logger, message);

    public void LogError(string message, Exception? exception = null) =>
        ImageViewerLog.Error(_logger, message, exception);
}
