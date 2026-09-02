using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 预览帧位图复用器：双缓冲交替写像素，替代每帧 <c>new WriteableBitmap</c>
/// （5MP 帧 ≈ 20 MB/帧；后备缓冲为非托管内存，靠终结器延迟回收，长跑下内存呈锯齿并持续产生 GC 压力）。
/// 双缓冲使相邻两帧的 BitmapSource 引用不同 —— WPF Image.Source 对相同引用赋值不重绘，
/// 单缓冲复用会静默冻结画面，双缓冲是安全的最小实现。
/// 线程约束：WriteableBitmap 后备缓冲绑定创建线程，本类所有方法必须仅在创建它的 UI 线程调用；
/// 后台线程只负责准备像素（BgraImageBuffer 或 Mat），Write 时同步拷贝。
/// </summary>
public sealed class PreviewBitmapSink : IDisposable
{
    private WriteableBitmap? _front;
    private WriteableBitmap? _back;
    private int _width;
    private int _height;
    private bool _useFront;

    /// <summary>取下一帧目标缓冲（尺寸变化时重建双缓冲）。</summary>
    public WriteableBitmap Next(int width, int height)
    {
        if (_front is null || width != _width || height != _height)
        {
            _front = Create(width, height);
            _back = Create(width, height);
            _width = width;
            _height = height;
        }

        _useFront = !_useFront;
        return _useFront ? _front! : _back!; // 上面的重建分支保证两者均已创建
    }

    /// <summary>把 BGRA 像素写入下一帧缓冲并返回（UI 线程调用）。</summary>
    public WriteableBitmap Write(BgraImageBuffer buffer)
    {
        var bitmap = Next(buffer.Width, buffer.Height);
        bitmap.WritePixels(
            new Int32Rect(0, 0, buffer.Width, buffer.Height),
            buffer.Pixels,
            buffer.Stride,
            0);
        return bitmap;
    }

    /// <summary>把 BGRA Mat 像素写入下一帧缓冲并返回（UI 线程调用；Mat 可任意线程创建，此处同步读取）。</summary>
    public WriteableBitmap Write(Mat bgra)
    {
        var bitmap = Next(bgra.Width, bgra.Height);
        bitmap.WritePixels(
            new Int32Rect(0, 0, bgra.Width, bgra.Height),
            bgra.Data,
            (int)(bgra.Rows * bgra.Step()),
            (int)bgra.Step());
        return bitmap;
    }

    private static WriteableBitmap Create(int width, int height) =>
        new(width, height, 96, 96, PixelFormats.Bgra32, null);

    public void Dispose()
    {
        // WriteableBitmap 无 Clear()（那是 WinRT 扩展）；置空引用即可，
        // 原生后备缓冲由 WPF 终结器释放，_front/_back 不再被引用即进入回收队列。
        _front = null;
        _back = null;
    }
}
