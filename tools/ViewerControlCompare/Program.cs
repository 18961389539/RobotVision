// 真实对比 DisplayImageViewer vs ImageViewer：构造耗时、内存、交互 CPU。
// 用法：dotnet run -c Release --project tools/ViewerControlCompare
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageViewer.Controls;
using FullImageViewer = ImageViewer.Controls.ImageViewer;

namespace ViewerControlCompare;

internal static class Program
{
    private const int Warmup = 3;
    private const int Samples = 15;
    private const int PageCount = 6;
    private const int WheelEvents = 200;

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            RunBenchmarks();
            return 0;
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static void RunBenchmarks()
    {
        Console.WriteLine("ViewerControlCompare — DisplayImageViewer vs ImageViewer");
        Console.WriteLine($"OS: {Environment.OSVersion.VersionString}  Arch: {(Environment.Is64BitProcess ? "x64" : "x86")}");
        Console.WriteLine($"GC: {(GCSettings.IsServerGC ? "Server" : "Workstation")}");
        Console.WriteLine($"Samples: warmup={Warmup}, measure={Samples}, pages={PageCount}, wheel={WheelEvents}");
        Console.WriteLine();

        var image = CreateTestBitmap(1920, 1080);
        var dllPath = Path.Combine(AppContext.BaseDirectory, "ImageViewerControl.dll");
        if (File.Exists(dllPath))
        {
            var dll = new FileInfo(dllPath);
            Console.WriteLine($"ImageViewerControl.dll: {dll.Length / 1024.0:F1} KB (deployed beside exe)");
        }
        Console.WriteLine();

        MeasureCreate<DisplayImageViewer>("DisplayImageViewer", v => v.ImageSource = image);
        MeasureCreate<FullImageViewer>("ImageViewer (default ctor + Host)", v =>
        {
            v.ImageSource = image;
            v.IsToolbarVisible = false;
        });

        Console.WriteLine();
        MeasureMultiInstance("DisplayImageViewer", () => new DisplayImageViewer(), image);
        MeasureMultiInstance("ImageViewer", () => new FullImageViewer(), image);

        Console.WriteLine();
        MeasureInteraction("DisplayImageViewer", () =>
        {
            var v = new DisplayImageViewer { ImageSource = image, Width = 1280, Height = 960 };
            Layout(v);
            return v;
        });
        MeasureInteraction("ImageViewer", () =>
        {
            var v = new FullImageViewer { ImageSource = image, Width = 1280, Height = 960, IsToolbarVisible = false };
            Layout(v);
            return v;
        });

        Console.WriteLine();
        MeasureDispose();

        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    private static void Layout(FrameworkElement e)
    {
        e.Measure(new Size(1280, 960));
        e.Arrange(new Rect(0, 0, 1280, 960));
        e.UpdateLayout();
    }

    private static BitmapSource CreateTestBitmap(int w, int h)
    {
        var stride = w * 4;
        var pixels = new byte[stride * h];
        var rnd = new Random(42);
        rnd.NextBytes(pixels);
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }

    private static void ForceGc()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static (long WorkingSet, long ManagedBytes) Snapshot()
    {
        ForceGc();
        return (Process.GetCurrentProcess().WorkingSet64, GC.GetTotalMemory(forceFullCollection: true));
    }

    private static double Percentile(double[] sorted, double p)
    {
        var i = (sorted.Length - 1) * p;
        var lo = (int)Math.Floor(i);
        var hi = (int)Math.Ceiling(i);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (i - lo);
    }

    private static void MeasureCreate<T>(string name, Action<T> configure) where T : FrameworkElement, new()
    {
        var times = new List<double>(Samples);
        long? firstManagedDelta = null;
        long? steadyManagedDelta = null;
        long? firstWsDelta = null;

        for (var run = 0; run < Warmup + Samples; run++)
        {
            var before = Snapshot();
            var sw = Stopwatch.StartNew();
            var control = new T();
            configure(control);
            Layout(control);
            sw.Stop();

            var after = Snapshot();
            var managedDelta = after.ManagedBytes - before.ManagedBytes;
            var wsDelta = after.WorkingSet - before.WorkingSet;

            if (run >= Warmup)
            {
                times.Add(sw.Elapsed.TotalMilliseconds);
                if (run == Warmup)
                {
                    firstManagedDelta = managedDelta;
                    firstWsDelta = wsDelta;
                }
                if (run == Warmup + Samples - 1)
                    steadyManagedDelta = managedDelta;
            }

            DisposeControl(control);
        }

        times.Sort();
        var arr = times.ToArray();
        Console.WriteLine("=== 单实例创建 + 布局 + 绑 1920×1080 图 ===");
        Console.WriteLine(name);
        Console.WriteLine($"  耗时 ms  median={Percentile(arr, 0.5):F2}  p95={Percentile(arr, 0.95):F2}  min={arr[0]:F2}  max={arr[^1]:F2}");
        Console.WriteLine($"  托管堆增量 (首次): {firstManagedDelta / 1024.0:F1} KB");
        Console.WriteLine($"  托管堆增量 (末次): {steadyManagedDelta / 1024.0:F1} KB");
        Console.WriteLine($"  工作集增量 (首次): {firstWsDelta / 1024.0:F1} KB");
        Console.WriteLine();
    }

    private static void MeasureMultiInstance(string name, Func<FrameworkElement> factory, ImageSource image)
    {
        ForceGc();
        var before = Snapshot();

        var controls = new List<FrameworkElement>(PageCount);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < PageCount; i++)
        {
            var c = factory();
            if (c is DisplayImageViewer d) d.ImageSource = image;
            else if (c is FullImageViewer iv)
            {
                iv.ImageSource = image;
                iv.IsToolbarVisible = false;
            }
            Layout(c);
            controls.Add(c);
        }
        sw.Stop();

        var after = Snapshot();
        Console.WriteLine($"=== 同时存在 {PageCount} 个实例（模拟 6 个产线页）===");
        Console.WriteLine(name);
        Console.WriteLine($"  总创建+布局 ms: {sw.Elapsed.TotalMilliseconds:F1}");
        Console.WriteLine($"  托管堆总增量: {(after.ManagedBytes - before.ManagedBytes) / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  工作集总增量: {(after.WorkingSet - before.WorkingSet) / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  平均每实例托管堆: {(after.ManagedBytes - before.ManagedBytes) / PageCount / 1024.0:F1} KB");
        Console.WriteLine($"  平均每实例工作集: {(after.WorkingSet - before.WorkingSet) / PageCount / 1024.0:F1} KB");

        foreach (var c in controls)
            DisposeControl(c);
        Console.WriteLine();
    }

