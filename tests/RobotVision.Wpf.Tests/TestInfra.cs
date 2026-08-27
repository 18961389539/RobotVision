using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// WPF ViewModel 测试共享设施：
/// - RunSta：WPF 成像/剪贴板等 STA 要求操作在独立 STA 线程执行；
/// - CreateVisionService / CreateTcp：轻量构造 ViewModel 依赖（不启动真实 TCP 监听、无模型加载）。
/// </summary>
public static class TestInfra
{
    /// <summary>在独立 STA 线程执行委托（WPF 位图解码等要求 STA）。异常原样传播。</summary>
    public static T RunSta<T>(Func<T> func)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = func(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
        return result!;
    }

    public static void RunSta(Action action) => RunSta<object?>(() => { action(); return null; });

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
}
