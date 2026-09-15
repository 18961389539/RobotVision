using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using RobotVision.Infrastructure.Lighting;
using RobotVision.WpfHost;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// WPF ViewModel 测试共享设施：
/// - RunSta：在常驻 STA 消息泵线程上执行（与 Application 同线程）；
/// - CreateVisionService / CreateTcp：轻量构造 ViewModel 依赖（不启动真实 TCP 监听、无模型加载）。
/// </summary>
public static class TestInfra
{
    /// <summary>WPF Application 单例：主题测试共用同一 App，禁止并行创建。</summary>
    public static readonly object WpfAppLock = new();

    private static Thread? _uiThread;

    public static void EnsureWpfApp()
    {
        lock (WpfAppLock)
        {
            if (Application.Current is App)
                return;

            if (Application.Current is not null)
                return;

            // 告诉 App.OnStartup 这是测试宿主：只需资源字典与主题，跳过单实例互斥 / 主窗口 / TCP。
            // 不设这个变量，本机若正运行着应用，App 会弹模态「已在运行中」框无人点击而永久阻塞
            // （实测：应用在跑时全量 Wpf 测试 13 分钟零输出）。也顺带避免测试去抢 TCP 9999。
            Environment.SetEnvironmentVariable("ROBOTVISION_TEST_HOST", "1");

            var ready = new ManualResetEventSlim(false);
            Exception? startupError = null;
            _uiThread = new Thread(() =>
            {
                try
                {
                    var app = new App();
                    app.InitializeComponent();

                    // 必须改成显式关闭：App.xaml 是 ShutdownMode="OnMainWindowClose"，而多个测试
                    // （如 PanelTitleThemeTests）会 new Window{...}.Show()/.Close() 来取样式。
                    // WPF 会把第一个创建的 Window 当作 MainWindow，它一关就触发 Application 关闭：
                    // Application.Current 随之变 null，之后任何 EnsureWpfApp 都会去再建一个 App，
                    // 而「同一 AppDomain 只能有一个 Application」→ new App() 在后台线程抛异常 →
                    // 未处理异常直接干掉测试进程（现象是随机的「测试运行已中止」，崩溃点每次不同）。
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    ready.Set();
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    // 交给调用线程重新抛出：否则这里的异常只会打死进程，测试报告里什么都看不到
                    startupError = ex;
                    ready.Set();
                }
            })
            {
                IsBackground = true,
                Name = "RobotVision.Wpf.Tests.UI",
            };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            ready.Wait();

            if (startupError is not null)
                ExceptionDispatchInfo.Capture(startupError).Throw();
        }
    }

    public static T RunSta<T>(Func<T> func)
    {
        EnsureWpfApp();
        T? result = default;
        Exception? error = null;
        Application.Current!.Dispatcher.Invoke(() =>
        {
            try { result = func(); }
            catch (Exception ex) { error = ex; }
        });
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
        return result!;
    }

    public static void RunSta(Action action) => RunSta<object?>(() => { action(); return null; });

    /// <summary>在 UI 线程上等待并抽空 Dispatcher 队列（供 DispatcherTimer 等异步 UI 逻辑测试）。</summary>
    public static void PumpDispatcherFor(TimeSpan duration)
    {
        var end = Environment.TickCount64 + (long)duration.TotalMilliseconds;
        var dispatcher = Dispatcher.CurrentDispatcher;
        while (Environment.TickCount64 < end)
        {
            dispatcher.Invoke(DispatcherPriority.Background, static () => { });
            Thread.Sleep(5);
        }
    }

