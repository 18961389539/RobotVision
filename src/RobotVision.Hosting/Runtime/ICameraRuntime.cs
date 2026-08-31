using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.Hosting;

/// <summary>相机运行时（WPF/宿主稳定入口，委托 <see cref="CameraManager"/>）。</summary>
public interface ICameraRuntime
{
    IReadOnlyCollection<string> CameraIds { get; }
    bool TryGet(string id, out ICamera? camera);
    bool IsRegistered(string id);
    string? GetGrabErrorHint(string? id);
    CameraFrame Grab(string id, CancellationToken ct = default);
    CameraFrame GrabForTeach(string id, CancellationToken ct = default);
    CameraFrame Grab(ICamera camera, CancellationToken ct = default);
    void Register(ICamera camera);
    bool Unregister(string id);
}

internal sealed class CameraRuntime(CameraManager inner) : ICameraRuntime
{
    public IReadOnlyCollection<string> CameraIds => inner.CameraIds;
    public bool TryGet(string id, out ICamera? camera) => inner.TryGet(id, out camera);
    public bool IsRegistered(string id) => inner.IsRegistered(id);
    public string? GetGrabErrorHint(string? id) => inner.GetGrabErrorHint(id);
    public CameraFrame Grab(string id, CancellationToken ct = default) => inner.Grab(id, ct);
    public CameraFrame GrabForTeach(string id, CancellationToken ct = default) => inner.GrabForTeach(id, ct);
    public CameraFrame Grab(ICamera camera, CancellationToken ct = default) => inner.Grab(camera, ct);
    public void Register(ICamera camera) => inner.Register(camera);
    public bool Unregister(string id) => inner.Unregister(id);
}
