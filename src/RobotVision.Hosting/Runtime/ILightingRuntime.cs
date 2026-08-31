using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting;

/// <summary>光源取图作用域（WPF 不直接依赖 <see cref="LightingScope"/>）。</summary>
public interface ILightingGrabScope : IDisposable
{
    int StabilizeDelayMs { get; }
}

/// <summary>光源运行时（WPF/宿主稳定入口）。</summary>
public interface ILightingRuntime
{
    IReadOnlyCollection<string> ControllerIds { get; }
    bool TryGet(string id, out ILightController? controller);
    bool IsRegistered(string id);
    ILightingGrabScope Apply(string? controllerId, LightingConfig? lighting);
    void Register(ILightController controller);
    bool Unregister(string id);
    void TurnOn(string id, int channel, int brightness);
    void TurnOff(string id);
}

internal sealed class LightingGrabScope(LightingScope inner) : ILightingGrabScope
{
    public int StabilizeDelayMs => inner.StabilizeDelayMs;
    public void Dispose() => inner.Dispose();
}

internal sealed class LightingRuntime(LightingManager inner) : ILightingRuntime
{
    public IReadOnlyCollection<string> ControllerIds => inner.ControllerIds;
    public bool TryGet(string id, out ILightController? controller) => inner.TryGet(id, out controller);
    public bool IsRegistered(string id) => inner.IsRegistered(id);
    public ILightingGrabScope Apply(string? controllerId, LightingConfig? lighting) =>
        new LightingGrabScope(inner.Apply(controllerId, lighting));
    public void Register(ILightController controller) => inner.Register(controller);
    public bool Unregister(string id) => inner.Unregister(id);
    public void TurnOn(string id, int channel, int brightness) => inner.TurnOn(id, channel, brightness);
    public void TurnOff(string id) => inner.TurnOff(id);
}
