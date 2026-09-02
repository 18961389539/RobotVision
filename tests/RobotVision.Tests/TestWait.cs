namespace RobotVision.Tests;

using Xunit;

/// <summary>条件轮询等待，替代固定 Thread.Sleep 以降低 CI 偶发失败。</summary>
internal static class TestWait
{
    public static void Until(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null,
        string? description = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(25);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            Thread.Sleep(pollInterval.Value);
        }

        Assert.Fail(description ?? "Condition was not met within the timeout.");
    }

    public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null,
        string? description = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(25);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(pollInterval.Value);
        }

        Assert.Fail(description ?? "Condition was not met within the timeout.");
    }

    /// <summary>在 duration 内持续断言 condition 为真（用于验证异步写入未发生）。</summary>
    public static void WhileTrue(Func<bool> condition, TimeSpan duration, TimeSpan? pollInterval = null,
        string? description = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(25);
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            if (!condition())
                Assert.Fail(description ?? "Condition became false before the duration elapsed.");
            Thread.Sleep(pollInterval.Value);
        }
    }
}
