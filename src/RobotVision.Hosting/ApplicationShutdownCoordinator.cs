using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting.Chat;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.Hosting;

internal static partial class ApplicationShutdownCoordinatorLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Application shutdown: host StopAsync timed out; skipping host.Dispose")]
    public static partial void StopTimedOut(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Application shutdown: host.StopAsync failed")]
    public static partial void StopFailed(ILogger? logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Application shutdown: host.Dispose timed out; abandoning synchronous dispose")]
    public static partial void DisposeTimedOut(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Application shutdown: host.Dispose failed")]
    public static partial void DisposeFailed(ILogger? logger, Exception ex);
}

/// <summary>应用退出时 <see cref="IHost"/> 关闭结果。</summary>
public enum ShutdownOutcome
{
    Completed,
    StopTimedOut,
    StopFailed,
    DisposeTimedOut,
    DisposeFailed,
}

/// <summary>
/// 有序关闭：相机排水 → TCP/聊天进程 → Host.StopAsync →（仅成功时）Host.Dispose。
/// 超时后不再同步 Dispose，避免 UI 线程被卡死相机驱动拖住。
/// </summary>
public static class ApplicationShutdownCoordinator
{
    public static TimeSpan GateDrainTimeout { get; } = TimeSpan.FromSeconds(3);
    public static TimeSpan StopTimeout { get; } = TimeSpan.FromSeconds(5);
    public static TimeSpan DisposeTimeout { get; } = TimeSpan.FromSeconds(2);
    public static TimeSpan UiWaitBudget { get; } = TimeSpan.FromSeconds(10);

    public static ShutdownOutcome Shutdown(IHost host, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        try
        {
            DrainCameras(host.Services, logger);

            var tcp = host.Services.GetService<TcpServerManager>();
            try { tcp?.Stop(); }
            catch (Exception ex) { ApplicationShutdownCoordinatorLog.StopFailed(logger, ex); }

            var llama = host.Services.GetService<LlamaServerHost>();
            try { llama?.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
            catch (Exception ex) { ApplicationShutdownCoordinatorLog.StopFailed(logger, ex); }

            using var stopCts = new CancellationTokenSource(StopTimeout);
            var stopTask = host.StopAsync(stopCts.Token);
            if (!stopTask.Wait(StopTimeout + TimeSpan.FromMilliseconds(250)))
            {
                ApplicationShutdownCoordinatorLog.StopTimedOut(logger);
                return ShutdownOutcome.StopTimedOut;
            }
        }
        catch (Exception ex)
        {
            ApplicationShutdownCoordinatorLog.StopFailed(logger, ex);
            return ShutdownOutcome.StopFailed;
        }

        try
        {
            var disposeTask = Task.Run(host.Dispose);
            if (!disposeTask.Wait(DisposeTimeout))
            {
                ApplicationShutdownCoordinatorLog.DisposeTimedOut(logger);
                return ShutdownOutcome.DisposeTimedOut;
            }
        }
        catch (Exception ex)
        {
            ApplicationShutdownCoordinatorLog.DisposeFailed(logger, ex);
            return ShutdownOutcome.DisposeFailed;
        }

        return ShutdownOutcome.Completed;
    }

    private static void DrainCameras(IServiceProvider services, ILogger? logger)
    {
        var cameras = services.GetService<CameraManager>();
        cameras?.PrepareForShutdown(GateDrainTimeout, logger);
    }
}
