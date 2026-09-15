using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting.Lighting;

/// <summary>
/// 串口光源控制器工厂（东冠 OSE PWD，RS232）：TypeName="Serial"。
/// 配置：Type="Serial"，Port="COM5"，BaudRate=9600，TimeoutMs，ChannelCount。
/// 已在 <see cref="LightControllerTypeRegistry.CreateDefault"/> 内置注册。
/// </summary>
public sealed class SerialLightControllerFactory : ILightControllerFactory
{
    public string TypeName => "Serial";

    public ILightController Create(LightControllerConfig config, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(config.Id))
            throw new ArgumentException("光源控制器 Id 不能为空", nameof(config));
        if (string.IsNullOrWhiteSpace(config.Port))
            throw new ArgumentException($"串口光源控制器 {config.Id} 需配置 Port（如 COM5）", nameof(config));

        var baudRate = config.BaudRate is >= 1200 and <= 921600 ? config.BaudRate : 9600;
        var channelCount = config.ChannelCount is >= 1 and <= 9
            ? config.ChannelCount
            : DongguanLightProtocol.DefaultChannelCount;
        if (logger is { } log)
            SerialLightControllerFactoryLog.Registered(log, config.Id, config.Port, baudRate);
        return new SerialLightController(
            config.Id, config.Port, baudRate, timeoutMs: config.TimeoutMs, channelCount: channelCount);
    }
}
