using System.Windows.Threading;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 专用 STA 离屏渲染线程：WPF 的 <see cref="System.Windows.Media.Imaging.RenderTargetBitmap"/>、
/// <see cref="System.Windows.Media.DrawingVisual"/> 等继承自 DispatcherObject，在线程池上创建会
/// 隐式绑定/创建 Dispatcher 且无法回收。中文标签等离屏绘制统一经此线程执行。
/// </summary>
internal static class WpfOffscreenRender
{
    private static readonly Dispatcher Dispatcher;

    static WpfOffscreenRender()
    {
        using var ready = new ManualResetEventSlim(false);
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set();
            System.Windows.Threading.Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "RobotVision.Wpf.OffscreenRender",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        Dispatcher = dispatcher!;
    }

    public static T Invoke<T>(Func<T> func)
    {
        if (Dispatcher.CheckAccess())
            return func();
        return Dispatcher.Invoke(func);
    }
}
