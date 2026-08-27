using RobotVision.Core.Models;

namespace RobotVision.Core.Abstractions;

public enum CameraKind
{
    File,
    Real,
    Virtual,
}

/// <summary>
/// 一次采集结果：图像 + 采集时刻（UTC）。
/// 图像所有权移交调用方，用完必须 Dispose（Dispose 释放图像）。
/// </summary>
public sealed class CameraFrame : IDisposable
{
    public VisionImage Image { get; }

    /// <summary>采集完成时刻（UTC）。模拟实现以 Grab 返回时刻计。</summary>
    public DateTime CapturedAtUtc { get; }

    /// <summary>底层采集（曝光+传输）耗时（毫秒）。未分段的实现为 0，由 CameraManager 用整段 Grab 代替。</summary>
    public double AcquireMs { get; }

    /// <summary>像素格式转换（Bayer→BGR 等）耗时（毫秒）。未分段的实现为 0。</summary>
    public double ConvertMs { get; }

    public CameraFrame(VisionImage image, DateTime capturedAtUtc, double acquireMs = 0, double convertMs = 0)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        CapturedAtUtc = capturedAtUtc;
        AcquireMs = acquireMs;
        ConvertMs = convertMs;
    }

    public void Dispose() => Image.Dispose();
}

/// <summary>
/// 相机抽象：真实相机（品牌 SDK）、文件夹回放相机、虚拟相机实现同一接口，
/// 上层流程无需区分调试与生产。
/// </summary>
public interface ICamera : IDisposable
{
    string Id { get; }

    CameraKind Kind { get; }

    /// <summary>
    /// 采集一帧。返回独立内存的 CameraFrame（含图像），调用方负责 Dispose。
    /// 取消令牌触发时抛 OperationCanceledException（阻塞中的底层调用完成后才响应）。
    /// </summary>
    CameraFrame Grab(CancellationToken ct = default);
}