    /// <summary>临时目录（测试后自动清理）。</summary>
    public sealed class TempDir : IDisposable
    {
        public TempDir(string prefix)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateSub(string name)
        {
            var dir = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>构造 VisionService（无真实模型，配方指向不存在模型时返回 1005；不存在配方返回 1001）。</summary>
    public static VisionService CreateVisionService(
        string recipeFolder,
        CameraManager? cameras = null,
        string? failureFolder = null,
        int maxQueueDepth = 4,
        int maxConcurrent = 1)
    {
        var recipes = new RecipeLoader(recipeFolder);
        cameras ??= new CameraManager();
        if (cameras.CameraIds.Count == 0)
            cameras.Register(new VirtualCamera("cam_virtual", 64, 64, "Bars"));

        var calibration = new CalibrationManager();
        var failureImages = new FailureImageStore(
            new FailureImageConfig { Folder = failureFolder ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rv_nowhere") },
            NullLogger<FailureImageStore>.Instance);

        return new VisionService(recipes, cameras, new LightingManager(), calibration,
            new AngleStrategyFactory(new ModelManager(System.IO.Path.GetTempPath())),
            failureImages,
            NullLogger<VisionService>.Instance)
        {
            MaxQueueDepth = maxQueueDepth,
            MaxConcurrent = maxConcurrent,
        };
    }

    /// <summary>构造未启动的 TcpServerManager（handler 不实际执行）。</summary>
    public static TcpServerManager CreateTcp(int port = 0)
    {
        var tcp = new TcpServerManager(
            "127.0.0.1", port, 5000,
            (_, _, _) => Task.FromResult(VisionResult.Fail("?", VisionErrorCode.InternalError, "unused", 0)),
            NullLogger<TcpServerManager>.Instance);
        return tcp;
    }

    public static AppConfig CreateAppConfig(string tempDir) => new()
    {
        IpAddress = "127.0.0.1",
        TcpPort = 0,
        RecipesFolder = System.IO.Path.Combine(tempDir, "recipes"),
        ModelsFolder = System.IO.Path.Combine(tempDir, "models"),
        CalibrationFolder = System.IO.Path.Combine(tempDir, "calibration"),
        FileLogging = new FileLoggingConfig { Folder = System.IO.Path.Combine(tempDir, "logs") },
        FailureImage = new FailureImageConfig { Folder = System.IO.Path.Combine(tempDir, "failures") },
    };

    public static ICameraRuntime CameraFacade(CameraManager cameras) => cameras.AsRuntimeFacade();

    public static ICalibrationRuntime CalibrationFacade(CalibrationManager calibration) =>
        calibration.AsRuntimeFacade();

    public static ILightingRuntime LightingFacade(LightingManager lighting) => lighting.AsRuntimeFacade();

    public static IModelRuntime ModelFacade(ModelManager models) => models.AsRuntimeFacade();

    public static ITcpRuntime TcpFacade(TcpServerManager tcp) => tcp.AsRuntimeFacade();

    public static IAngleStrategyCatalog AngleCatalog(AngleStrategyTypeRegistry registry) =>
        registry.AsRuntimeFacade();

    public static IRecipeTestService CreateRecipeTestService(
        VisionService vision,
        CameraManager cameras,
        ModelManager models,
        CalibrationManager calibration,
        LightingManager lighting) =>
        new RecipeTestService(
            vision,
            CameraFacade(cameras),
            ModelFacade(models),
            CalibrationFacade(calibration),
            LightingFacade(lighting));

    public static IMaskTemplateTeachService MaskTeach() => new MaskTemplateTeachService();

    public static ISegmentRefineGuidance RefineGuidance() => new SegmentRefineGuidance();

    public static ICalibrationWizardService CalibrationWizard(
        ICameraRuntime? cameras = null,
        ICalibrationRuntime? calibration = null) =>
        new CalibrationWizardService(
            cameras ?? new CameraManager().AsRuntimeFacade(),
            calibration ?? new CalibrationManager().AsRuntimeFacade());

    public static IModelTestService ModelTest(ModelManager models) => new ModelTestService(models);

    public static IImageFileReader ImageFiles() => new ImageFileReader();

    public static IFrameOverlayPresenter Overlay() => new FrameOverlayPresenter();

    public static IMonitorPreviewService MonitorPreview(
        RecipeLoader recipes,
        ICameraRuntime? cameras = null,
        ICalibrationRuntime? calibration = null) =>
        new MonitorPreviewService(
            cameras ?? new CameraManager().AsRuntimeFacade(),
            calibration ?? new CalibrationManager().AsRuntimeFacade(),
            recipes);
}
