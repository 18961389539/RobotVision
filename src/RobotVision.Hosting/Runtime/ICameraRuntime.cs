using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.Hosting;

/// <summary>Virtual 相机棋盘内角点规格（仅 Virtual 类型有效）。</summary>
public sealed record VirtualChessboardSpec(int InnerWidth, int InnerHeight);

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

    /// <summary>File 相机回放文件列表；非 File 或无文件时返回 null。</summary>
    IReadOnlyList<string>? GetPlaybackFiles(string cameraId);

    /// <summary>Virtual 相机棋盘内角点数；非 Virtual 时返回 null。</summary>
    VirtualChessboardSpec? GetVirtualChessboardInnerCorners(string cameraId);
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

    public IReadOnlyList<string>? GetPlaybackFiles(string cameraId)
    {
        if (!inner.TryGet(cameraId, out var camera) || camera is not FileCamera file)
            return null;
        return file.PlaybackFiles.Count > 0 ? file.PlaybackFiles : null;
    }

    public VirtualChessboardSpec? GetVirtualChessboardInnerCorners(string cameraId)
    {
        if (!inner.TryGet(cameraId, out var camera) || camera is not VirtualCamera virtualCam)
            return null;
        var innerCorners = virtualCam.ChessboardInnerCorners;
        return new VirtualChessboardSpec(innerCorners.Width, innerCorners.Height);
    }
}
