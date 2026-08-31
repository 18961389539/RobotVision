using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using RobotVision.WpfHost.Features.Analysis;
using RobotVision.WpfHost.Features.Calibration;
using RobotVision.WpfHost.Features.CalibrationWizard;
using RobotVision.WpfHost.Features.Cameras;
using RobotVision.WpfHost.Features.Chat;
using RobotVision.WpfHost.Features.Communication;
using RobotVision.WpfHost.Features.Failures;
using RobotVision.WpfHost.Features.Lightings;
using RobotVision.WpfHost.Features.Logs;
using RobotVision.WpfHost.Features.Models;
using RobotVision.WpfHost.Features.Monitor;
using RobotVision.WpfHost.Features.Recipe;
using RobotVision.WpfHost.Features.Settings;
using RobotVision.WpfHost.Shared;
using SystemPage = RobotVision.WpfHost.Features.SystemInfo.SystemPage;
using SystemViewModel = RobotVision.WpfHost.Features.SystemInfo.SystemViewModel;

namespace RobotVision.WpfHost;

public partial class App : Application
{
    private IHost? _host;
    private Mutex? _instanceMutex;
    private bool _showingFatalDialog;
    private int _unhandledCount;

    /// <summary>页面 code-behind 解析 ViewModel 的入口（快照模式同样可用）。</summary>
    public static IServiceProvider Services { get; private set; } =
        new ServiceCollection().BuildServiceProvider();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var bootstrapCfg = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .Build()
            .Get<AppConfig>() ?? new AppConfig();
        AppThemeManager.Apply(bootstrapCfg.UiTheme);

        // 离屏快照模式：渲染主窗口到 PNG 后退出（不启动 TCP，避免与运行实例抢端口）
        if (e.Args.Contains("--snapshot", StringComparer.OrdinalIgnoreCase))
        {
            RenderSnapshot();
            Shutdown(0);
            return;
        }

        if (!TryAcquireSingleInstance())
        {
            Shutdown(0);
            return;
        }

        // 重入保护：MessageBox 自带嵌套消息泵，期间的新异常不允许再弹框（否则递归至栈溢出）
        DispatcherUnhandledException += (_, args) =>
        {
            var count = Interlocked.Increment(ref _unhandledCount);
            var text = $"[第 {count} 次] {args.Exception}";
            Console.Error.WriteLine(text);
            System.Diagnostics.Debug.WriteLine(text);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rv-ui-exception.txt"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + text + Environment.NewLine + new string('-', 80) + Environment.NewLine); } catch { }

            if (!_showingFatalDialog && count <= 3)
            {
                _showingFatalDialog = true;
                try
                {
                    MessageBox.Show($"未处理的 UI 异常，程序已尝试继续运行：\n\n{args.Exception}",
                        "RobotVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally
                {
                    _showingFatalDialog = false;
                }
            }
            args.Handled = true;
        };

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false);

        var cfg = builder.Configuration.Get<AppConfig>() ?? new AppConfig();
        DataRootBinder.Apply(cfg);
        builder.Logging.AddRobotVisionFileLogging(cfg);
        builder.Services.AddRobotVision(cfg);
        builder.Services.AddHostedService<TcpHostedService>();
        builder.Services.AddSingleton<LogSink>();
        builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<LogSink>());
        builder.Services.AddSingleton<IChatLogSource>(sp => sp.GetRequiredService<LogSink>());
        builder.Services.AddSingleton<MainViewModel>();
        RegisterPageViewModels(builder.Services);

        _host = builder.Build();
        Services = _host.Services;

        // 先显示主窗口壳，避免 CameraManager / MainViewModel 组装期间长时间无界面
        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // 服务与 ViewModel 在首帧渲染后继续（仍可能在绑定 DataContext 时短暂卡顿，但窗口已可见）
        Dispatcher.BeginInvoke(() =>
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            var recipeErrors = _host.Services.GetRequiredService<RobotVision.Core.Recipe.RecipeLoader>().LoadAll();
            foreach (var (recipeName, error) in recipeErrors)
                logger.LogWarning("配方 {Recipe} 加载失败: {Error}", recipeName, error);

            _host.Services.GetRequiredService<CommunicationViewModel>();
            _host.Start();
            window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch
        {
            // 退出阶段尽力而为
        }
        _host?.Dispose();
        ReleaseSingleInstance();
        base.OnExit(e);
    }

