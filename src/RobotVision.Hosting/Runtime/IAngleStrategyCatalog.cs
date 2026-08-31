using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Inference.Strategies;

namespace RobotVision.Hosting;

/// <summary>角度策略工厂目录（WPF/宿主稳定入口）。</summary>
public interface IAngleStrategyCatalog
{
    IReadOnlyList<IAngleStrategyFactory> Factories { get; }
}

internal sealed class AngleStrategyCatalog(AngleStrategyTypeRegistry inner) : IAngleStrategyCatalog
{
    public IReadOnlyList<IAngleStrategyFactory> Factories => inner.Factories;
}
