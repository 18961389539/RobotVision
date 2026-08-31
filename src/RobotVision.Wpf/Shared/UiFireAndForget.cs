using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 在 UI 线程上启动后台 Task 并记录未观察异常，替代裸 <c>_ = SomeAsync()</c>。
/// </summary>
internal static class UiFireAndForget
{
    public static void Run(Func<Task> work, ILogger log, [CallerMemberName] string? operation = null)
    {
        _ = RunCore(work, log, operation ?? "ui-async");
    }

    public static void Run(Task task, ILogger log, [CallerMemberName] string? operation = null)
    {
        _ = ObserveAsync(task, log, operation ?? "ui-async");
    }

    private static async Task RunCore(Func<Task> work, ILogger log, string operation)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            WpfUiLog.UiAsyncFailed(log, ex, operation);
        }
    }

    private static async Task ObserveAsync(Task task, ILogger log, string operation)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            WpfUiLog.UiAsyncFailed(log, ex, operation);
        }
    }
}
