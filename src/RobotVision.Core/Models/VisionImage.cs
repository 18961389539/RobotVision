namespace RobotVision.Core.Models;

/// <summary>图像平面点（像素坐标，y 轴向下）。Core 几何不再依赖 OpenCvSharp.Point2d。</summary>
public readonly record struct ImagePoint(double X, double Y);

/// <summary>
/// 领域层图像缓冲区：宽高、通道、行跨距与像素指针。
/// 所有权由 <paramref name="owner"/> 释放（通常是 OpenCV Mat 或钉住的托管数组）。
/// 调用方用完必须 Dispose。不引用任何第三方图像库。
/// </summary>
public sealed class VisionImage : IDisposable
{
    private readonly IDisposable? _owner;
    private int _disposed;

    public int Width { get; }

    public int Height { get; }

    public int Channels { get; }

    public IntPtr Data { get; }

    public int Stride { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0 || Data == IntPtr.Zero;

    public VisionImage(int width, int height, int channels, IntPtr data, int stride, IDisposable? owner)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stride);
        if (data == IntPtr.Zero)
            throw new ArgumentException("像素缓冲区指针为空", nameof(data));

        Width = width;
        Height = height;
        Channels = channels;
        Data = data;
        Stride = stride;
        _owner = owner;
    }

    /// <summary>拷贝像素到独立托管缓冲（不依赖 OpenCV）。</summary>
    public VisionImage Clone()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var length = checked(Stride * Height);
        var buffer = new byte[length];
        System.Runtime.InteropServices.Marshal.Copy(Data, buffer, 0, length);
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(
            buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        return new VisionImage(
            Width, Height, Channels, handle.AddrOfPinnedObject(), Stride, new PinnedBuffer(handle));
    }

    /// <summary>全零图像（推理预热等）。</summary>
    public static VisionImage AllocateZero(int width, int height, int channels)
    {
        var stride = checked(width * channels);
        var buffer = new byte[checked(stride * height)];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(
            buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        return new VisionImage(
            width, height, channels, handle.AddrOfPinnedObject(), stride, new PinnedBuffer(handle));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _owner?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class PinnedBuffer(System.Runtime.InteropServices.GCHandle handle) : IDisposable
    {
        public void Dispose()
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}
