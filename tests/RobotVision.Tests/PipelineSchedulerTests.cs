using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 管线调度器（PipelineScheduler）并发/超时语义测试：
/// - 正常执行：结果透传 + onCompleted 必被调用；
/// - 提交超限（排队+执行 > MaxQueueDepth）立即返回 1009 Busy；
/// - 排队阶段取消 → 1010 排队超时（未进入处理，无僵尸）；
/// - 执行阶段取消 → 1008 处理超时（推理不可中断，晚到结果被丢弃）；
/// - MaxConcurrent=1 时并发提交串行执行（最多 1 个同时在跑）；
/// - BeginExecution/CompleteExecution 维护 IsProcessing 与 LastElapsedMs。
/// </summary>
[Collection("Serial")]
public class PipelineSchedulerTests
{
    private static PipelineScheduler Create(int maxConcurrent = 2, int maxQueueDepth = 4) =>
        new(NullLogger.Instance)
        {
            MaxConcurrent = maxConcurrent,
            MaxQueueDepth = maxQueueDepth,
        };

    [Fact]
    public async Task RunAsync_Success_ReturnsResult_AndCallsOnCompleted()
    {
        var scheduler = Create();
        var completed = new List<VisionResult>();

        var result = await scheduler.RunAsync(
            "r1", (name, _) => Task.FromResult(VisionResult.Success(name, [], 5.0)),
            completed.Add, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("r1", result.RecipeName);
        Assert.Equal(5.0, result.ElapsedMs);
        var callback = Assert.Single(completed);
        Assert.True(callback.Ok);
    }

    [Fact]
    public async Task RunAsync_OverMaxQueueDepth_ReturnsBusy_AndCompletesCallback()
    {
        var scheduler = Create(maxConcurrent: 1, maxQueueDepth: 2);
        var enter = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        // 任务 a 占用唯一槽位
        var first = scheduler.RunAsync("a", async (name, _) =>
        {
            enter.SetResult();
            await release.Task;
            return VisionResult.Success(name, [], 1);
        }, _ => { }, CancellationToken.None);
        await enter.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // 任务 b 进入排队（depth=2，未超限）
        var second = scheduler.RunAsync(
            "b", (name, _) => Task.FromResult(VisionResult.Success(name, [], 1)),
            _ => { }, CancellationToken.None);

        // 任务 c 提交时 depth=3 > 2 → 立即 1009
        var completed = new List<VisionResult>();
        var third = await scheduler.RunAsync(
            "c", (name, _) => Task.FromResult(VisionResult.Success(name, [], 1)),
            completed.Add, CancellationToken.None);

        Assert.False(third.Ok);
        Assert.Equal(VisionErrorCode.Busy, third.ErrorCode);
        var callback = Assert.Single(completed); // Busy 结果也走 onCompleted（统计口径一致）
        Assert.Equal(VisionErrorCode.Busy, callback.ErrorCode);

        release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(2));
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(secondResult.Ok); // 排队中的任务 b 正常执行完毕
    }

    [Fact]
    public async Task RunAsync_CancelledWhileQueued_ReturnsQueueTimeout()
    {
        var scheduler = Create(maxConcurrent: 1, maxQueueDepth: 4);
        var enter = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var first = scheduler.RunAsync("a", async (name, _) =>
        {
            enter.SetResult();
            await release.Task;
            return VisionResult.Success(name, [], 1);
        }, _ => { }, CancellationToken.None);
        await enter.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // 第二个任务排队期间取消 → 1010（未进入处理，无僵尸）
        using var cts = new CancellationTokenSource(100);
        var second = scheduler.RunAsync(
            "b", (name, _) => Task.FromResult(VisionResult.Success(name, [], 1)),
            _ => { }, cts.Token);

        var result = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Ok);
        Assert.Equal(VisionErrorCode.QueueTimeout, result.ErrorCode);

        release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RunAsync_CancelledDuringExecution_ReturnsTimeout()
    {
        var scheduler = Create(maxConcurrent: 2, maxQueueDepth: 4);
        var started = new TaskCompletionSource();
        using var cts = new CancellationTokenSource();

        // 已获槽执行（stage=1）后取消 → 1008 处理超时；晚到的成功结果被丢弃
        var run = scheduler.RunAsync("a", async (name, _) =>
        {
            started.SetResult();
            await Task.Delay(300); // 模拟不可中断的 ONNX 推理
            return VisionResult.Success(name, [], 1);
        }, _ => { }, cts.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Ok);
        Assert.Equal(VisionErrorCode.Timeout, result.ErrorCode);
    }

    [Fact]
    public async Task RunAsync_MaxConcurrent1_SerializesExecution()
    {
        var scheduler = Create(maxConcurrent: 1, maxQueueDepth: 8);
        var active = 0;
        var maxActive = 0;
        var gate = new object();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i =>
            scheduler.RunAsync($"r{i}", async (name, _) =>
            {
                int now;
                lock (gate)
                {
                    now = ++active;
                    if (now > maxActive)
                        maxActive = now;
                }
                await Task.Delay(30);
                lock (gate)
                    active--;
                return VisionResult.Success(name, [], 1);
            }, _ => { }, CancellationToken.None)));

        Assert.All(results, r => Assert.True(r.Ok));
        Assert.Equal(1, maxActive); // 单槽位：任何时刻最多 1 个在执行
    }

    [Fact]
    public void BeginExecution_IsProcessing_AndCompleteExecution_SetsLastElapsedMs()
    {
        var scheduler = Create();
        Assert.False(scheduler.IsProcessing);
        Assert.Equal(0, scheduler.LastElapsedMs);

        using (scheduler.BeginExecution())
            Assert.True(scheduler.IsProcessing);
        Assert.False(scheduler.IsProcessing);

        scheduler.CompleteExecution(123.4);
        Assert.Equal(123.4, scheduler.LastElapsedMs);
    }

    [Fact]
    public async Task RunAsync_OnCompleted_CalledEvenOnTimeout()
    {
        var scheduler = Create(maxConcurrent: 1, maxQueueDepth: 4);
        var enter = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var first = scheduler.RunAsync("a", async (name, _) =>
        {
            enter.SetResult();
            await release.Task;
            return VisionResult.Success(name, [], 1);
        }, _ => { }, CancellationToken.None);
        await enter.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // 排队取消：onCompleted 同样收到 1010 应答（指标口径统一）
        var completed = new List<VisionResult>();
        using var cts = new CancellationTokenSource(100);
        var second = scheduler.RunAsync(
            "b", (name, _) => Task.FromResult(VisionResult.Success(name, [], 1)),
            completed.Add, cts.Token);
        await second.WaitAsync(TimeSpan.FromSeconds(5));

        var callback = Assert.Single(completed);
        Assert.Equal(VisionErrorCode.QueueTimeout, callback.ErrorCode);

        release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
