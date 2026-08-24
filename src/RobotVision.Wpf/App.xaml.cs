using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting;

namespace RobotVision.WpfHost;

public partial class App : Application
{
    private IHost? _host;
    private bool _showingFatalDialog;
    private int _unhandledCount;

    /// <summary>页面 code-behind 解析 ViewModel 的入口（快照模式同样可用）。</summary>
    public static IServiceProvider Services { get; private set; } =
        new ServiceCollection().BuildServiceProvider();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 注册强调色资源（SystemAccentColorPrimary 等）：ThemesDictionary 只含主题字典，
        // Badge 等控件模板以 StaticResource 引用强调色，未 Apply 时导航到相关页面会抛 XamlParseException
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
            Wpf.Ui.Appearance.ApplicationTheme.Dark);

        // 离屏快照模式：渲染主窗口到 PNG 后退出（不启动 TCP，避免与运行实例抢端口）
        if (e.Args.Contains("--snapshot", StringComparer.OrdinalIgnoreCase))
        {
            RenderSnapshot();
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
        builder.Logging.AddRobotVisionFileLogging(cfg);
        builder.Services.AddRobotVision(cfg);
        builder.Services.AddHostedService<TcpHostedService>();
        builder.Services.AddSingleton<LogSink>();
        builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<LogSink>());
        builder.Services.AddSingleton<MainViewModel>();
        RegisterPageViewModels(builder.Services);

        _host = builder.Build();
        Services = _host.Services;

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        var recipeErrors = _host.Services.GetRequiredService<RobotVision.Core.Recipe.RecipeLoader>().LoadAll();
        foreach (var (recipeName, error) in recipeErrors)
            logger.LogWarning("配方 {Recipe} 加载失败: {Error}", recipeName, error);

        _host.Start();

        var window = new MainWindow
        {
            DataContext = _host.Services.GetRequiredService<MainViewModel>(),
        };
        MainWindow = window;
        window.Show();
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
        base.OnExit(e);
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
        services.AddSingleton<CommunicationViewModel>();
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
        builder.Services.AddRobotVision(cfg);
        builder.Services.AddSingleton<LogSink>();
        builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<LogSink>());
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
            ("monitor", typeof(Pages.MonitorPage)),
            ("cameras", typeof(Pages.CamerasPage)),
            ("lightings", typeof(Pages.LightingsPage)),
            ("recipe", typeof(Pages.RecipePage)),
            ("models", typeof(Pages.ModelsPage)),
            ("calibration", typeof(Pages.CalibrationPage)),
            ("wizard", typeof(Pages.CalibrationWizardPage)),
            ("failures", typeof(Pages.FailuresPage)),
            ("communication", typeof(Pages.CommunicationPage)),
            ("logs", typeof(Pages.LogsPage)),
            ("settings", typeof(Pages.SettingsPage)),
            ("system", typeof(Pages.SystemPage)),
        };
        foreach (var (name, page) in pages)
        {
            // WPF Page 只允许 Window/Frame 父级，快照用 Frame 承载
            var frame = new System.Windows.Controls.Frame
            {
                Content = Activator.CreateInstance(page),
            };
            host.Child = frame;
            host.UpdateLayout();
            // Frame.Content 走异步导航队列，需泵空 Dispatcher 后页面才挂载
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.SystemIdle);
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

