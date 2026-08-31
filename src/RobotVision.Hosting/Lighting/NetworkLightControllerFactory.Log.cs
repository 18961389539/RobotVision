using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting.Lighting;

internal static partial class NetworkLightControllerFactoryLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "光源 {Id}：{Protocol} → {Endpoint}")]
    public static partial void Registered(ILogger logger, string id, string protocol, string endpoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "光源 {Id}：{Protocol} → {Endpoint}（本地 {LocalEndpoint}）")]
    public static partial void RegisteredWithLocal(ILogger logger, string id, string protocol, string endpoint, string localEndpoint);
}
