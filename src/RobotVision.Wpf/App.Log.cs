using Microsoft.Extensions.Logging;
using RobotVision.Hosting;

namespace RobotVision.WpfHost;

internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "配方 {Recipe} 加载失败: {Error}")]
    public static partial void RecipeLoadFailed(ILogger logger, string recipe, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Generic host stop timed out; skipped synchronous host.Dispose")]
    public static partial void HostStopTimedOut(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Application shutdown incomplete: {Outcome}")]
    public static partial void HostShutdownIncomplete(ILogger logger, ShutdownOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Generic host stop failed during application exit")]
    public static partial void HostStopFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dispatcher unhandled UI exception #{Count}")]
    public static partial void DispatcherUnhandled(ILogger logger, Exception ex, int count);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Too many unhandled UI exceptions; shutting down")]
    public static partial void TooManyUnhandledUiExceptions(ILogger logger, Exception ex);
}
