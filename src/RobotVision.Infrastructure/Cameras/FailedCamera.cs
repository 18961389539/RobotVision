using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 初始化失败的占位相机：保持 Id 已注册（配方引用校验通过），取图时抛 1011。
/// 避免"构造失败 → 未注册 → TRIGGER 返回 1002/1001"，与协议文档的 1011 语义对齐。
/// </summary>
public sealed class FailedCamera(string id, CameraKind kind, string message) : ICamera
{
    private readonly string _message = string.IsNullOrWhiteSpace(message)
        ? $"相机 {id} 初始化失败"
        : message;

    public string Id { get; } = id;

    public CameraKind Kind { get; } = kind;

    /// <summary>初始化失败原因（取图前可在 UI 展示）。</summary>
    public string FaultMessage => _message;

    public CameraFrame Grab(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        throw new VisionException(VisionErrorCode.CameraInitFailed, _message);
    }

    public void Dispose()
    {
    }
}
