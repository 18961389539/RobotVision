using System.Collections.Concurrent;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 光源管理器：注册、按 Id 查找控制器，并把配方照明配置应用到取图流程。
/// 未配置任何光源时（空管理器）零开销——现有无光源配方行为完全不变。
/// 按 Id 门闩与 <see cref="Cameras.CameraManager"/> 同构：热替换不得在 Send 中途 Dispose。
/// </summary>
public sealed class LightingManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ILightController> _controllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public int Count => _controllers.Count;

    public IReadOnlyCollection<string> ControllerIds => _controllers.Keys.ToArray();

    public void Register(ILightController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(controller.Id);
        gate.Wait();
        try
        {
            // 覆盖同 Id 时释放旧实例（运行时重配光源）。
            // 在门闩内 Dispose：与在途 Apply/TurnOff 互斥，不会在发送中途拆掉套接字。
            if (_controllers.TryGetValue(controller.Id, out var old) && !ReferenceEquals(old, controller))
                old.Dispose();
            _controllers[controller.Id] = controller;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>移除控制器并释放实例；不存在返回 false。运行时下线光源用。</summary>
    public bool Unregister(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(id);
        gate.Wait();
        try
        {
            if (!_controllers.TryRemove(id, out var controller))
                return false;
            controller.Dispose();
            return true;
        }
        finally
        {
            gate.Release();
        }
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
    /// 已注册但指令发送失败抛 1020，不进入取图。
    /// 门闩在作用域存活期间保持，避免稳定延时/取图/推理阶段并发 Apply 交错。
    /// </summary>
    public LightingScope Apply(string? controllerId, LightingConfig? lighting)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(controllerId) || lighting is null || lighting.Channels.Count == 0)
            return LightingScope.None;

        var gate = Gate(controllerId);
        gate.Wait();
        try
        {
            var controller = Get(controllerId);
            if (!controller.Apply(lighting))
            {
                throw new VisionException(
                    VisionErrorCode.LightCommandFailed,
                    $"光源 {controllerId} 指令发送失败");
            }

            return new LightingScope(this, controllerId, gate, lighting.StabilizeDelayMs, lighting.TurnOffAfterGrab);
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    /// <summary>
    /// 手动开灯（UI 调试调光）：按通道与亮度点亮并保持常亮，直到显式 <see cref="TurnOff"/>。
    /// 不参与取图管线；NoopLightController 调用无副作用。发送失败抛 1020。
    /// </summary>
    public void TurnOn(string id, int channel, int brightness)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(id);
        gate.Wait();
        try
        {
            var controller = Get(id);
            var lighting = new LightingConfig
            {
                Channels = [new LightingChannelConfig { Channel = Math.Max(1, channel), Brightness = Math.Clamp(brightness, 0, 255) }],
                TurnOffAfterGrab = false,
            };
            if (!controller.Apply(lighting))
            {
                throw new VisionException(
                    VisionErrorCode.LightCommandFailed,
                    $"光源 {id} 指令发送失败");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>手动熄灯（UI 调试调光）。控制器未注册时抛 1006。</summary>
    public void TurnOff(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(id);
        gate.Wait();
        try
        {
            Get(id).TurnOff();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>取图结束后熄灯（调用方已持有该 Id 门闩）。控制器已下线则跳过，不抛 1006。</summary>
    internal void TurnOffWhileHoldingGate(string id)
    {
        if (_disposed)
            return;
        if (_controllers.TryGetValue(id, out var controller))
        {
            try { controller.TurnOff(); }
            catch (VisionException) { /* 熄灯尽力而为，不让取图收尾失败 */ }
        }
    }

    private SemaphoreSlim Gate(string id) =>
        _gates.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        var gateIds = _gates.Keys.ToArray();
        Array.Sort(gateIds, StringComparer.OrdinalIgnoreCase);
        foreach (var id in gateIds)
        {
            if (_gates.TryGetValue(id, out var gate))
                gate.Wait();
        }

        try
        {
            foreach (var controller in _controllers.Values)
                controller.Dispose();
            _controllers.Clear();
        }
        finally
        {
            foreach (var gate in _gates.Values)
            {
                try { gate.Release(); }
                catch (ObjectDisposedException)
                {
                    // 关闭路径上另一线程恰好完成 Dispose 的竞态：门闩已释放，忽略即可
                    System.Diagnostics.Trace.TraceWarning(
                        "LightingManager: 门闩释放与 Dispose 竞态（预期，忽略）");
                }
            }

            foreach (var gate in _gates.Values)
                gate.Dispose();
            _gates.Clear();
        }
    }
}

/// <summary>
/// 一次取图周期的光源作用域：Dispose 时按配置熄灯（幂等，重复 Dispose 安全）。
/// </summary>
public sealed class LightingScope : IDisposable
{
    /// <summary>无光源配置时的共享空作用域：零开销，IsActive = false。</summary>
    public static readonly LightingScope None = new(null, 0, false);

    private readonly LightingManager? _manager;
    private readonly string? _id;
    private readonly ILightController? _controller;
    private readonly SemaphoreSlim? _gate;
    private readonly bool _turnOffOnDispose;
    private int _disposed;

    /// <summary>点亮到取图之间的稳定延时（ms），由调用方 await。</summary>
    public int StabilizeDelayMs { get; }

    /// <summary>当前作用域是否点亮了真实控制器（False = 无光源配置的零开销空操作）。</summary>
    public bool IsActive => _manager is not null || _controller is not null;

    public LightingScope(ILightController? controller, int stabilizeDelayMs, bool turnOffOnDispose)
    {
        _controller = controller;
        StabilizeDelayMs = stabilizeDelayMs;
        _turnOffOnDispose = turnOffOnDispose;
    }

    internal LightingScope(
        LightingManager manager,
        string id,
        SemaphoreSlim gate,
        int stabilizeDelayMs,
        bool turnOffOnDispose)
    {
        _manager = manager;
        _id = id;
        _gate = gate;
        StabilizeDelayMs = stabilizeDelayMs;
        _turnOffOnDispose = turnOffOnDispose;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            if (_manager is not null && _id is not null && _turnOffOnDispose)
                _manager.TurnOffWhileHoldingGate(_id);
            else if (_controller is not null && _turnOffOnDispose)
                _controller.TurnOff();
        }
        finally
        {
            _gate?.Release();
        }
    }
}
