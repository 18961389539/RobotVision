using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class TcpHostedServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "TCP 服务启动失败（端口 {Port} 可能被占用），机器人链路不可用；视觉服务继续运行")]
    public static partial void StartFailed(ILogger logger, Exception ex, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "正在停止 TCP 服务...")]
    public static partial void Stopping(ILogger logger);
}
