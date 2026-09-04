namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 取图等待切片：把最长几十秒的 SDK 阻塞拆成短轮询，好在超时预算内响应 <see cref="CancellationToken"/>。
/// </summary>
internal static class CameraGrabWait
{
    /// <summary>单次 RetrieveResult / 轮询上限。短到取消能在一帧曝光内生效，长到不把 SDK 打成忙等。</summary>
    internal const int PollMs = 50;

    /// <summary>距 deadline 还剩多少毫秒该作为下一次阻塞超时；已到期返回 0。</summary>
    internal static int NextSliceMs(long deadlineTick, int pollMs, long nowTick)
    {
        var remaining = deadlineTick - nowTick;
        if (remaining <= 0)
            return 0;
        return remaining > pollMs ? pollMs : (int)remaining;
    }

    /// <summary>可取消的短睡（连接期 FORCEIP 后再枚举）。令牌触发则抛 <see cref="OperationCanceledException"/>。</summary>
    internal static void WaitUnlessCanceled(int milliseconds, CancellationToken ct)
    {
        if (milliseconds <= 0)
            return;
        if (ct.WaitHandle.WaitOne(milliseconds))
            ct.ThrowIfCancellationRequested();
    }
}
