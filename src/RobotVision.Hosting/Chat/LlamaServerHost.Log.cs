using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting.Chat;

internal static partial class LlamaServerHostLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "已启动 llama-server {Pid} {Args}")]
    public static partial void Started(ILogger logger, int pid, string args);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void Warning(ILogger logger, string message);
}
