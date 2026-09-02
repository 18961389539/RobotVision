namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 页面/窗口级异步会话：CTS + 代数版本 + 在途任务登记。
/// Unload 时调用 <see cref="Deactivate"/>（仅取消、不阻塞 UI 线程），回写 UI 前用 <see cref="IsCurrent"/> 校验。
/// </summary>
public sealed class PageAsyncSession : IDisposable
{
    private readonly object _trackLock = new();
    private readonly List<Task> _inFlight = [];
    private CancellationTokenSource? _cts = new();
    private int _generation;
    private int _deactivated;

    public CancellationToken Token => _cts?.Token ?? CancellationToken.None;

    public int CaptureGeneration() => Volatile.Read(ref _generation);

    public bool IsCurrent(int generation) =>
        generation == Volatile.Read(ref _generation);

    public void Track(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        lock (_trackLock)
            _inFlight.Add(task);
        _ = ObserveAsync(task);
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // 取消后的 late fault 由调用方日志；此处不抛到 Unload 路径
        }
        finally
        {
            lock (_trackLock)
                _inFlight.Remove(task);
        }
    }

    /// <summary>使当前会话失效并取消令牌；可重复调用（幂等）。不在 UI 线程等待在途任务。</summary>
    public void Deactivate()
    {
        if (Interlocked.Exchange(ref _deactivated, 1) == 1)
            return;

        Interlocked.Increment(ref _generation);
        Cancel();
    }

    private void Cancel()
    {
        var cts = _cts;
        if (cts is null)
            return;

        _cts = null;
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts.Dispose();
    }

    public void Dispose()
    {
        Deactivate();
        GC.SuppressFinalize(this);
    }
}
