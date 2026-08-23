using System.Collections.Concurrent;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 光源管理器：注册、按 Id 查找控制器，并把配方照明配置应用到取图流程。
/// 未配置任何光源时（空管理器）零开销——现有无光源配方行为完全不变。
/// </summary>
public sealed class LightingManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ILightController> _controllers = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _controllers.Count;

    public IReadOnlyCollection<string> ControllerIds => _controllers.Keys.ToArray();

    public void Register(ILightController controller)
    {
        // 覆盖同 Id 时释放旧实例（运行时重配光源）
        if (_controllers.TryGetValue(controller.Id, out var old) && !ReferenceEquals(old, controller))
            old.Dispose();
        _controllers[controller.Id] = controller;
    }

    /// <summary>移除控制器并释放实例；不存在返回 false。运行时下线光源用。</summary>
    public bool Unregister(string id)
    {
        if (!_controllers.TryRemove(id, out var controller))
            return false;
        controller.Dispose();
        return true;
    }

    /// <summary>尝试取控制器（不抛异常），供 UI 探测注册状态。</summary>
    public bool TryGet(string id, out ILightController? controller) =>
        _controllers.TryGetValue(id, out controller);

    public bool IsRegistered(string id) => _controllers.ContainsKey(id);

    public ILightController Get(string id) =>
        _controllers.TryGetValue(id, out var controller)
            ? controller
            : throw new VisionException(VisionErrorCode.LightNotRegistered, $"光源控制器未注册: {id}");

    /// <summary>
    /// 点亮光源并返回作用域：作用域 Dispose 时按配置熄灯。
    /// 未配置照明（controllerId 空 / lighting 空 / 无通道）时返回零开销空作用域，
    /// 不触碰任何控制器。稳定延时由调用方在取图前异步等待（见 <see cref="LightingScope.StabilizeDelayMs"/>）。
    /// </summary>
    public LightingScope Apply(string? controllerId, LightingConfig? lighting)
    {
        if (string.IsNullOrWhiteSpace(controllerId) || lighting is null || lighting.Channels.Count == 0)
            return LightingScope.None;

        var controller = Get(controllerId);
        controller.Apply(lighting);
        return new LightingScope(controller, lighting.StabilizeDelayMs, lighting.TurnOffAfterGrab);
    }

    /// <summary>
    /// 手动开灯（UI 调试调光）：按通道与亮度点亮并保持常亮，直到显式 <see cref="TurnOff"/>。
    /// 不参与取图管线；NoopLightController 调用无副作用。
    /// </summary>
    public void TurnOn(string id, int channel, int brightness)
    {
        var controller = Get(id);
        controller.Apply(new LightingConfig
        {
            Channels = [new LightingChannelConfig { Channel = Math.Max(1, channel), Brightness = Math.Clamp(brightness, 0, 255) }],
            TurnOffAfterGrab = false,
        });
    }

    /// <summary>手动熄灯（UI 调试调光）。控制器未注册时抛 1006。</summary>
    public void TurnOff(string id) => Get(id).TurnOff();

    public void Dispose()
    {
        foreach (var controller in _controllers.Values)
            controller.Dispose();
        _controllers.Clear();
    }
}

/// <summary>
/// 一次取图周期的光源作用域：Dispose 时按配置熄灯（幂等，重复 Dispose 安全）。
/// </summary>
public sealed class LightingScope : IDisposable
{
    /// <summary>无光源配置时的共享空作用域：零开销，IsActive = false。</summary>
    public static readonly LightingScope None = new(null, 0, false);

    private readonly ILightController? _controller;
    private readonly bool _turnOffOnDispose;
    private int _disposed;

    /// <summary>点亮到取图之间的稳定延时（ms），由调用方 await。</summary>
    public int StabilizeDelayMs { get; }

    /// <summary>当前作用域是否点亮了真实控制器（False = 无光源配置的零开销空操作）。</summary>
    public bool IsActive => _controller is not null;

    public LightingScope(ILightController? controller, int stabilizeDelayMs, bool turnOffOnDispose)
    {
        _controller = controller;
        StabilizeDelayMs = stabilizeDelayMs;
        _turnOffOnDispose = turnOffOnDispose;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_controller is not null && _turnOffOnDispose)
            _controller.TurnOff();
    }
}
