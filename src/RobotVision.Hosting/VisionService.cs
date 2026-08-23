using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting;

/// <summary>
/// 一次推理完成后推送给订阅者的画面快照（去畸变图 + 像素位姿）。
/// 供 UI 叠加显示检测结果；TCP 链路不产生快照开销（无订阅者时跳过克隆）。
/// </summary>
/// <param name="UndistortedImage">去畸变图像，所有权移交订阅者，用完必须 Dispose。</param>
public sealed record VisionFrameSnapshot(
    string RecipeName,
    Mat UndistortedImage,
    IReadOnlyList<PixelPose> Poses);

/// <summary>按配方聚合的运行统计（成功率/平均耗时），供 UI 看板展示。</summary>
public sealed record RecipeStatsSnapshot(
    string Recipe,
    long Total,
    long Ok,
    long Failed,
    double AvgMs,
    double LastMs,
    DateTime? LastAt)
{
    public double SuccessRate => Total == 0 ? 0 : (double)Ok / Total;
}

/// <summary>单个配方的累计计数（内部可变，快照时投影为不可变记录）。</summary>
internal sealed class RecipeStat
{
    public long Total;
    public long Ok;
    public long Failed;
    public double TotalMs;
    public double LastMs;
    public DateTime? LastAt;
}

