using System.Globalization;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting.Lighting;

/// <summary>
/// 网络光源控制器工厂（UDP/TCP）：参照 VPDLFramework 的 ECLightControl，
/// 接入真实频闪控制器时用 <c>LightControllerTypeRegistry.Default.Register(new NetworkLightControllerFactory())</c>
/// 一行注册——服务、UI 类型下拉、运行时注册自动生效。
/// 配置：Type="Network"，Endpoint="192.168.0.66:4001"，Protocol="Udp|Tcp"（默认 Tcp），
/// LocalEndpoint="0.0.0.0:5001"（可选），TimeoutMs，ReconnectAttempts。
/// </summary>
public sealed class NetworkLightControllerFactory : ILightControllerFactory
{
    public string TypeName => "Network";

    public ILightController Create(LightControllerConfig config, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(config.Id))
            throw new ArgumentException("光源控制器 Id 不能为空", nameof(config));
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException($"网络光源控制器 {config.Id} 需配置 Endpoint（host:port）", nameof(config));

        var (host, port) = ParseEndpoint(config.Endpoint);
        var protocol = string.Equals(config.Protocol, "Udp", StringComparison.OrdinalIgnoreCase)
            ? LightProtocol.Udp
            : LightProtocol.Tcp;

        if (string.IsNullOrWhiteSpace(config.LocalEndpoint))
        {
            if (logger is not null)
                NetworkLightControllerFactoryLog.Registered(logger, config.Id, protocol.ToString(), config.Endpoint);
            return new NetworkLightController(config.Id, protocol, host, port,
                timeoutMs: config.TimeoutMs, reconnectAttempts: config.ReconnectAttempts);
        }

        // UDP 固定本地端口：绑定本地端点后同一端口收发（应答/心跳场景）
        var (localHost, localPort) = ParseEndpoint(config.LocalEndpoint);
        if (logger is not null)
            NetworkLightControllerFactoryLog.RegisteredWithLocal(
                logger, config.Id, protocol.ToString(), config.Endpoint, config.LocalEndpoint);
        return new NetworkLightController(config.Id, protocol, host, port,
            timeoutMs: config.TimeoutMs, reconnectAttempts: config.ReconnectAttempts,
            localEndPoint: new System.Net.IPEndPoint(
                System.Net.IPAddress.Parse(localHost == "0.0.0.0" ? "0.0.0.0" : localHost), localPort));
    }

    /// <summary>解析 host:port（支持 IPv4 与主机名）。</summary>
    internal static (string Host, int Port) ParseEndpoint(string endpoint)
    {
        var idx = endpoint.LastIndexOf(':');
        if (idx <= 0 || idx == endpoint.Length - 1)
            throw new ArgumentException($"端点格式应为 host:port：{endpoint}");
        var host = endpoint[..idx].Trim('[', ']');
        if (host.Length == 0)
            throw new ArgumentException($"端点缺少主机：{endpoint}");
        if (!int.TryParse(endpoint[(idx + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
            throw new ArgumentException($"端点端口非法：{endpoint}");
        return (host, port);
    }
}
