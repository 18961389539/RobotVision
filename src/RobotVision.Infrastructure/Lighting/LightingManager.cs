using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<LightingManager>? _logger;
    private bool _disposed;

    /// <summary>
    /// 可选日志。只用于两个"否则完全静默"的路径：取图后自动熄灯失败、关闭时门闩排空超时。
    /// 传 null 完全可用（测试大量直接 <c>new LightingManager()</c>），不传则这两条路径只静默跳过。
    /// </summary>
    public LightingManager(ILogger<LightingManager>? logger = null) => _logger = logger;

    /// <summary>
    /// 关闭时等待在途 Apply/TurnOff 结束的最长时间，超时即放弃等待继续关闭。
    /// <para>
    /// 不能无限等：未 Dispose 的 <see cref="LightingScope"/> 会一直持有门闩，无界等待会让**进程退不掉**。
    /// 与 <c>CameraManager.DefaultGateDrainTimeout</c> 对齐——相机侧已踩过这个坑并这样修好了。
    /// </para>
    /// </summary>
    public static TimeSpan DefaultGateDrainTimeout { get; } = TimeSpan.FromSeconds(3);

    public int Count => _controllers.Count;

    public IReadOnlyCollection<string> ControllerIds => _controllers.Keys.ToArray();

    /// <summary>
    /// 临时调试开关：true 时屏蔽取图后的**自动**熄灯——<see cref="TurnOffWhileHoldingGate"/>
    /// 变成空操作，灯在取图结束后保持点亮，直到显式 <see cref="TurnOff"/> 或进程退出。
    /// 默认 false（产线行为：按配方 <c>TurnOffAfterGrab</c> 熄灯）。
    /// 由宿主按环境变量 <c>ROBOTVISION_KEEP_LIGHT_ON</c> 置位，见 ServiceCollectionExtensions.RegisterLighting。
    /// 只影响自动熄灯：手动 <see cref="TurnOff"/> 照常生效，下一次 <see cref="Apply"/> 照常改写亮度。
    /// 关闭时不影响取图链路本身——<c>LightingScope.Dispose</c> 幂等，仅熄灯动作被跳过。
    /// </summary>
    public bool SuppressAutoTurnOff { get; set; }

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
                    $"光源 {controllerId} 指令发送失败{TransportCause(controller)}");
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
                    $"光源 {id} 指令发送失败{TransportCause(controller)}");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 手动熄灯（UI 调试调光）。控制器未注册时抛 1006；指令发送失败抛 1020（不再静默成功）。
    /// </summary>
    public void TurnOff(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(id);
        gate.Wait();
        try
        {
            var controller = Get(id);
            if (!controller.TurnOff())
            {
                throw new VisionException(
                    VisionErrorCode.LightCommandFailed,
                    $"光源 {id} 熄灯指令发送失败{TransportCause(controller)}");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 取图结束后熄灯（调用方已持有该 Id 门闩）。控制器已下线则跳过，不抛 1006。
    /// <see cref="SuppressAutoTurnOff"/> 为 true 时整体跳过（临时调试开关，灯保持点亮）。
    /// <para>
    /// **失败一律不上抛**（取图收尾不能被熄灯失败打断），但**必须留痕**：
    /// 这条路径不出现在任何 UI 上，若静默，现场只会看到"灯一直亮着"而查无实据
    /// （2026-09-14 排查光源时就吃过"故障看不见"的亏）。日志需注入 logger 才生效。
    /// </para>
    /// </summary>
    internal void TurnOffWhileHoldingGate(string id)
    {
        if (_disposed)
            return;
        if (SuppressAutoTurnOff)
            return;
        if (!_controllers.TryGetValue(id, out var controller))
            return;

        try
        {
            if (controller.TurnOff())
                return;

            if (_logger is not null)
                LightingManagerLog.AutoTurnOffRejected(_logger, id, TransportCause(controller));
        }
        catch (VisionException ex)
        {
            if (_logger is not null)
                LightingManagerLog.AutoTurnOffThrew(_logger, ex, id);
        }
    }

    private SemaphoreSlim Gate(string id) =>
        _gates.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// 把控制器的传输层失败原因附到消息上（仅当它实现了 <see cref="ILightDiagnostics"/>）。
    /// 串口光源未打开/被占用时，只报「指令发送失败」现场无法判断原因，故带上具体异常。
    /// </summary>
    private static string TransportCause(ILightController controller) =>
        controller is ILightDiagnostics { LastTransportError: { Length: > 0 } error }
            ? $"（{error}）"
            : "";

    public void Dispose()
    {
        if (_disposed)
            return;

        // 先置 _disposed 再排空：所有入口都有 ObjectDisposedException.ThrowIf(_disposed)，
        // 因此排空期间不会再有新操作来抢门闩，剩下的只是等在途者结束。
        _disposed = true;

        DrainGates(DefaultGateDrainTimeout);

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
                    if (_logger is not null)
                        LightingManagerLog.GateReleaseRacedWithDispose(_logger);
                }
            }

            foreach (var gate in _gates.Values)
                gate.Dispose();
            _gates.Clear();
        }
    }

    /// <summary>在 <paramref name="timeout"/> 内逐个等在途者释放门闩；超时则记录并继续（不放任进程退不掉）。</summary>
    private void DrainGates(TimeSpan timeout)
    {
        var gateIds = _gates.Keys.ToArray();
        if (gateIds.Length == 0)
            return;

        Array.Sort(gateIds, StringComparer.OrdinalIgnoreCase);
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        foreach (var id in gateIds)
        {
            var remainingMs = deadline - Environment.TickCount64;
            if (remainingMs <= 0)
            {
                LogGateDrainTimedOut(id);
                continue;
            }

            if (_gates.TryGetValue(id, out var gate) && !gate.Wait(TimeSpan.FromMilliseconds(remainingMs)))
                LogGateDrainTimedOut(id);
        }
    }

    private void LogGateDrainTimedOut(string id)
    {
        if (_logger is not null)
            LightingManagerLog.GateDrainTimedOut(_logger, id);
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