    private static void MeasureInteraction(string name, Func<FrameworkElement> factory)
    {
        var control = factory();
        var host = new Window
        {
            Width = 1280,
            Height = 960,
            Content = control,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
        };
        host.Show();
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        ForceGc();
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < WheelEvents; i++)
        {
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, i % 2 == 0 ? 120 : -120)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
            };
            control.RaiseEvent(args);
        }
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        sw.Stop();
        var cpuAfter = Process.GetCurrentProcess().TotalProcessorTime;
        var cpuMs = (cpuAfter - cpuBefore).TotalMilliseconds;

        host.Close();
        DisposeControl(control);

        Console.WriteLine($"=== 交互：{WheelEvents} 次滚轮缩放（含一次离屏 Window 渲染）===");
        Console.WriteLine(name);
        Console.WriteLine($"  墙钟 ms: {sw.Elapsed.TotalMilliseconds:F1}");
        Console.WriteLine($"  CPU ms (进程): {cpuMs:F1}");
        Console.WriteLine($"  每次滚轮 CPU ms: {cpuMs / WheelEvents:F3}");
        Console.WriteLine();
    }

    private static void MeasureDispose()
    {
        var times = new double[Samples];
        for (var run = 0; run < Warmup + Samples; run++)
        {
            var v = new FullImageViewer { IsToolbarVisible = false };
            Layout(v);
            var sw = Stopwatch.StartNew();
            v.Dispose();
            sw.Stop();
            if (run >= Warmup)
                times[run - Warmup] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        Console.WriteLine("=== ImageViewer.Dispose()（DisplayImageViewer 无 Dispose）===");
        Console.WriteLine($"  耗时 ms median={Percentile(times, 0.5):F2}  p95={Percentile(times, 0.95):F2}");
    }

    private static void DisposeControl(FrameworkElement control)
    {
        if (control is IDisposable d)
            d.Dispose();
        else if (control is IAsyncDisposable ad)
            ad.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