    /// <summary>进程级互斥：禁止双开（否则 TCP 9999 端口冲突、相机争用）。</summary>
    private bool TryAcquireSingleInstance()
    {
        foreach (var name in new[] { @"Global\RobotVision.WpfHost.SingleInstance", @"Local\RobotVision.WpfHost.SingleInstance" })
        {
            try
            {
                _instanceMutex = new Mutex(initiallyOwned: true, name, out var createdNew);
                if (createdNew)
                    return true;

                _instanceMutex.Dispose();
                _instanceMutex = null;
                break;
            }
            catch (UnauthorizedAccessException)
            {
                // Global 名已被其他会话占用等情况，尝试 Local
            }
        }

        MessageBox.Show(
            "RobotVision 已在运行中，不能重复启动。\n\n请切换到任务栏已有窗口，或先关闭再启动。",
            "RobotVision",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private void ReleaseSingleInstance()
    {
        if (_instanceMutex is null)
            return;
        try { _instanceMutex.ReleaseMutex(); } catch { /* 未持有或已释放 */ }
        _instanceMutex.Dispose();
        _instanceMutex = null;
    }

    private static void RegisterPageViewModels(IServiceCollection services)
    {
        services.AddSingleton<RecipeViewModel>();
        services.AddSingleton<CamerasViewModel>();
        services.AddSingleton<LightingsViewModel>();
        services.AddSingleton<ModelsViewModel>();
        services.AddSingleton<CalibrationViewModel>();
        services.AddSingleton<CalibrationWizardViewModel>();
        services.AddSingleton<FailuresViewModel>();
        services.AddSingleton<AnalysisViewModel>();
        services.AddSingleton<CommunicationViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SystemViewModel>();
    }

    private void RenderSnapshot()
    {
        const int width = 1280;
        const int height = 820;

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false);
        var cfg = builder.Configuration.Get<AppConfig>() ?? new AppConfig();
        cfg.Chat.AutoStart = false;
        DataRootBinder.Apply(cfg);
        builder.Services.AddRobotVision(cfg);
        builder.Services.AddSingleton<LogSink>();
        builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<LogSink>());
        builder.Services.AddSingleton<IChatLogSource>(sp => sp.GetRequiredService<LogSink>());
        builder.Services.AddSingleton<MainViewModel>();
        RegisterPageViewModels(builder.Services);
        _host = builder.Build();
        Services = _host.Services;

        var sink = _host.Services.GetRequiredService<LogSink>();
        var vm = _host.Services.GetRequiredService<MainViewModel>();

        // 示例数据：让快照呈现真实内容（日志行 + 结果行）
        vm.Logs.Add(new LogLine(DateTime.Now.ToString("HH:mm:ss"), "Information", "RobotVision 已启动 | TCP 0.0.0.0:9999"));
        vm.Logs.Add(new LogLine(DateTime.Now.ToString("HH:mm:ss"), "Information", "配方 A01: 检出 8 个目标，耗时 1422ms"));
        vm.Poses.Add(new PoseRow(1, 102.356, 88.412, 45.123, 0.95));
        vm.Poses.Add(new PoseRow(2, -15.004, 210.779, 132.050, 0.87));

        var window = new MainWindow { DataContext = vm };
        window.Arrange(new Rect(0, 0, width, height));
        window.UpdateLayout();

        // 取窗口内容，包一层主题背景再渲染（RTB 直接渲染 Window 会得到空图；
        // 离屏树不生成 DataGrid/ListBox 行容器，表格内容需在真实窗口验证）
        var content = (UIElement)window.Content;
        window.Content = null;
        var host = new Border
        {
            Background = (Brush)FindResource("ApplicationBackgroundBrush"),
            Width = width,
            Height = height,
        };
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));

        Directory.CreateDirectory("artifacts");

        // 导航壳（NavigationView 离屏不触发 Loaded/内部导航，单独渲染壳）
        host.Child = content;
        host.UpdateLayout();
        RenderHost(host, "shell", width, height);

        // 逐页直接实例化渲染，验证各管理页布局
        var pages = new (string Name, Type Page)[]
        {
            ("monitor", typeof(MonitorPage)),
            ("cameras", typeof(CamerasPage)),
            ("lightings", typeof(LightingsPage)),
            ("recipe", typeof(RecipePage)),
            ("models", typeof(ModelsPage)),
            ("calibration", typeof(CalibrationPage)),
            ("wizard", typeof(CalibrationWizardPage)),
            ("failures", typeof(FailuresPage)),
            ("analysis", typeof(AnalysisPage)),
            ("communication", typeof(CommunicationPage)),
            ("chat", typeof(ChatPage)),
            ("logs", typeof(LogsPage)),
            ("settings", typeof(SettingsPage)),
            ("system", typeof(SystemPage)),
        };
        foreach (var (name, page) in pages)
        {
            // WPF Page 只允许 Window/Frame 父级，快照用 Frame 承载
            var instance = (System.Windows.Controls.Page)Activator.CreateInstance(page)!;
            var frame = new System.Windows.Controls.Frame { Content = instance };
            host.Child = frame;
            host.UpdateLayout();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.SystemIdle);
            if (instance.DataContext is AnalysisViewModel analysis)
            {
                _ = analysis.RefreshAsync();
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (DateTime.UtcNow < deadline && analysis.Message is "尚未加载" or "加载中…")
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        () => { }, System.Windows.Threading.DispatcherPriority.Background);
                    Thread.Sleep(20);
                }
            }
            host.UpdateLayout();
            RenderHost(host, name, width, height);
        }

        vm.Dispose();
    }

    private static void RenderHost(Border host, string name, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine("artifacts", $"ui-snapshot-{name}.png"));
        encoder.Save(stream);
    }
}

