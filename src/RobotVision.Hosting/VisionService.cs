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

/// <summary>
/// 流程编排：加载配方 → 取图 → 内参去畸变 → 角度策略推理 → 外参变换 → 组装结果。
/// 并发/超时/队列语义由 <see cref="PipelineScheduler"/> 承担，运行指标由 <see cref="VisionMetrics"/>
/// 承担——本类只保留编排（管线步骤）与快照发布，公共 API 外观保持不变。
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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cameraGates = new(StringComparer.OrdinalIgnoreCase);

    private PipelineScheduler? _scheduler;

    /// <summary>调度器（懒创建：与 VisionService 主构造解耦，构造时无需额外参数）。</summary>
    private PipelineScheduler Scheduler => _scheduler ??= new PipelineScheduler(log);

    private readonly VisionMetrics _metrics = new();

    public int QueueDepth => Scheduler.QueueDepth;

    /// <summary>是否有推理任务正在执行（供 STATUS 命令区分 ready/busy）。</summary>
    public bool IsProcessing => Scheduler.IsProcessing;

    /// <summary>最近一次完成推理的耗时（ms，供 STATUS 命令）。</summary>
    public double LastElapsedMs => Scheduler.LastElapsedMs;

    /// <summary>
    /// 提交上限（排队 + 执行的任务总数，含正在执行的任务），超过立即返回 1009。
    /// 必须 ≥ <see cref="MaxConcurrent"/>，否则永远不会发生排队。
    /// </summary>
    public int MaxQueueDepth
    {
        get => Scheduler.MaxQueueDepth;
        set => Scheduler.MaxQueueDepth = value;
    }

    /// <summary>
    /// 并发执行上限（信号量槽位数）：同时进入取图/推理阶段的任务数。
    /// 取图按相机串行、推理按模型串行；此值决定最多几个工位可并行处理。
    /// 首次请求时固化，运行中修改不生效。
    /// </summary>
    public int MaxConcurrent
    {
        get => Scheduler.MaxConcurrent;
        set => Scheduler.MaxConcurrent = value;
    }

    /// <summary>最近窗口健康指标：总次数 / 业务失败数 / 超时数（含 1008/1010）/ 平均耗时 / P95。</summary>
    public (int Total, int Failed, int TimedOut, double AvgMs, double P95Ms) Health => _metrics.Health;

    /// <summary>按配方聚合的运行统计快照（最近触发优先）。含手动触发与 TCP 触发。</summary>
    public IReadOnlyList<RecipeStatsSnapshot> GetRecipeStats() => _metrics.GetRecipeStats();

    /// <summary>
    /// 每次成功完成推理（含零检出）后触发；在管线线程内克隆快照（快），
    /// 订阅者回调在独立线程池线程执行（绘制/位图转换等重活不阻塞管线）。
    /// 订阅者须自行保证线程安全；订阅者异常不影响管线。
    /// </summary>
    public event Action<VisionFrameSnapshot>? FrameProcessed;

    public Task<VisionResult> RunAsync(string recipeName, CancellationToken ct) =>
        RunAsync(recipeName, null, ct);

    /// <summary>
    /// 带拍照位姿的触发（TRIGGER,配方名,X,Y,RZ）：OnArm（相机装末端）工位在取图前
    /// 校验上报位姿与外参档案标定位姿的一致性（容差 appsettings PoseCheck），
    /// 不一致抛 1012——位姿漂移后旧外参映射已失效，静默执行会输出错位坐标。
    /// pose=null（旧格式 PLC/UI 手动触发）跳过校验。
    /// </summary>
    public Task<VisionResult> RunAsync(string recipeName, TcpClientPose? pose, CancellationToken ct) =>
        Scheduler.RunAsync(recipeName,
            (name, token) => ProcessCoreInnerAsync(name, pose, token), _metrics.Record, ct);

    private async Task<VisionResult> ProcessCoreInnerAsync(string recipeName, TcpClientPose? pose, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var processing = Scheduler.BeginExecution();
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

            // OnArm 位姿一致性（取图前拦截，无现场图可留）：不一致抛 1012 PoseMismatch。
            // 多项式工位走各自的位姿校验（Translate 模式仅校验 RZ，平移用于合成）
            if (pose is not null)
            {
                if (calibration.HasPolynomial(recipe.StationId))
                    calibration.VerifyPolynomialClientPose(recipe.StationId!, pose);
                else
                    calibration.VerifyClientPose(recipe.StationId, pose);
            }

            // 取图前：点亮光源并等待稳定（配方未配置照明时零开销）
            using var lightingScope = lighting.Apply(recipe.LightControllerId, recipe.Lighting);
            if (lightingScope.StabilizeDelayMs > 0)
                await Task.Delay(lightingScope.StabilizeDelayMs, ct);

            // 多项式工位（单图模式）：跳过内参去畸变，推理直接用原图（多项式吸收畸变）
            var usePolynomial = calibration.HasPolynomial(recipe.StationId);

            // 取图 + 去畸变：按相机粒度串行（相机 I/O 非线程安全）；不同相机可并行取图。
            // Grab 支持取消：阻塞中的 SDK 调用返回后立即响应取消（抛 OperationCanceledException）。
            var cameraGate = _cameraGates.GetOrAdd(recipe.CameraId, _ => new SemaphoreSlim(1, 1));
            await cameraGate.WaitAsync(ct);
            try
            {
                using var frame = cameras.Grab(recipe.CameraId, ct);
                grabMs = stopwatch.Elapsed.TotalMilliseconds;
                if (usePolynomial)
                {
                    // 分辨率必须与标定档案一致（归一化坐标错位 = 映射整体失效）
                    calibration.VerifyPolynomialResolution(recipe.StationId!, frame.Image.Width, frame.Image.Height);
                    undistorted = frame.Image.Clone(); // 复用变量：快照/失败留存/Dispose 路径不变
                }
                else
                {
                    undistorted = calibration.Undistort(recipe.CameraId, frame.Image);
                }
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
                .Select(p => usePolynomial
                    ? calibration.PixelToRobotPolynomial(recipe.StationId!, p, recipe.CameraId, pose)
                    : calibration.PixelToRobot(recipe.StationId, p, recipe.DebugPassthrough, recipe.CameraId))
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
            return VisionResult.Fail(recipeName, ex.ErrorCode, ex.Message, stopwatch.Elapsed.TotalMilliseconds);
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
            Scheduler.CompleteExecution(stopwatch.Elapsed.TotalMilliseconds);
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

        // 每个订阅者一份独立克隆：多播委托按订阅顺序串行传同一 snapshot，
        // 订阅者各自 using 释放 UndistortedImage——共享一份时先执行的订阅者
        // 释放后，后续订阅者拿到已释放的 Mat（异常被各自 catch 静默吞掉，
        // 表现为"后订阅的页面永远无图"）。
        // 克隆（快）留在管线线程，保证在 undistorted Dispose 前完成。
        foreach (var subscriber in handler.GetInvocationList())
        {
            Mat clone;
            try
            {
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
                    subscriber.DynamicInvoke(snapshot);
                }
                catch (Exception ex)
                {
                    // 订阅者异常绝不能影响产线管线
                    log.LogWarning(ex, "FrameProcessed 订阅者处理快照异常");
                }
            });
        }
    }
}
