using Microsoft.Extensions.Logging;
using RobotVision.Core.Models;

namespace RobotVision.Hosting;

/// <summary>
/// 管线调度器：并发槽位 + 排队 + 超时/取消语义。
/// 从 VisionService 拆出（原上帝类职责之一），与编排（VisionService.ProcessCoreInnerAsync）解耦：
/// - 排队阶段可取消：超时返回 1010 排队超时并放弃排队（未进入处理，无僵尸）；
/// - 处理阶段不可取消（ONNX 推理无法中断）：调用方超时返回 1008 处理超时，
///   任务在后台跑完并丢弃结果（TrySetResult 落空），槽位随即释放；
/// - 提交总数受 MaxQueueDepth 限制（排队 + 执行），超过立即返回 1009；
/// - 完成后把最终应答交给 <paramref name="onCompleted"/>（统计/健康指标记录）。
/// </summary>
public sealed class PipelineScheduler(ILogger log)
{
    private readonly ILogger _log = log;
    private int _depth;
    private int _processing;
    private double _lastElapsedMs;

    /// <summary>
    /// 并发执行槽位信号量（懒创建，容量 = 首次请求时的 MaxConcurrent）。
    /// LazyThreadSafetyMode.ExecutionAndPublication 保证首次并发请求只执行一次工厂、
    /// 只创建一个信号量（原子性），后续请求统一复用同一实例——
    /// 避免并发下创建两个信号量、双双获槽突破 MaxConcurrent。
    /// 创建后容量固化，运行中修改 MaxConcurrent 不生效（与既有语义一致）。
    /// 字段初始化器不能引用实例属性，故在属性 getter 中懒创建：
    /// 竞争失败的 Lazy 从未访问 Value，工厂（信号量创建）不会执行，仍只有一个信号量。
    /// </summary>
    private Lazy<SemaphoreSlim>? _pipeline;

    private SemaphoreSlim Pipeline =>
        (_pipeline ??= new Lazy<SemaphoreSlim>(
            () => new SemaphoreSlim(Math.Max(1, MaxConcurrent), Math.Max(1, MaxConcurrent)),
            LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    public int QueueDepth => Volatile.Read(ref _depth);

    /// <summary>是否有推理任务正在执行（供 STATUS 命令区分 ready/busy）。</summary>
    public bool IsProcessing => Volatile.Read(ref _processing) > 0;

    /// <summary>最近一次完成推理的耗时（ms，供 STATUS 命令）。</summary>
    public double LastElapsedMs => Volatile.Read(ref _lastElapsedMs);

    /// <summary>
    /// 提交上限（排队 + 执行的任务总数，含正在执行的任务），超过立即返回 1009。
    /// 必须 ≥ <see cref="MaxConcurrent"/>，否则永远不会发生排队。
    /// </summary>
    public int MaxQueueDepth { get; set; } = 4;

    /// <summary>
    /// 并发执行上限（信号量槽位数）：同时进入取图/推理阶段的任务数。
    /// 取图按相机串行、推理按模型串行；此值决定最多几个工位可并行处理。
    /// 首次请求时固化，运行中修改不生效。
    /// </summary>
    public int MaxConcurrent { get; set; } = 2;

    /// <summary>
    /// 提交一个管线任务：排队 → 获槽 → 执行 <paramref name="core"/> → 完成后回调。
    /// 编排逻辑（取图/推理/后处理）经 core 委托注入，本类只负责并发与超时语义。
    /// </summary>
    public async Task<VisionResult> RunAsync(
        string recipeName,
        Func<string, CancellationToken, Task<VisionResult>> core,
        Action<VisionResult> onCompleted,
        CancellationToken ct)
    {
        var waiter = new TaskCompletionSource<VisionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        // stage：0=排队阶段（可放弃） 1=执行阶段（不可中断）。
        // 读写在 stageLock 下与取消回调互斥：获槽成功后立即置 stage=1，
        // 取消回调要么看到"已进入执行"（1008 处理超时），要么看到"仍在排队"（1010 排队超时），
        // 消除"获槽成功与置 stage 之间取消被误标 1010"的竞态窗口。
        var stageLock = new object();
        var stage = 0;

        // 排队阶段超时/取消 → 1010 排队超时；执行阶段调用方超时 → 1008 处理超时
        // （执行阶段的结果若晚到，TrySetResult 落空即丢弃——僵尸任务不污染应答）
        using var registration = ct.Register(() =>
        {
            var code = VisionErrorCode.QueueTimeout;
            var message = "排队超时";
            lock (stageLock)
            {
                if (stage == 1)
                {
                    code = VisionErrorCode.Timeout;
                    message = "处理超时";
                }
            }
            waiter.TrySetResult(VisionResult.Fail(recipeName, code, message, 0));
        });

        if (Interlocked.Increment(ref _depth) > MaxQueueDepth)
        {
            Interlocked.Decrement(ref _depth);
            var busy = VisionResult.Fail(recipeName, VisionErrorCode.Busy,
                $"排队超限（>{MaxQueueDepth}），请稍后重试", 0);
            onCompleted(busy);
            return busy;
        }

        try
        {
            var acquired = false;
            try
            {
                // 等待并发槽位；超时则放弃排队（未进入处理，无僵尸）
                await Pipeline.WaitAsync(ct).ConfigureAwait(false);
                acquired = true;
            }
            catch (OperationCanceledException)
            {
            }

            if (acquired)
            {
                var run = false;
                lock (stageLock)
                {
                    // 获槽成功后、置 stage 前若已取消：释放槽位且不跑 core。
                    // 取消回调已按 stage==0 置 1010；此处再跑会一边回排队超时一边继续推理。
                    if (ct.IsCancellationRequested)
                        Pipeline.Release();
                    else
                    {
                        stage = 1;
                        run = true;
                    }
                }

                if (run)
                {
                    try
                    {
                        // 取图/光源延时等阶段响应取消（取消后按 1008 处理超时）；
                        // 推理段不可中断（Task.Run 内不响应取消），调用方超时后任务跑完丢弃结果。
                        var result = await core(recipeName, ct).ConfigureAwait(false);
                        waiter.TrySetResult(result);
                    }
                    finally
                    {
                        Pipeline.Release();
                    }
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _depth);
        }

        var final = await waiter.Task.ConfigureAwait(false);

        // 统一口径：RecipeStats 与 Health 都以"最终应答"（waiter.Task 的结果）为准——
        // 客户端超时后推理在后台跑完、应答已被置为超时时，配方统计同样记为该次超时失败，
        // 不再出现"RecipeStats 记成功、Health 记超时"的矛盾；两套指标口径完全一致。
        onCompleted(final);
        return final;
    }

    /// <summary>由编排（VisionService）在执行开始/结束时调用，维护 IsProcessing 语义。</summary>
    public IDisposable BeginExecution()
    {
        Interlocked.Increment(ref _processing);
        return new ProcessingLease(this);
    }

    /// <summary>由编排（VisionService）在完成后调用，维护 LastElapsedMs 语义。</summary>
    public void CompleteExecution(double elapsedMs) =>
        Volatile.Write(ref _lastElapsedMs, elapsedMs);

    private sealed class ProcessingLease(PipelineScheduler owner) : IDisposable
    {
        public void Dispose() => Interlocked.Decrement(ref owner._processing);
    }
}
