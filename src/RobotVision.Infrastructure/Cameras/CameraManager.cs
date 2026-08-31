using System.Collections.Concurrent;
using System.Diagnostics;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>按 Id 串行取图的分段结果：门闩等待 vs 持锁采集。</summary>
public readonly record struct GrabTrace(CameraFrame Frame, double GateWaitMs, double GrabMs);

/// <summary>
/// 相机管理类：负责相机注册、按 Id 查找与取图。
/// 真实相机（海康/Basler 等）实现 ICamera 后调用 Register 接入。
/// 取图按相机 Id 串行：SDK 非线程安全，产线 TRIGGER 与 UI 预览必须走同一把锁。
/// </summary>
public sealed class CameraManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ICamera> _cameras = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private string[]? _cachedIds;
    private bool _disposed;

    public int Count => _cameras.Count;

    /// <summary>已注册相机 Id 列表（结果缓存，注册/注销时失效）。
    /// 返回只读包装而非内部数组——直接暴露数组给调用方可被修改，破坏内部缓存一致性。</summary>
    public IReadOnlyCollection<string> CameraIds
    {
        get
        {
            lock (_lock)
                return Array.AsReadOnly(_cachedIds ??= _cameras.Keys.ToArray());
        }
    }

    public void Register(ICamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(camera.Id);
        gate.Wait();
        try
        {
            // 覆盖同 Id 时释放旧实例（运行时重配相机）。
            // 在门闩内 Dispose：与在途 Grab 互斥，不会在采集中途拆掉 SDK 句柄。
            if (_cameras.TryGetValue(camera.Id, out var old) && !ReferenceEquals(old, camera))
                old.Dispose();
            _cameras[camera.Id] = camera;
            InvalidateIds();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>移除相机并释放实例；不存在返回 false。运行时下线相机用。</summary>
    public bool Unregister(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(id);
        gate.Wait();
        try
        {
            if (!_cameras.TryRemove(id, out var camera))
                return false;
            camera.Dispose();
            InvalidateIds();
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>尝试取相机（不抛异常），供 UI 探测注册状态。不要对返回实例直接 Grab——
    /// 会绕过按 Id 串行门闩；取图请用 <see cref="Grab(string, CancellationToken)"/>。</summary>
    public bool TryGet(string id, out ICamera? camera) =>
        _cameras.TryGetValue(id, out camera);

    public bool IsRegistered(string id) => _cameras.ContainsKey(id);

    /// <summary>取图不可用时的原因（未注册 / 初始化失败）；可用时返回 null。</summary>
    public string? GetGrabErrorHint(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "请先选择相机";
        if (!_cameras.TryGetValue(id, out var camera))
            return $"相机未注册: {id}";
        return camera is FailedCamera failed ? failed.FaultMessage : null;
    }

    public ICamera Get(string id) =>
        _cameras.TryGetValue(id, out var camera)
            ? camera
            : throw new VisionException(VisionErrorCode.CameraNotRegistered, $"相机未注册: {id}");

    /// <summary>按 Id 串行取图（已注册实例）。</summary>
    public CameraFrame Grab(string id, CancellationToken ct = default) =>
        GrabCore(id, () => Get(id).Grab(ct), ct);

    /// <summary>
    /// 示教/框选：文件夹相机再读上次文件、不推进回放下标；其它相机与 <see cref="Grab(string, CancellationToken)"/> 相同。
    /// </summary>
    public CameraFrame GrabForTeach(string id, CancellationToken ct = default) =>
        GrabCore(id, () =>
        {
            var cam = Get(id);
            return cam is FileCamera file ? file.RepeatLast(ct) : cam.Grab(ct);
        }, ct);

    /// <summary>按相机 Id 串行取图（临时实例与已注册同 Id 互斥，供「先试后存」）。</summary>
    public CameraFrame Grab(ICamera camera, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return GrabCore(camera.Id, () => camera.Grab(ct), ct);
    }

    /// <summary>异步获锁后取图，避免在 UI/管线线程上同步 Wait。</summary>
    public async Task<CameraFrame> GrabAsync(string id, CancellationToken ct = default)
    {
        var trace = await GrabTracedAsync(id, ct).ConfigureAwait(false);
        return trace.Frame;
    }

    /// <summary>异步获锁后对任意实例取图（与同 Id 的已注册相机互斥）。</summary>
    public async Task<CameraFrame> GrabAsync(ICamera camera, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var trace = await GrabTracedAsync(camera, ct).ConfigureAwait(false);
        return trace.Frame;
    }

    /// <summary>与 <see cref="GrabAsync(string, CancellationToken)"/> 相同，额外返回等锁 / 采集耗时。</summary>
    public Task<GrabTrace> GrabTracedAsync(string id, CancellationToken ct = default) =>
        GrabCoreTracedAsync(id, () => Get(id).Grab(ct), ct);

    /// <summary>与 <see cref="GrabAsync(ICamera, CancellationToken)"/> 相同，额外返回等锁 / 采集耗时。</summary>
    public Task<GrabTrace> GrabTracedAsync(ICamera camera, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return GrabCoreTracedAsync(camera.Id, () => camera.Grab(ct), ct);
    }

    private CameraFrame GrabCore(string id, Func<CameraFrame> grab, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(id);
        gate.Wait(ct);
        try
        {
            return grab();
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<GrabTrace> GrabCoreTracedAsync(string id, Func<CameraFrame> grab, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var gate = Gate(id);
        var sw = Stopwatch.StartNew();
        await gate.WaitAsync(ct).ConfigureAwait(false);
        var waitMs = sw.Elapsed.TotalMilliseconds;
        try
        {
            // WaitAsync 若同步完成会停在调用线程；从 UI 进来时必须把 pylon Grab 丢到线程池。
            sw.Restart();
            var frame = await Task.Run(() => grab(), ct).ConfigureAwait(false);
            return new GrabTrace(frame, waitMs, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim Gate(string id) =>
        _gates.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));

    private void InvalidateIds()
    {
        lock (_lock)
            _cachedIds = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var camera in _cameras.Values)
            camera.Dispose();
        _cameras.Clear();
        foreach (var gate in _gates.Values)
            gate.Dispose();
        _gates.Clear();
        InvalidateIds();
    }
}
