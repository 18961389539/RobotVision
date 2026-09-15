using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting.Chat;

internal static partial class ChatUiHostLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "工艺助手 UI 已监听 {Origin}")]
    public static partial void Started(ILogger logger, string origin);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void Warning(ILogger logger, string message);
}
