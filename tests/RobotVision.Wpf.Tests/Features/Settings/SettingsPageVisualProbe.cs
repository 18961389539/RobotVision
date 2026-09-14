using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Hosting;
using RobotVision.WpfHost.Features.Settings;
using Xunit.Abstractions;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 服务设置页的「交互态」视觉探针：把页面在 默认 / 脏状态 / 校验错误 三种状态下渲染成 PNG，
/// 落到 <c>artifacts/settings-*.png</c> 供人工核对颜色与对比度。
///
/// 为什么需要它：App 的 <c>--snapshot</c> 只能渲染「刚构造出来的」页面（都是无改动、无错误的干净态），
/// 而本页最需要人眼确认的恰恰是错误定位（字段标红、锚点红点+选中高亮、自动滚动）
/// 与脏状态（琥珀徽章、"须重启"留痕）——这些只能由 VM 状态驱动。
///
/// 默认跳过（避免每次跑测试都写文件、多花十余秒）。需要时：
/// <code>RV_VISUAL_PROBE=1 dotnet test tests/RobotVision.Wpf.Tests -c Debug --filter FullyQualifiedName~SettingsPageVisualProbe</code>
/// </summary>
public sealed class SettingsPageVisualProbe(ITestOutputHelper output)
{
    private const int Width = 1280;
    private const int Height = 2400;

    [Fact]
    public void Render_interactive_states()
    {
        if (Environment.GetEnvironmentVariable("RV_VISUAL_PROBE") != "1")
        {
            output.WriteLine("跳过：设置 RV_VISUAL_PROBE=1 后重跑才会渲染 PNG。");
            return;
        }

        var dir = new TestInfra.TempDir("rv_probe");
        var cfg = TestInfra.CreateAppConfig(dir.Path);
        using var tcp = TestInfra.CreateTcp();
        var vision = TestInfra.CreateVisionService(cfg.RecipesFolder);
        var failures = new FailureImageStore(
            new FailureImageConfig { Folder = cfg.FailureImage.Folder, RetainedCount = 200 },
            NullLogger<FailureImageStore>.Instance);
        using var results = new ResultLogStore(
            new ResultLogConfig { Folder = Path.Combine(dir.Path, "results") },
            NullLogger<ResultLogStore>.Instance);
        var captures = new SuccessCaptureStore(
            new CaptureSuccessConfig { Folder = Path.Combine(dir.Path, "captures") },
            NullLogger<SuccessCaptureStore>.Instance);
        var store = new AppSettingsStore(cfg, Path.Combine(dir.Path, "appsettings.json"));
        var outDir = ResolveArtifactsDir();

        SettingsViewModel New() => new(cfg, TestInfra.TcpFacade(tcp), vision, failures, results, captures,
            store, new TestDialogService(), TestLog.Null<SettingsViewModel>());

        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();

            // 默认（等价于 App --snapshot 的干净态）
            RenderPage(New(), outDir, "probe-a-default");

            // 脏状态：普通项 + 需重启项各来几个，看徽章计数、锚点黄点、琥珀"须重启"留痕
            var dirty = New();
            dirty.MaxConcurrent = 8;              // 需重启
            dirty.TcpBacklog = 64;                // 需重启
            dirty.FileLoggingRetainedDays = 45;   // 需重启
            dirty.PoseXyToleranceMm = 0.35;       // 普通项
            RenderPage(dirty, outDir, "probe-b-dirty");
            output.WriteLine($"[脏] {dirty.DirtySummary} | {dirty.PendingRestartSummary}");

            // 校验错误：自动定位到「网络端点」分组，该字段标签应标红、锚点应有红点
            var bad = New();
            bad.TcpPort = 70000;                  // 超出 1~65535
            bad.SaveCommand.Execute(null);
            RenderPage(bad, outDir, "probe-c-error");
            output.WriteLine($"[错] HasError={bad.HasError} Field={bad.ErrorField} " +
                $"Group={bad.ErrorGroup} SelectedAnchor={bad.SelectedAnchor?.Key} Msg={bad.ErrorMessage}");

            // 局部放大：小字号/低彩度文字必须放大才判断得出颜色与对比度
            RenderZoom(bad, outDir, "probe-d-anchorrail-tail", new Int32Rect(0, 330, 235, 160), 3);
            RenderZoom(bad, outDir, "probe-e-endpoint-label", new Int32Rect(230, 2030, 700, 170), 2);
            RenderZoom(bad, outDir, "probe-f-error-bottomleft", new Int32Rect(0, Height - 190, 520, 180), 2);
            RenderZoom(dirty, outDir, "probe-g-dirty-bottomleft", new Int32Rect(0, Height - 230, 700, 220), 2);
        });

        dir.Dispose();
    }

    private static void RenderPage(SettingsViewModel vm, string outDir, string name)
    {
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Build(vm));
        Save(bitmap, outDir, name);
    }

    /// <summary>高 DPI 渲染即等比放大，用来判断小字号文字的颜色与被裁与否。</summary>
    private static void RenderZoom(SettingsViewModel vm, string outDir, string name,
        Int32Rect rect, double scale)
    {
        var bitmap = new RenderTargetBitmap(
            (int)(Width * scale), (int)(Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(Build(vm));

        Save(new CroppedBitmap(bitmap, new Int32Rect(
            (int)(rect.X * scale), (int)(rect.Y * scale),
            (int)(rect.Width * scale), (int)(rect.Height * scale))), outDir, name);
    }

    private static void Save(BitmapSource bitmap, string outDir, string name)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(outDir, $"settings-{name}.png"));
        encoder.Save(stream);
    }

    /// <summary>
    /// 三个必须照做的步骤，缺一个渲染结果就是错的：
    /// 1. 用 <see cref="System.Windows.Controls.Frame"/> 承载 —— 裸 Page 直接 Measure/Arrange 时
    ///    <c>ui:CardExpander</c> 的内容根本不会被实例化（全停在折叠态，渲染出来只有单行标题）；
    /// 2. 外层包一个主题背景 Border —— <c>RenderTargetBitmap</c> 直接渲染 Window/透明根会得到空图；
    /// 3. <see cref="TestInfra.PumpDispatcherFor"/> 让展开动画跑完 —— 只 pump 一次 SystemIdle 时
    ///    内容停在动画中间态，高度不够导致首行文字被裁。
    /// </summary>
    private static System.Windows.Controls.Border Build(SettingsViewModel vm)
    {
        var frame = new System.Windows.Controls.Frame
        {
            Content = new SettingsPage(vm, new TestDialogService()),
            NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden,
        };
        var host = new System.Windows.Controls.Border
        {
            Width = Width,
            Height = Height,
            Background = (Brush)Application.Current!.FindResource("ApplicationBackgroundBrush"),
            Child = frame,
        };
        host.Measure(new Size(Width, Height));
        host.Arrange(new Rect(0, 0, Width, Height));
        host.UpdateLayout();
        TestInfra.PumpDispatcherFor(TimeSpan.FromMilliseconds(600));
        host.UpdateLayout();
        return host;
    }

    private static string ResolveArtifactsDir([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile);
        while (dir is { Length: > 0 })
        {
            if (Directory.Exists(Path.Combine(dir, "artifacts")) && File.Exists(Path.Combine(dir, "RobotVision.sln")))
                return Path.Combine(dir, "artifacts");
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException($"未能向上找到仓库根（起点：{thisFile}）");
    }
}
