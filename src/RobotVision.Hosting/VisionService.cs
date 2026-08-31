using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting;

/// <summary>
/// 产线推理完成后推送给订阅者的画面快照（去畸变图 + 像素位姿）。
/// 配方试触发不走此通道，见 <see cref="PreviewRunResult"/>。
/// </summary>
/// <param name="UndistortedImage">去畸变图像，所有权移交订阅者，用完必须 Dispose。</param>
public sealed record VisionFrameSnapshot(
    string RecipeName,
    VisionImage UndistortedImage,
    IReadOnlyList<PixelPose> Poses,
    RecipeDisplayHints DisplayHints);

/// <summary>配方试触发专用画面（去畸变图 + 像素位姿 + 叠加策略）。调用方负责 <see cref="IDisposable.Dispose"/>。</summary>
public sealed record PreviewRunOutcome(
    VisionImage UndistortedImage,
    IReadOnlyList<PixelPose> PixelPoses,
    RecipeDisplayHints DisplayHints) : IDisposable
{
    public void Dispose() => UndistortedImage.Dispose();
}

/// <summary>配方页试触发完整应答：PLC 口径的 <see cref="VisionResult"/> + 可选调试画面。</summary>
public sealed record PreviewRunResult(VisionResult Result, PreviewRunOutcome? Frame);

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
    ILogger<VisionService> log,
    AssetIntegrityChecker? assets = null,
    ProcessHealthStore? health = null,
    ResultLogStore? resultLog = null,
    SuccessCaptureStore? captures = null)
{
    private PipelineScheduler? _scheduler;

    /// <summary>调度器（懒创建：与 VisionService 主构造解耦，构造时无需额外参数）。</summary>
    private PipelineScheduler Scheduler => _scheduler ??= new PipelineScheduler(log);

    private readonly VisionMetrics _metrics = new();
    private int _healthRestored;

    public ProcessHealthStore? ProcessHealth => health;

    public int ConsecutiveFailLimit => health?.ConsecutiveFailLimit ?? 0;

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
    public (int Total, int Failed, int TimedOut, double AvgMs, double P95Ms) Health
    {
        get
        {
            EnsureHealthRestored();
            return _metrics.Health;
        }
    }

    /// <summary>按配方聚合的运行统计快照（最近触发优先）。含手动触发与 TCP 触发。</summary>
    public IReadOnlyList<RecipeStatsSnapshot> GetRecipeStats()
    {
        EnsureHealthRestored();
        return _metrics.GetRecipeStats();
    }

    /// <summary>所有配方中最大连续过程失败次数（STATUS 附加字段）。</summary>
    public int MaxConsecutiveFails
    {
        get
        {
            EnsureHealthRestored();
            return _metrics.MaxConsecutiveFails;
        }
    }

    /// <summary>是否有配方因连续失败被联锁。</summary>
    public bool AnyInhibited
    {
        get
        {
            EnsureHealthRestored();
            return health?.AnyInhibited(_metrics) == true;
        }
    }

    /// <summary>解除连续失败联锁（全部或指定配方）并落盘。</summary>
    public void ClearInhibit(string? recipe = null)
    {
        EnsureHealthRestored();
        _metrics.ResetConsecutive(recipe);
        try { health?.PersistState(_metrics); }
        catch (Exception ex) { log.LogWarning(ex, "解除联锁后落盘失败"); }
    }

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
    /// pose=null 时：OnArm 已记录示教位姿则 1014；否则跳过校验（Fixed / 旧档案）。
    /// </summary>
    public Task<VisionResult> RunAsync(string recipeName, TcpClientPose? pose, CancellationToken ct)
    {
        EnsureHealthRestored();
        // 联锁在入队前短路：被锁配方连打不得占 MaxQueueDepth，避免把其他工位打成 1009。
        if (health?.IsInhibited(_metrics, recipeName) == true)
            return Task.FromResult(VisionResult.Fail(recipeName, VisionErrorCode.ProcessUnhealthy,
                "PROCESS_UNHEALTHY", 0));

        return Scheduler.RunAsync(recipeName,
            async (name, token) => (await ProcessCoreInnerAsync(name, pose, token)).Result,
            result =>
            {
                // 结果日志：每次触发的原始留档（含联锁拒绝，分析时按 Code 过滤）。
                // 成功/失败同一格式，供追溯与统计（data/results/ 按天 JSON Lines）。
                resultLog?.Record(result, LookupContext(result.RecipeName));

                // 已在队列内才撞上联锁的请求：不记过程失败、不追加 TSV，以免 PLC 重试刷良率。
                if (result.ErrorCode == VisionErrorCode.ProcessUnhealthy)
                    return;
                _metrics.Record(result);
                health?.OnCompleted(result, _metrics);
            }, ct);
    }

    /// <summary>
    /// 配方页试触发：用内存中的编辑器副本跑完整链路，不读磁盘、不写产量/1018、不留成功图。
    /// PLC TRIGGER 仍走 <see cref="RunAsync(string, TcpClientPose?, CancellationToken)"/>。
    /// </summary>
    public async Task<PreviewRunResult> RunPreviewAsync(
        RecipeConfig recipe, TcpClientPose? pose, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var clone = recipe.Clone();
        var name = RecipeLoader.IsValidRecipeName(clone.Name) ? clone.Name : "preview";
        clone.Name = name;
        EnsureHealthRestored();
        PreviewRunResult? captured = null;
        await Scheduler.RunAsync(name,
            async (_, token) =>
            {
                var core = await ProcessCoreInnerAsync(name, pose, token, overlay: clone, preview: true);
                captured = new PreviewRunResult(core.Result, core.PreviewFrame);
                return core.Result;
            },
            _ => { },
            ct).ConfigureAwait(false);
        return captured ?? new PreviewRunResult(
            VisionResult.Fail(name, VisionErrorCode.InternalError, "试触发未返回画面", 0), null);
    }

    /// <summary>从配方解析相机/工位（供结果日志关联上下文；未知配方返回 null，不影响日志）。</summary>
    private (string CameraId, string StationId)? LookupContext(string recipeName)
    {
        try
        {
            var recipe = recipes.Get(recipeName);
            return (recipe.CameraId, recipe.StationId ?? "");
        }
        catch
        {
            return null;
        }
    }

    private sealed record ProcessCoreOutcome(VisionResult Result, PreviewRunOutcome? PreviewFrame);

    private static ProcessCoreOutcome Core(VisionResult result, PreviewRunOutcome? preview = null) =>
        new(result, preview);

    private static PreviewRunOutcome? TryHandoffPreview(
        bool preview, RecipeConfig recipe, ref VisionImage? undistorted, IReadOnlyList<PixelPose> poses)
    {
        if (!preview || undistorted is null)
            return null;
        var image = undistorted;
        undistorted = null;
        return new PreviewRunOutcome(image, poses, RecipeDisplayHints.ForRecipeTest(recipe));
    }

    private async Task<ProcessCoreOutcome> ProcessCoreInnerAsync(
        string recipeName, TcpClientPose? pose, CancellationToken ct,
        RecipeConfig? overlay = null, bool preview = false)
    {
        var stopwatch = Stopwatch.StartNew();
        using var processing = Scheduler.BeginExecution();
        VisionImage? undistorted = null;
        double grabMs = 0, undistortMs = 0, inferenceMs = 0;
        double recipeMs = 0, lightOnMs = 0, stabilizeMs = 0;
        double gateWaitMs = 0, acquireMs = 0, convertMs = 0;
        double segmentMs = 0, refineMs = 0;
        FailureContext? failureCtx = null;
        try
        {
            RecipeConfig recipe;
            if (overlay is not null)
            {
                RecipeLoader.Validate(overlay);
                recipes.ValidateReferences(overlay);
                recipe = overlay;
            }
            else
                recipe = recipes.Get(recipeName);
            failureCtx = BuildFailureContext(recipe, preview);

            // 停用配方（Enabled=false）拒绝触发，文件保留；试触发仍可跑编辑器以便调试停用配方
            if (!preview && !recipe.Enabled)
                throw new InvalidRecipeException(recipeName, "配方已停用（Enabled=false）",
                    VisionErrorCode.RecipeDisabled);

            EnsureHealthRestored();
            if (!preview && health?.IsInhibited(_metrics, recipeName) == true)
                return Core(VisionResult.Fail(recipeName, VisionErrorCode.ProcessUnhealthy,
                    "PROCESS_UNHEALTHY", stopwatch.Elapsed.TotalMilliseconds));

            var assetError = assets?.Check(recipe);
            if (assetError is not null)
                return Core(VisionResult.Fail(recipeName, VisionErrorCode.AssetMismatch,
                    assetError, stopwatch.Elapsed.TotalMilliseconds));

            // OnArm 位姿：缺位姿直接 1014；有位姿再做 1012 一致性（多项式 Translate 仅校 RZ）。
            calibration.RequireClientPose(recipe.StationId, pose);
            if (pose is not null)
            {
                if (calibration.HasPolynomial(recipe.StationId))
                    calibration.VerifyPolynomialClientPose(recipe.StationId!, pose);
                else
                    calibration.VerifyClientPose(recipe.StationId, pose);
            }

            recipeMs = stopwatch.Elapsed.TotalMilliseconds;

            // 取图前：点亮光源并等待稳定（配方未配置照明时零开销）
            using var lightingScope = lighting.Apply(recipe.LightControllerId, recipe.Lighting);
            lightOnMs = stopwatch.Elapsed.TotalMilliseconds - recipeMs;
            if (lightingScope.StabilizeDelayMs > 0)
                await Task.Delay(lightingScope.StabilizeDelayMs, ct).ConfigureAwait(false);
            stabilizeMs = stopwatch.Elapsed.TotalMilliseconds - recipeMs - lightOnMs;

            // 映射模式：多项式 > 外参 > 比例（无标定板工位的回退：图像平面毫米输出）。
            // 多项式/比例均为单图模式：跳过内参去畸变，推理直接用原图
            // （多项式吸收畸变；比例以原图为测量基准，去畸变反而使像素错位）。
            var mappingMode = calibration.GetMappingMode(recipe.StationId);

            // 取图走 CameraManager 按 Id 串行门闩（与 UI 预览共用，SDK 非线程安全）。
            // 去畸变在锁外：预览可与 CPU 去畸变重叠，不必再占相机。
            var grabTrace = await cameras.GrabTracedAsync(recipe.CameraId, ct).ConfigureAwait(false);
            using var frame = grabTrace.Frame;
            grabMs = stopwatch.Elapsed.TotalMilliseconds;
            gateWaitMs = grabTrace.GateWaitMs;
            if (frame.AcquireMs >= 0.5 || frame.ConvertMs >= 0.5)
            {
                acquireMs = frame.AcquireMs;
                convertMs = frame.ConvertMs;
            }
            else
            {
                acquireMs = grabTrace.GrabMs;
            }
            if (mappingMode == StationMappingMode.Polynomial)
            {
                // 分辨率必须与标定档案一致（归一化坐标错位 = 映射整体失效）
                calibration.VerifyPolynomialResolution(recipe.StationId!, frame.Image.Width, frame.Image.Height);
                undistorted = frame.Image.Clone();
            }
            else if (mappingMode == StationMappingMode.Scale)
            {
                // 比例档案记录了分辨率时校验一致（换分辨率 mm/px 失效）
                calibration.VerifyScaleResolution(recipe.StationId!, frame.Image.Width, frame.Image.Height);
                undistorted = frame.Image.Clone();
            }
            else
            {
                undistorted = calibration.Undistort(recipe.CameraId, frame.Image);
            }
            undistortMs = stopwatch.Elapsed.TotalMilliseconds;

            // 取图完成立即熄灯（兑现 TurnOffAfterGrab 语义）：推理/后处理全程不再亮灯，
            // 避免原来 `using var` 把熄灯推迟到请求结束、取图后推理阶段一直亮灯；
            // LightingScope.Dispose 幂等，末尾 using 再次 Dispose 是空操作；
            // TurnOffAfterGrab=false 时此处为空操作，灯保持点亮（与配置语义一致）。
            lightingScope.Dispose();

            // 推理：ModelSession 内信号量按模型串行（Yolo 非线程安全），不同模型可并行；
            // 等待模型信号量阶段响应取消（排队超时），ONNX 推理本身不可中断
            var strategy = strategies.Create(recipe);
            var infer = await Task.Run(() =>
            {
                InferenceStageClock.Reset();
                var poses = strategy.Compute(undistorted, recipe, ct);
                var (seg, refine) = InferenceStageClock.Snapshot();
                return (Poses: poses, SegmentMs: seg, RefineMs: refine);
            }, ct).ConfigureAwait(false);
            var pixelPoses = infer.Poses;
            segmentMs = infer.SegmentMs;
            refineMs = infer.RefineMs;
            inferenceMs = stopwatch.Elapsed.TotalMilliseconds;

            if (!preview)
                PublishSnapshot(recipe, undistorted, pixelPoses);

            string Stages(double total) => FormatStageMs(new PipelineStageMs
            {
                Mode = mappingMode,
                GrabMs = grabMs,
                PrepMs = Math.Max(0, undistortMs - grabMs),
                InferMs = Math.Max(0, inferenceMs - undistortMs),
                PostMs = Math.Max(0, total - inferenceMs),
                RecipeMs = recipeMs,
                LightOnMs = lightOnMs,
                StabilizeMs = stabilizeMs,
                GateWaitMs = gateWaitMs,
                AcquireMs = acquireMs,
                ConvertMs = convertMs,
                SegmentMs = segmentMs,
                RefineMs = refineMs,
            });

            var reject = PixelPoseOutput.RejectReason(pixelPoses);
            if (reject is { } missCode)
            {
                var missElapsed = stopwatch.Elapsed.TotalMilliseconds;
                var missMessage = missCode == VisionErrorCode.RefineFailed
                    ? "分割已检出但精修未通过（头尾不可判或匹配失败）"
                    : "未检出目标";
                var miss = VisionResult.Fail(recipeName, missCode, missMessage, missElapsed);
                if (!preview)
                    failureImages.Save(recipeName, undistorted, miss, failureCtx);
                log.LogInformation("配方 {Recipe}: {Message}，总耗时 {Elapsed:0}ms（{Stages}）",
                    recipeName, missMessage, missElapsed, Stages(missElapsed));
                return Core(miss, TryHandoffPreview(preview, recipe, ref undistorted, pixelPoses));
            }

            var usablePoses = PixelPoseOutput.UsableOnly(pixelPoses);

            var robotPoses = usablePoses
                .Select(p => mappingMode switch
                {
                    StationMappingMode.Polynomial =>
                        calibration.PixelToRobotPolynomial(recipe.StationId!, p, recipe.CameraId, pose),
                    StationMappingMode.Scale =>
                        calibration.PixelToRobotScale(recipe.StationId!, p, recipe.CameraId),
                    _ => calibration.PixelToRobot(recipe.StationId, p, recipe.CameraId, pose),
                })
                .Select(r => calibration.CompensateRotation(recipe.StationId, recipe.RotationCompensation, r))
                .Select(r => recipe.OutputOffset.Apply(r))
                .ToList();

            // 与 Poses 一一对应的置信度透传（UI/留存可用，TCP 应答格式不含）
            var confidences = usablePoses.Select(p => p.Score).ToList();

            var elapsed = stopwatch.Elapsed.TotalMilliseconds;
            log.LogInformation("配方 {Recipe}: 检出 {Count} 个目标，总耗时 {Elapsed:0}ms（{Stages}）",
                recipeName, robotPoses.Count, elapsed,
                Stages(elapsed));

            var success = VisionResult.Success(recipeName, robotPoses,
                stopwatch.Elapsed.TotalMilliseconds, confidences);

            // 成功产品现场图留存（开关 CaptureSuccess.Enabled，默认关）：
            // 克隆在调用线程完成，PNG 编码/写盘在后台线程池，不阻塞管线
            if (!preview && captures is not null)
                captures.Save(recipeName, undistorted, robotPoses, success, failureCtx);

            return Core(success, TryHandoffPreview(preview, recipe, ref undistorted, pixelPoses));
        }
        catch (RecipeNotFoundException)
        {
            return Core(VisionResult.Fail(recipeName, VisionErrorCode.UnknownRecipe,
                $"配方不存在: {recipeName}", stopwatch.Elapsed.TotalMilliseconds));
        }
        catch (InvalidRecipeException ex)
        {
            return Core(VisionResult.Fail(recipeName, ex.ErrorCode, ex.Message, stopwatch.Elapsed.TotalMilliseconds));
        }
        catch (OperationCanceledException)
        {
            // 取图/光源延时阶段被取消（调用方超时）：按处理超时返回；
            // 晚到的结果由 RunAsync 的 TrySetResult 落空丢弃，不产生僵尸应答
            return Core(VisionResult.Fail(recipeName, VisionErrorCode.Timeout,
                "处理超时", stopwatch.Elapsed.TotalMilliseconds));
        }
        catch (VisionException vex)
        {
            var fail = VisionResult.Fail(recipeName, vex.ErrorCode, vex.Message,
                stopwatch.Elapsed.TotalMilliseconds);
            if (undistorted is not null && !preview)
                failureImages.Save(recipeName, undistorted, fail, failureCtx);
            return Core(fail);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "配方 {Recipe} 处理异常", recipeName);
            var fail = VisionResult.Fail(recipeName, VisionErrorCode.InternalError, ex.Message,
                stopwatch.Elapsed.TotalMilliseconds);
            if (undistorted is not null && !preview)
                failureImages.Save(recipeName, undistorted, fail, failureCtx);
            return Core(fail);
        }
        finally
        {
            Scheduler.CompleteExecution(stopwatch.Elapsed.TotalMilliseconds);
            undistorted?.Dispose();
        }
    }

    /// <summary>
    /// 各阶段耗时（非累计）：取图 / 去畸变或克隆 / 推理 / 后处理。
    /// 取图、推理括号内为细分（配方/点亮/稳定/等锁/采集/转图，分割/精修），不足 0.5ms 省略。
    /// 比例与多项式工位跳过内参 Remap，只克隆原图，标签用「克隆」以免误以为做了去畸变。
    /// </summary>
    internal static string FormatStageMs(
        StationMappingMode mode, double grabEndMs, double prepEndMs, double inferEndMs, double totalMs) =>
        FormatStageMs(new PipelineStageMs
        {
            Mode = mode,
            GrabMs = grabEndMs,
            PrepMs = Math.Max(0, prepEndMs - grabEndMs),
            InferMs = Math.Max(0, inferEndMs - prepEndMs),
            PostMs = Math.Max(0, totalMs - inferEndMs),
        });

    internal static string FormatStageMs(PipelineStageMs s)
    {
        var grabHead = $"取图 {s.GrabMs:0}";
        var grabParts = JoinStageParts(
            ("配方", s.RecipeMs),
            ("点亮", s.LightOnMs),
            ("稳定", s.StabilizeMs),
            ("等锁", s.GateWaitMs),
            ("采集", s.AcquireMs),
            ("转图", s.ConvertMs));
        if (grabParts.Length > 0)
            grabHead += $"（{grabParts}）";

        var prepName = s.Mode is StationMappingMode.Scale or StationMappingMode.Polynomial
            ? "克隆"
            : "去畸变";

        var inferHead = $"推理 {s.InferMs:0}";
        var inferParts = JoinStageParts(("分割", s.SegmentMs), ("精修", s.RefineMs));
        if (inferParts.Length > 0)
            inferHead += $"（{inferParts}）";

        return $"{grabHead} · {prepName} {s.PrepMs:0} · {inferHead} · 后处理 {s.PostMs:0}";
    }

    private static string JoinStageParts(params (string Name, double Ms)[] parts)
    {
        List<string>? list = null;
        foreach (var (name, ms) in parts)
        {
            if (ms < 0.5)
                continue;
            (list ??= new List<string>(parts.Length)).Add($"{name} {ms:0}");
        }

        return list is null ? "" : string.Join(" · ", list);
    }

    /// <summary>失败留存诊断上下文（相机/工位/模型/阈值/触发源，写进 JSON 元数据）。</summary>
    private static FailureContext BuildFailureContext(RecipeConfig recipe, bool preview = false) => new(
        CameraId: recipe.CameraId,
        StationId: recipe.StationId,
        Models: recipe.Models.Count > 0 ? string.Join("|", recipe.Models) : null,
        AngleMode: recipe.AngleMode.ToString(),
        Confidence: recipe.Confidence,
        Iou: recipe.Iou,
        Source: preview ? "recipe-preview" : "pipeline");

    private void PublishSnapshot(RecipeConfig recipe, VisionImage undistorted, IReadOnlyList<PixelPose> poses)
    {
        var handler = FrameProcessed;
        if (handler is null)
            return;

        var displayHints = RecipeDisplayHints.ForRecipeTest(recipe);

        foreach (var subscriber in handler.GetInvocationList())
        {
            VisionImage clone;
            try
            {
                clone = undistorted.Clone();
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "快照克隆失败");
                continue;
            }

            var snapshot = new VisionFrameSnapshot(recipe.Name, clone, poses, displayHints);
            _ = Task.Run(() =>
            {
                try
                {
                    subscriber.DynamicInvoke(snapshot);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "FrameProcessed 订阅者处理快照异常");
                    try { snapshot.UndistortedImage.Dispose(); } catch { /* 尽力而为 */ }
                }
            });
        }
    }

    private void EnsureHealthRestored()
    {
        if (health is null || Interlocked.Exchange(ref _healthRestored, 1) != 0)
            return;
        health.RestoreInto(_metrics);
    }
}

/// <summary>管线各阶段耗时（毫秒，非累计）。近 0 的细分项不写入日志。</summary>
internal sealed class PipelineStageMs
{
    public StationMappingMode Mode { get; init; }
    public double GrabMs { get; init; }
    public double PrepMs { get; init; }
    public double InferMs { get; init; }
    public double PostMs { get; init; }
    public double RecipeMs { get; init; }
    public double LightOnMs { get; init; }
    public double StabilizeMs { get; init; }
    public double GateWaitMs { get; init; }
    public double AcquireMs { get; init; }
    public double ConvertMs { get; init; }
    public double SegmentMs { get; init; }
    public double RefineMs { get; init; }
}
