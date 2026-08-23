using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting.Lighting;

/// <summary>
/// None 光源控制器工厂：无操作虚拟实现（未接硬件时的调试兜底，同 FileCamera 定位）。
/// </summary>
public sealed class NoneLightControllerFactory : ILightControllerFactory
{
    public string TypeName => "None";

    public ILightController Create(LightControllerConfig config, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(config.Id))
            throw new ArgumentException("光源控制器 Id 不能为空", nameof(config));
        return new NoopLightController(config.Id);
    }
}
