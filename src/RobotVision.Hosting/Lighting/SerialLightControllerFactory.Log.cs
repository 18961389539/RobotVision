using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting.Lighting;

internal static partial class SerialLightControllerFactoryLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "光源 {Id}：Serial → {Port} @ {BaudRate}")]
    public static partial void Registered(ILogger logger, string id, string port, int baudRate);
}
