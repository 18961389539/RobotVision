using OpenCvSharp;

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
/// 时间戳供上层把帧与触发时刻/机器人位置关联（工业现场节拍分析）。
/// </summary>
public sealed class CameraFrame : IDisposable
{
    public Mat Image { get; }

    /// <summary>采集完成时刻（UTC）。模拟实现以 Grab 返回时刻计。</summary>
    public DateTime CapturedAtUtc { get; }

    public CameraFrame(Mat image, DateTime capturedAtUtc)
    {
        Image = image;
        CapturedAtUtc = capturedAtUtc;
    }

    public void Dispose() => Image.Dispose();
}

/// <summary>
/// 相机抽象：真实相机（品牌 SDK）、文件夹回放相机、虚拟相机实现同一接口，
/// 上层流程无需区分调试与生产。
/// Grab 是同步方法（品牌 SDK 采集多为同步阻塞）：
/// - 通过 CancellationToken 响应取消（进入阻塞前检查；阻塞中的 SDK 调用本身不可中断，
///   返回后立即抛出 OperationCanceledException，由调用方按超时语义处理）；
/// - 返回帧带采集时刻。
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
