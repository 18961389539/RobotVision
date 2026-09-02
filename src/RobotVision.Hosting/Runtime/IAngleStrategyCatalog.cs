using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;

namespace RobotVision.Hosting;

/// <summary>角度策略下拉选项（WPF 稳定入口，不暴露 Infrastructure 工厂类型）。</summary>
public sealed record AngleModeOption(AngleMode Mode, string Label);

/// <summary>角度策略目录（WPF/宿主稳定入口）。</summary>
public interface IAngleStrategyCatalog
{
    IReadOnlyList<AngleModeOption> Options { get; }
}

internal sealed class AngleStrategyCatalog(AngleStrategyTypeRegistry inner) : IAngleStrategyCatalog
{
    private IReadOnlyList<AngleModeOption>? _options;

    public IReadOnlyList<AngleModeOption> Options =>
        _options ??= inner.Factories.Select(f => new AngleModeOption(f.Mode, f.Label)).ToList();
}
