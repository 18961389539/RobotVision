using System.Collections.Concurrent;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 相机管理类：负责相机注册、按 Id 查找与取图。
/// 真实相机（海康/Basler 等）实现 ICamera 后调用 Register 接入。
/// </summary>
public sealed class CameraManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ICamera> _cameras = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private string[]? _cachedIds;

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
        // 覆盖同 Id 时释放旧实例（运行时重配相机）。
        // 释放旧实例前必须确认不是同一实例（重复注册不应自我释放）；
        // 若旧实例正被使用（取图中），其 Grab 内部锁会与 Dispose 互斥，
        // 锁内复查 _disposed 后以 1003 失败，不会出现悬垂访问。
        if (_cameras.TryGetValue(camera.Id, out var old) && !ReferenceEquals(old, camera))
            old.Dispose();
        _cameras[camera.Id] = camera;
        InvalidateIds();
    }

    /// <summary>移除相机并释放实例；不存在返回 false。运行时下线相机用。</summary>
    public bool Unregister(string id)
    {
        if (!_cameras.TryRemove(id, out var camera))
            return false;
        camera.Dispose();
        InvalidateIds();
        return true;
    }

    /// <summary>尝试取相机（不抛异常），供 UI 探测注册状态。</summary>
    public bool TryGet(string id, out ICamera? camera) =>
        _cameras.TryGetValue(id, out camera);

    public bool IsRegistered(string id) => _cameras.ContainsKey(id);

    public ICamera Get(string id) =>
        _cameras.TryGetValue(id, out var camera)
            ? camera
            : throw new VisionException(VisionErrorCode.CameraNotRegistered, $"相机未注册: {id}");

    public CameraFrame Grab(string id, CancellationToken ct = default) => Get(id).Grab(ct);

    private void InvalidateIds()
    {
        lock (_lock)
            _cachedIds = null;
    }

    public void Dispose()
    {
        foreach (var camera in _cameras.Values)
            camera.Dispose();
        _cameras.Clear();
        InvalidateIds();
    }
}