/// <summary>
/// 流程编排：加载配方 → 取图 → 内参去畸变 → 角度策略推理 → 外参变换 → 组装结果。
/// 并发模型：并发槽位（最多 MaxQueueDepth 个任务同时执行），不再全局单锁——
/// - 取图按相机粒度串行（相机 I/O 非线程安全），不同相机可并行取图；
/// - 推理由 ModelSession 内信号量按模型串行（Yolo 非线程安全），不同模型可并行；
/// - 外参/组装为纯计算。
/// 超时与队列设计：
/// - 排队阶段可取消：超时返回 1010 排队超时并放弃排队（未进入处理，无僵尸）；
/// - 处理阶段不可取消（ONNX 推理无法中断）：调用方超时返回 1008 处理超时，
///   任务在后台跑完并丢弃结果（TrySetResult 落空），槽位随即释放；
/// - 提交总数受 MaxQueueDepth 限制（排队 + 执行），超过立即返回 1009。
/// 失败留存：取图成功后的任何失败（未检出/模型异常/内部错误）把去畸变图
/// 交给 FailureImageStore 落盘；取图前的失败（配方/相机/标定）无现场可留。
/// </summary>
public sealed class VisionService(
    RecipeLoader recipes,
    CameraManager cameras,
    LightingManager lighting,
    CalibrationManager calibration,
    AngleStrategyFactory strategies,
    FailureImageStore failureImages,
    ILogger<VisionService> log)
{
    /// <summary>全局滚动健康窗口大小（最近 N 次的成功率/P95）。</summary>
    private const int HealthWindow = 128;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cameraGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RecipeStat> _stats = new(StringComparer.OrdinalIgnoreCase);
    private int _depth;
    private int _processing;
    private double _lastElapsedMs;

    private readonly object _healthLock = new();
    private readonly double[] _healthLatency = new double[HealthWindow];
    private readonly byte[] _healthOutcome = new byte[HealthWindow]; // 0 成功 / 1 业务失败 / 2 超时
    private int _healthIndex;

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

    /// <summary>最近窗口健康指标：总次数 / 业务失败数 / 超时数（含 1008/1010）/ 平均耗时 / P95。</summary>
    public (int Total, int Failed, int TimedOut, double AvgMs, double P95Ms) Health
    {
        get
        {
            lock (_healthLock)
            {
                var count = Math.Min(_healthIndex, HealthWindow);
                if (count == 0)
                    return (0, 0, 0, 0, 0);

                var latencies = new double[count];
                var failed = 0;
                var timedOut = 0;
                for (var i = 0; i < count; i++)
                {
                    var idx = (_healthIndex - count + i) % HealthWindow;
                    latencies[i] = _healthLatency[idx];
                    if (_healthOutcome[idx] == 1) failed++;
                    else if (_healthOutcome[idx] == 2) timedOut++;
                }
                Array.Sort(latencies);
                var p95 = latencies[(int)Math.Ceiling(count * 0.95) - 1];
                return (count, failed, timedOut, latencies.Average(), p95);
            }
        }
    }

    /// <summary>按配方聚合的运行统计快照（最近触发优先）。含手动触发与 TCP 触发。</summary>
    public IReadOnlyList<RecipeStatsSnapshot> GetRecipeStats() =>
        _stats.Select(kv =>
            {
                var s = kv.Value;
                // 单条计数在条目锁下读取，避免读到"总数已加、耗时/最近时间未更新"的不一致快照
                lock (s)
                {
                    return new RecipeStatsSnapshot(
                        kv.Key, s.Total, s.Ok, s.Failed,
                        s.Total == 0 ? 0 : s.TotalMs / s.Total, s.LastMs, s.LastAt);
                }
            })
            .OrderByDescending(s => s.LastAt ?? DateTime.MinValue)
            .ToList();

    /// <summary>
    /// 每次成功完成推理（含零检出）后触发；在管线线程内克隆快照（快），
    /// 订阅者回调在独立线程池线程执行（绘制/位图转换等重活不阻塞管线）。
    /// 订阅者须自行保证线程安全；订阅者异常不影响管线。
    /// </summary>
    public event Action<VisionFrameSnapshot>? FrameProcessed;

    public async Task<VisionResult> RunAsync(string recipeName, CancellationToken ct)
    {
        var waiter = new TaskCompletionSource<VisionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        // stage：0=排队阶段（可放弃） 1=执行阶段（不可中断）。
        // 读写在 stageLock 下与取消回调互斥：获槽成功后立即置 stage=1，
        // 取消回调要么看到"已进入执行"（1008 处理超时），要么看到"仍在排队"（1010 排队超时），
        // 消除原实现"获槽成功与置 stage 之间取消被误标 1010"的竞态窗口。
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
            return VisionResult.Fail(recipeName, VisionErrorCode.Busy,
                $"排队超限（>{MaxQueueDepth}），请稍后重试", 0);
        }

        try
        {
            var acquired = false;
            try
            {
                // 等待并发槽位；超时则放弃排队（未进入处理，无僵尸）
                await Pipeline.WaitAsync(ct);
                acquired = true;
            }
            catch (OperationCanceledException)
            {
            }

            if (acquired)
            {
                // 获槽成功立即置执行阶段（与取消回调同锁），竞态窗口内的取消按 1008 处理
                lock (stageLock) { stage = 1; }
                try
                {
                    // 取图/光源延时等阶段响应取消（取消后按 1008 处理超时）；
                    // 推理段不可中断（Task.Run 内不响应取消），调用方超时后任务跑完丢弃结果。
                    var result = await ProcessCoreInnerAsync(recipeName, ct);
                    waiter.TrySetResult(result);
                }
                finally
                {
                    Pipeline.Release();
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _depth);
        }

        var final = await waiter.Task;

        // 统一口径：RecipeStats 与 Health 都以"最终应答"（waiter.Task 的结果）为准——
        // 客户端超时后推理在后台跑完、应答已被置为超时时，配方统计同样记为该次超时失败，
        // 不再出现"RecipeStats 记成功、Health 记超时"的矛盾；两套指标口径完全一致。
        RecordStats(recipeName, final);
        RecordHealth(final);
        return final;
    }

    /// <summary>成功（含零检出）计入 Ok；业务失败（配方/相机/模型/超时等）计入 Failed。进程内存统计，重启归零。</summary>
    private void RecordStats(string recipeName, VisionResult result)
    {
        var s = _stats.GetOrAdd(recipeName, _ => new RecipeStat());
        lock (s)
        {
            s.Total++;
            if (result.Ok)
                s.Ok++;
            else
                s.Failed++;
            s.TotalMs += result.ElapsedMs;
            s.LastMs = result.ElapsedMs;
            s.LastAt = DateTime.Now;
        }
    }

    /// <summary>滚动记录最近一次请求结果（健康指标）。</summary>
    private void RecordHealth(VisionResult result)
    {
        var outcome = result.Ok ? (byte)0
            : result.ErrorCode is VisionErrorCode.Timeout or VisionErrorCode.QueueTimeout ? (byte)2
            : (byte)1;
        lock (_healthLock)
        {
            _healthLatency[_healthIndex % HealthWindow] = result.ElapsedMs;
            _healthOutcome[_healthIndex % HealthWindow] = outcome;
            _healthIndex++;
        }
    }

    private async Task<VisionResult> ProcessCoreInnerAsync(string recipeName, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        Interlocked.Increment(ref _processing);
        Mat? undistorted = null;
        double grabMs = 0, undistortMs = 0, inferenceMs = 0;
        FailureContext? failureCtx = null;
        try
        {
            var recipe = recipes.Get(recipeName);
            failureCtx = BuildFailureContext(recipe);

            // 停用配方（Enabled=false）拒绝触发，文件保留
            if (!recipe.Enabled)
                throw new InvalidRecipeException(recipeName, "配方已停用（Enabled=false）");

            // 取图前：点亮光源并等待稳定（配方未配置照明时零开销）
            using var lightingScope = lighting.Apply(recipe.LightControllerId, recipe.Lighting);
            if (lightingScope.StabilizeDelayMs > 0)
                await Task.Delay(lightingScope.StabilizeDelayMs, ct);

            // 取图 + 去畸变：按相机粒度串行（相机 I/O 非线程安全）；不同相机可并行取图。
            // Grab 支持取消：阻塞中的 SDK 调用返回后立即响应取消（抛 OperationCanceledException）。
            var cameraGate = _cameraGates.GetOrAdd(recipe.CameraId, _ => new SemaphoreSlim(1, 1));
            await cameraGate.WaitAsync(ct);
            try
            {
                using var frame = cameras.Grab(recipe.CameraId, ct);
                grabMs = stopwatch.Elapsed.TotalMilliseconds;
                undistorted = calibration.Undistort(recipe.CameraId, frame.Image);
                undistortMs = stopwatch.Elapsed.TotalMilliseconds;
            }
            finally
            {
                cameraGate.Release();
            }

            // 取图完成立即熄灯（兑现 TurnOffAfterGrab 语义）：推理/后处理全程不再亮灯，
            // 避免原来 `using var` 把熄灯推迟到请求结束、取图后推理阶段一直亮灯；
            // LightingScope.Dispose 幂等，末尾 using 再次 Dispose 是空操作；
            // TurnOffAfterGrab=false 时此处为空操作，灯保持点亮（与配置语义一致）。
            lightingScope.Dispose();

            // 推理：ModelSession 内信号量按模型串行（Yolo 非线程安全），不同模型可并行；
            // 等待模型信号量阶段响应取消（排队超时），ONNX 推理本身不可中断
            var strategy = strategies.Create(recipe);
            var pixelPoses = await Task.Run(() => strategy.Compute(undistorted, recipe, ct), ct);
            inferenceMs = stopwatch.Elapsed.TotalMilliseconds;

            PublishSnapshot(recipeName, undistorted, pixelPoses);

            if (pixelPoses.Count == 0)
            {
                var miss = VisionResult.Fail(recipeName, VisionErrorCode.NoTargetFound,
                    "未检出目标", stopwatch.Elapsed.TotalMilliseconds);
                failureImages.Save(recipeName, undistorted, miss, failureCtx);
                log.LogInformation("配方 {Recipe}: 未检出目标，总耗时 {Elapsed:0}ms（取图 {Grab:0} · 去畸变 {Undistort:0} · 推理 {Inference:0}）",
                    recipeName, stopwatch.Elapsed.TotalMilliseconds, grabMs, undistortMs, inferenceMs);
                return miss;
            }

            var robotPoses = pixelPoses
                .Select(p => calibration.PixelToRobot(recipe.StationId, p, recipe.DebugPassthrough, recipe.CameraId))
                .Select(r => calibration.CompensateRotation(recipe.StationId, recipe.RotationCompensation, r))
                .ToList();

            // 与 Poses 一一对应的置信度透传（UI/留存可用，TCP 应答格式不含）
            var confidences = pixelPoses.Select(p => p.Score).ToList();

            log.LogInformation("配方 {Recipe}: 检出 {Count} 个目标，总耗时 {Elapsed:0}ms（取图 {Grab:0} · 去畸变 {Undistort:0} · 推理 {Inference:0} · 后处理 {Post:0}）",
                recipeName, robotPoses.Count, stopwatch.Elapsed.TotalMilliseconds,
                grabMs, undistortMs, inferenceMs,
                stopwatch.Elapsed.TotalMilliseconds - inferenceMs);

            return VisionResult.Success(recipeName, robotPoses,
                stopwatch.Elapsed.TotalMilliseconds, confidences);
        }
        catch (RecipeNotFoundException)
        {
            return VisionResult.Fail(recipeName, VisionErrorCode.UnknownRecipe,
                $"配方不存在: {recipeName}", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (InvalidRecipeException ex)
        {
            return VisionResult.Fail(recipeName, VisionErrorCode.UnknownRecipe,
                ex.Message, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // 取图/光源延时阶段被取消（调用方超时）：按处理超时返回；
            // 晚到的结果由 RunAsync 的 TrySetResult 落空丢弃，不产生僵尸应答
            return VisionResult.Fail(recipeName, VisionErrorCode.Timeout,
                "处理超时", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (VisionException vex)
        {
            var fail = VisionResult.Fail(recipeName, vex.ErrorCode, vex.Message,
                stopwatch.Elapsed.TotalMilliseconds);
            if (undistorted is not null)
                failureImages.Save(recipeName, undistorted, fail, failureCtx);
            return fail;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "配方 {Recipe} 处理异常", recipeName);
            var fail = VisionResult.Fail(recipeName, VisionErrorCode.InternalError, ex.Message,
                stopwatch.Elapsed.TotalMilliseconds);
            if (undistorted is not null)
                failureImages.Save(recipeName, undistorted, fail, failureCtx);
            return fail;
        }
        finally
        {
            Volatile.Write(ref _lastElapsedMs, stopwatch.Elapsed.TotalMilliseconds);
            Interlocked.Decrement(ref _processing);
            undistorted?.Dispose();
        }
    }

    /// <summary>失败留存诊断上下文（相机/工位/模型/阈值/触发源，写进 JSON 元数据）。</summary>
    private static FailureContext BuildFailureContext(RecipeConfig recipe) => new(
        CameraId: recipe.CameraId,
        StationId: recipe.StationId,
        Models: recipe.Models.Count > 0 ? string.Join("|", recipe.Models) : null,
        AngleMode: recipe.AngleMode.ToString(),
        Confidence: recipe.Confidence,
        Iou: recipe.Iou,
        Source: "pipeline");

    private void PublishSnapshot(string recipeName, Mat undistorted, IReadOnlyList<PixelPose> poses)
    {
        var handler = FrameProcessed;
        if (handler is null)
            return;

        Mat clone;
        try
        {
            // 克隆（快）留在管线线程，保证在 undistorted Dispose 前完成
            clone = undistorted.Clone();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "快照克隆失败");
            return;
        }

        // 订阅者回调（绘制叠加/位图转换等重活）移到线程池，管线尽快释放
        var snapshot = new VisionFrameSnapshot(recipeName, clone, poses);
        _ = Task.Run(() =>
        {
            try
            {
                handler(snapshot);
            }
            catch (Exception ex)
            {
                // 订阅者异常绝不能影响产线管线
                log.LogWarning(ex, "FrameProcessed 订阅者处理快照异常");
            }
        });
    }
}
