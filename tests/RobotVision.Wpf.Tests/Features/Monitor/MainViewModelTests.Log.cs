using Microsoft.Extensions.Logging;

namespace RobotVision.Wpf.Tests;

internal static partial class MainViewModelTestsLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "hello info")]
    public static partial void HelloInfo(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "hello warn")]
    public static partial void HelloWarn(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "info line")]
    public static partial void InfoLine(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "error line")]
    public static partial void ErrorLine(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "line {I}")]
    public static partial void LineIndex(ILogger logger, int i);

    [LoggerMessage(Level = LogLevel.Information, Message = "x")]
    public static partial void SimpleX(ILogger logger);
}
