using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting;

using RobotVision.Hosting.Cameras;
using RobotVision.Hosting.Chat;
using RobotVision.Hosting.Lighting;

/// <summary>
/// 视觉服务的统一组装：配置 → 相机/模型/标定管理器 → VisionService → TCP。
/// 由 WPF 宿主（RobotVision.WpfHost）组装调用。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRobotVision(this IServiceCollection services, AppConfig cfg)
    {
        // 启动值域校验：TimeoutMs/MaxQueueDepth/MaxConcurrent/TcpPort/白名单/相机取图超时联动等
        // 非法值直接启动失败（抛出清晰异常），避免 TimeoutMs=100 之类的配置静默生效带病运行
        // （与 AppSettingsStore.Validate 保存时校验同规则，保存能过、启动就能过）
        var bootWarnings = new List<string>();
        var timeoutBefore = cfg.TimeoutMs;
        cfg.NormalizeVisionTiming();
        if (cfg.TimeoutMs != timeoutBefore)
        {
            bootWarnings.Add(
                $"TimeoutMs={timeoutBefore} 低于下限 {AppConfig.DefaultRequestTimeoutMs}，已抬到 {cfg.TimeoutMs}。" +
                "PLC Socket 收包超时须大于此值，文档按 90s 配置，不要再用 5s/10s。");
        }
        AppSettingsStore.ValidateConfig(cfg);
        DataRootBinder.Apply(cfg);
        DataRootBinder.CopyLegacyIfEmpty(cfg, AppContext.BaseDirectory);

        // 目录缺失等启动告警：组装阶段宿主日志尚未可用（自建 LoggerFactory 只输出控制台、
        // 不进文件日志，无头部署会丢），改为暂存文本、由宿主日志管道补发一次
        // （见文件底部 BootWarningLogService，AddRobotVision 末尾统一注册）。

        var recipesFolder = cfg.ResolveAndPrepareRecipesFolder();
        var modelsFolder = cfg.ResolveModelsFolder();
        var calibrationFolder = cfg.ResolveCalibrationFolder();
        foreach (var (label, resolved) in new[]
                 {
                     ("配方目录", recipesFolder), ("模型目录", modelsFolder), ("标定目录", calibrationFolder),
                 })
            if (!Directory.Exists(resolved))
                bootWarnings.Add($"{label} 不存在，将以 exe 目录为基准创建/使用: {resolved}");
        Directory.CreateDirectory(calibrationFolder);

        services.AddSingleton(cfg);
        services.AddSingleton(cfg.Chat);
        services.AddSingleton<OpenAiChatClient>();
        services.AddSingleton<LlamaServerHost>();
        services.AddHostedService(sp => sp.GetRequiredService<LlamaServerHost>());
        services.AddSingleton<ILocalChatClient>(sp => new ManagedLlamaChatClient(
            sp.GetRequiredService<LlamaServerHost>(),
            sp.GetRequiredService<OpenAiChatClient>(),
            sp.GetRequiredService<ChatConfig>()));
        services.AddSingleton<StationChatTools>();
        services.AddSingleton<WebChatClient>();
        services.AddSingleton<ChatToolAuditStore>();
        services.AddSingleton(sp => new ChatToolRegistry(
            sp.GetRequiredService<StationChatTools>().Tools.Concat(sp.GetRequiredService<WebChatClient>().Tools),
            sp.GetRequiredService<ChatToolAuditStore>(),
            sp.GetRequiredService<ChatConfig>()));
        services.AddSingleton<ChatAgent>();
        services.AddSingleton<CameraConfigStore>(new CameraConfigStore(cfg));
        services.AddSingleton<LightingConfigStore>(new LightingConfigStore(cfg));

        // 相机工厂注册表：内置 File/Basler/GigEVision/Virtual 已注册，第三方品牌在启动早期
        // 调 CameraTypeRegistry.Default.Register(...) 一行接入，此处与 UI 均从注册表查询。
        services.AddSingleton(CameraTypeRegistry.Default);
        CameraTypeRegistry.Default.Register(new FileCameraFactory(cfg));

        // 光源控制器工厂注册表：内置 None 已注册，真实控制器（奥普特/康耐视等，
        // 串口/Modbus/TCP）在启动早期调 LightControllerTypeRegistry.Default.Register(...)
        // 一行接入——服务注册、UI 类型下拉、运行时注册全部自动生效，与相机同构。
        services.AddSingleton(LightControllerTypeRegistry.Default);

        // 配方加载器：注入引用完整性校验器（联动相机/模型/标定管理器）。
        // 工厂延迟执行，首次解析 RecipeLoader 时所有依赖均已注册，无循环依赖。
        services.AddSingleton(MaskTemplateRotationCache.Shared);
        services.AddSingleton(sp =>
        {
            var loader = new RecipeLoader(recipesFolder);
            var rotations = sp.GetRequiredService<MaskTemplateRotationCache>();
            var recipeLog = sp.GetRequiredService<ILogger<RecipeLoader>>();
            loader.AfterMaterialize = recipe =>
            {
                try { rotations.Warm(recipe); }
                catch (Exception ex)
                {
                    recipeLog.LogWarning(ex, "配方 {Name} 模板旋转缓存预热失败，首次匹配将现场旋转", recipe.Name);
                }

                try { MaskSiftRefine.Warm(recipe); }
                catch (Exception ex)
                {
                    recipeLog.LogWarning(ex, "配方 {Name} SIFT 示教缓存预热失败，首次精修将现场提取", recipe.Name);
                }

                try { MaskShapeMatch.Warm(recipe); }
                catch (Exception ex)
                {
                    recipeLog.LogWarning(ex, "配方 {Name} 形状匹配示教缓存预热失败，首次精修将现场提取", recipe.Name);
                }
            };
            loader.AfterDelete = name =>
            {
                rotations.Remove(name);
                MaskSiftRefine.Remove(name);
                MaskShapeMatch.Remove(name);
            };
            loader.ReferenceValidator = recipe => ValidateRecipeReferences(
                recipe,
                sp.GetRequiredService<CameraManager>(),
                sp.GetRequiredService<ModelManager>(),
                sp.GetRequiredService<CalibrationManager>(),
                sp.GetRequiredService<LightingManager>());
            return loader;
        });

        services.AddSingleton(sp =>
        {
            var log = sp.GetRequiredService<ILogger<CameraManager>>();
            var registry = sp.GetRequiredService<CameraTypeRegistry>();
            var manager = new CameraManager();
            foreach (var camera in cfg.Cameras)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(camera.Id))
                    {
                        log.LogWarning("配置中存在无 Id 的相机条目，已跳过");
                        continue;
                    }
                    if (manager.IsRegistered(camera.Id))
                    {
                        log.LogWarning("相机 Id 重复: {Id}（后条目已跳过，请检查 appsettings.json）", camera.Id);
                        continue;
                    }
                    if (!registry.TryGet(camera.Type, out var factory))
                    {
                        log.LogWarning("相机 {Id} 类型 {Type} 无工厂（实现 ICameraFactory 并调用 CameraTypeRegistry.Register 一行接入）",
                            camera.Id, camera.Type);
                        manager.Register(new FailedCamera(camera.Id, InferCameraKind(camera.Type),
                            $"相机 {camera.Id} 类型 {camera.Type} 无工厂"));
                        continue;
                    }

                    if (AppConfigExtensions.IsHardwareCameraType(camera.Type) &&
                        string.IsNullOrWhiteSpace(camera.DeviceId))
                    {
                        log.LogWarning(
                            "相机 {Id} 未填写 DeviceId：仅当现场只有一台该类型相机时才能打开，多台将拒绝绑定",
                            camera.Id);
                    }

                    // 超时预算软校验（与具体类型无关：所有带 GrabTimeoutMs 语义的相机通用）
                    if (camera.GrabTimeoutMs > 0 && camera.GrabTimeoutMs >= cfg.TimeoutMs)
                        log.LogWarning("相机 {Id} GrabTimeoutMs={Grab} 不小于总超时 TimeoutMs={Total}，取图超时将表现为 1008 而非 1003，建议调小",
                            camera.Id, camera.GrabTimeoutMs, cfg.TimeoutMs);

                    var instance = factory.Create(camera, log);
                    manager.Register(instance);
                    log.LogInformation("相机 {Id} 已注册（{Type}）", camera.Id, camera.Type);
                }
                catch (Exception ex)
                {
                    var kind = InferCameraKind(camera.Type);
                    var message = ex is VisionException vex
                        ? vex.Message
                        : $"相机 {camera.Id} 初始化失败: {ex.Message}";
                    manager.Register(new FailedCamera(camera.Id, kind, message));
                    log.LogError(ex, "相机 {Id} 初始化失败，使用该相机的配方将返回错误码 {Code}",
                        camera.Id, (int)VisionErrorCode.CameraInitFailed);
                }
            }
            return manager;
        });

        // 推理引擎工厂：Provider 可配（appsettings Inference:Provider，默认 OpenVinoGpu；
        // GPU 会话失败且 CPU 成功则回退并粘性）。模型管理页测试推理走 ModelManager，与产线同锁。
        services.AddSingleton<IInferenceEngineFactory>(sp =>
            new YoloDotNetEngineFactory(
                cfg.Inference.Provider,
                sp.GetService<ILogger<YoloDotNetEngineFactory>>()));
        services.AddSingleton(sp => new ModelManager(
            modelsFolder,
            sp.GetRequiredService<IInferenceEngineFactory>(),
            cfg.Inference.MaxSessions));

        // 光源控制器：None 类型为无操作虚拟实现（调试兜底），
        // 真实控制器（串口/Modbus/TCP）实现 ILightControllerFactory 并注册到
        // LightControllerTypeRegistry 后在此自动创建（与相机注册同构，无需改本方法）。
        services.AddSingleton(sp =>
        {
            var log = sp.GetRequiredService<ILogger<LightingManager>>();
            var registry = sp.GetRequiredService<LightControllerTypeRegistry>();
            var manager = new LightingManager();
            foreach (var light in cfg.LightControllers)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(light.Id))
                    {
                        log.LogWarning("配置中存在无 Id 的光源控制器条目，已跳过");
                        continue;
                    }
                    if (manager.IsRegistered(light.Id))
                    {
                        log.LogWarning("光源控制器 Id 重复: {Id}（后条目已跳过，请检查 appsettings.json）", light.Id);
                        continue;
                    }
                    if (!registry.TryGet(light.Type, out var factory))
                    {
                        log.LogWarning("光源控制器 {Id} 类型 {Type} 无工厂（实现 ILightControllerFactory 并调用 LightControllerTypeRegistry.Register 一行接入）",
                            light.Id, light.Type);
                        manager.Register(new FailedLightController(light.Id,
                            $"光源控制器 {light.Id} 类型 {light.Type} 无工厂"));
                        continue;
                    }

                    var instance = factory.Create(light, log);
                    manager.Register(instance);
                    log.LogInformation("光源控制器 {Id} 已注册（{Type}）", light.Id, light.Type);
                }
                catch (Exception ex)
                {
                    manager.Register(new FailedLightController(light.Id,
                        $"光源控制器 {light.Id} 初始化失败: {ex.Message}"));
                    log.LogError(ex, "光源控制器 {Id} 初始化失败，使用该控制器的配方将返回错误码 {Code}",
                        light.Id, (int)VisionErrorCode.LightNotRegistered);
                }
            }
            return manager;
        });

        services.AddSingleton(sp =>
        {
            var calibration = new CalibrationManager
            {
                // TRIGGER 位姿校验容差（PoseCheck 段；保存设置后经 RuntimeSync 热应用）
                PoseCheckEnabled = cfg.PoseCheck.Enabled,
                PoseXyToleranceMm = cfg.PoseCheck.XyToleranceMm,
                PoseRzToleranceDeg = cfg.PoseCheck.RzToleranceDeg,
            };
            var calibLog = sp.GetRequiredService<ILogger<CalibrationManager>>();
            foreach (var (file, error) in calibration.LoadDirectory(calibrationFolder))
                calibLog.LogWarning("标定档案 {File} 加载失败: {Error}", file, error);
            foreach (var warning in calibration.QualityWarnings)
                calibLog.LogWarning("标定质量警告: {Warning}", warning);
            return calibration;
        });

        services.AddSingleton<AngleStrategyFactory>();

        // 角度策略工厂注册表：内置 MaskMinAreaRect/DualCenterLine/KeyPointLine 已注册，
        // 新增角度模式在启动早期调 AngleStrategyTypeRegistry.Default.Register(...) 一行接入，
        // 此处与 UI 均从注册表查询（与相机/光源注册表同构）。
        services.AddSingleton(AngleStrategyTypeRegistry.Default);
        services.AddSingleton(sp => new FailureImageStore(
            cfg.FailureImage, sp.GetRequiredService<ILogger<FailureImageStore>>()));
        services.AddSingleton(sp => new SqliteResultStore(
            cfg.ResultLog, sp.GetRequiredService<ILogger<SqliteResultStore>>()));
        services.AddSingleton(sp => new ResultLogStore(
            cfg.ResultLog,
            sp.GetRequiredService<ILogger<ResultLogStore>>(),
            sp.GetRequiredService<SqliteResultStore>()));
        services.AddSingleton(sp => new SuccessCaptureStore(
            cfg.CaptureSuccess, sp.GetRequiredService<ILogger<SuccessCaptureStore>>()));

        var metricsFolder = cfg.ResolveMetricsFolder();
        services.AddSingleton(sp =>
            new ProcessHealthStore(cfg.ProcessHealth, metricsFolder,
                sp.GetRequiredService<ILogger<ProcessHealthStore>>()));
        services.AddSingleton<AssetIntegrityChecker>();

        services.AddSingleton(sp => new VisionService(
            sp.GetRequiredService<RecipeLoader>(),
            sp.GetRequiredService<CameraManager>(),
            sp.GetRequiredService<LightingManager>(),
            sp.GetRequiredService<CalibrationManager>(),
            sp.GetRequiredService<AngleStrategyFactory>(),
            sp.GetRequiredService<FailureImageStore>(),
            sp.GetRequiredService<ILogger<VisionService>>(),
            sp.GetRequiredService<AssetIntegrityChecker>(),
            sp.GetRequiredService<ProcessHealthStore>(),
            sp.GetRequiredService<ResultLogStore>(),
            sp.GetRequiredService<SuccessCaptureStore>())
        {
            MaxQueueDepth = Math.Max(1, cfg.MaxQueueDepth),
            MaxConcurrent = Math.Clamp(cfg.MaxConcurrent, 1, Math.Max(1, cfg.MaxQueueDepth)),
        });

        services.AddSingleton(sp =>
        {
            var vision = sp.GetRequiredService<VisionService>();
            var recipes = sp.GetRequiredService<RecipeLoader>();
            return new TcpServerManager(
                cfg.IpAddress,
                cfg.TcpPort,
                cfg.TimeoutMs,
                (recipeKey, pose, ct) =>
                {
                    var (name, resolveError) = recipes.ResolveTriggerKey(recipeKey);
                    if (resolveError is not null)
                    {
                        var code = resolveError switch
                        {
                            "INVALID_RECIPE_NAME" or "INVALID_SERIAL" or "TRIGGER_ARGUMENT_COUNT"
                                or "INVALID_POSE_NUMBER" => VisionErrorCode.InvalidTriggerArgument,
                            _ => VisionErrorCode.UnknownRecipe,
                        };
                        return Task.FromResult(VisionResult.Fail(recipeKey, code, resolveError, 0));
                    }

                    return vision.RunAsync(name!, pose, ct);
                },
                sp.GetRequiredService<ILogger<TcpServerManager>>())
            {
                MaxConnections = cfg.MaxConnections,
                IpWhitelist = cfg.IpWhitelist,
                Backlog = cfg.TcpBacklog,
                IdleTimeoutMs = cfg.IdleTimeoutMs,
                // STATUS：ready 仅在未执行且队列为空时成立，避免 PLC 在排队时误判空闲
                StateProvider = () => new TcpServerManager.TcpServerState(
                    !vision.IsProcessing && vision.QueueDepth == 0,
                    vision.QueueDepth, vision.MaxQueueDepth, vision.LastElapsedMs,
                    vision.MaxConsecutiveFails, vision.AnyInhibited ? 1 : 0),
                ClearInhibitHandler = recipe =>
                {
                    if (string.IsNullOrEmpty(recipe))
                    {
                        vision.ClearInhibit();
                        return "OK,CLEARED";
                    }

                    var (name, resolveError) = recipes.ResolveTriggerKey(recipe);
                    if (resolveError is not null)
                    {
                        var code = resolveError switch
                        {
                            "INVALID_RECIPE_NAME" or "INVALID_SERIAL" or "TRIGGER_ARGUMENT_COUNT"
                                or "INVALID_POSE_NUMBER" => VisionErrorCode.InvalidTriggerArgument,
                            _ => VisionErrorCode.UnknownRecipe,
                        };
                        return TcpServerManager.FormatReply(
                            VisionResult.Fail(recipe, code, resolveError, 0));
                    }

                    vision.ClearInhibit(name);
                    return "OK,CLEARED";
                },
            };
        });

        services.AddSingleton(sp =>
        {
            var store = new AppSettingsStore(cfg);
            // 保存后把可热应用的参数同步到运行中的管理器：任何调用方保存即生效，
            // 不再依赖 UI 逐个手动应用；不可热应用的参数（MaxConcurrent/TcpBacklog
            // 首次固化/启动时读取）显式跳过并记录提示
            store.RuntimeSync = updated =>
            {
                var tcp = sp.GetRequiredService<TcpServerManager>();
                tcp.TimeoutMs = updated.TimeoutMs;
                tcp.IdleTimeoutMs = updated.IdleTimeoutMs;
                tcp.MaxConnections = updated.MaxConnections;
                tcp.IpWhitelist = updated.IpWhitelist;

                var vision = sp.GetRequiredService<VisionService>();
                vision.MaxQueueDepth = Math.Max(1, updated.MaxQueueDepth);

                // 位姿校验容差热应用（1012 的判定标准保存即生效）
                var calibration = sp.GetRequiredService<CalibrationManager>();
                calibration.PoseCheckEnabled = updated.PoseCheck.Enabled;
                calibration.PoseXyToleranceMm = updated.PoseCheck.XyToleranceMm;
                calibration.PoseRzToleranceDeg = updated.PoseCheck.RzToleranceDeg;

                var health = sp.GetRequiredService<ProcessHealthStore>();
                health.ApplyConfig(updated.ProcessHealth);

                var results = sp.GetRequiredService<ResultLogStore>();
                results.ApplyConfig(updated.ResultLog);

                var failures = sp.GetRequiredService<FailureImageStore>();
                failures.Enabled = updated.FailureImage.Enabled;
                failures.RetainedCount = updated.FailureImage.RetainedCount;
                failures.RetainedDays = updated.FailureImage.RetainedDays;

                sp.GetRequiredService<SuccessCaptureStore>().ApplyConfig(updated.CaptureSuccess);

                // MaxConcurrent/TcpBacklog 首次固化或启动时读取，运行时修改不生效（UI 已提示重启）
                if (updated.MaxConcurrent != vision.MaxConcurrent || updated.TcpBacklog != tcp.Backlog)
                    sp.GetRequiredService<ILogger<AppSettingsStore>>().LogInformation(
                        "MaxConcurrent/TcpBacklog 改动已保存到配置文件，需重启程序后生效");
            };
            return store;
        });

        // 组装阶段的启动告警（目录缺失等）经宿主日志管道补发，进入文件日志（无头部署不丢）
        if (bootWarnings.Count > 0)
            services.AddHostedService(sp => new BootWarningLogService(
                bootWarnings, sp.GetRequiredService<ILogger<BootWarningLogService>>()));

        return services;
    }

    private static CameraKind InferCameraKind(string? type) =>
        string.Equals(type, "File", StringComparison.OrdinalIgnoreCase) ? CameraKind.File
        : string.Equals(type, "Virtual", StringComparison.OrdinalIgnoreCase) ? CameraKind.Virtual
        : CameraKind.Real;

    /// <summary>
    /// 配方引用完整性校验：把"相机未注册/模型文件缺失/工位未标定/旋转中心缺失"
    /// 从触发时才报错提前到加载/保存时拦截。返回错误（含协议码）或 null（通过）。
    /// </summary>
    private static RecipeReferenceError? ValidateRecipeReferences(
        RecipeConfig recipe,
        CameraManager cameras,
        ModelManager models,
        CalibrationManager calibration,
        LightingManager lighting)
    {
        if (!cameras.CameraIds.Any(id => string.Equals(id, recipe.CameraId, StringComparison.OrdinalIgnoreCase)))
            return new RecipeReferenceError($"相机未注册: {recipe.CameraId}", VisionErrorCode.CameraNotRegistered);

        if (!string.IsNullOrEmpty(recipe.LightControllerId) &&
            !lighting.IsRegistered(recipe.LightControllerId))
            return new RecipeReferenceError($"光源控制器未注册: {recipe.LightControllerId}", VisionErrorCode.LightNotRegistered);

        foreach (var model in recipe.Models)
        {
            if (!models.ModelFileExists(model))
                return new RecipeReferenceError($"模型文件不存在或为空: {model}", VisionErrorCode.ModelNotAvailable);
        }

        if (!string.IsNullOrEmpty(recipe.StationId) &&
            calibration.GetMappingMode(recipe.StationId) == StationMappingMode.None)
            return new RecipeReferenceError(
                $"工位未做外参/多项式/比例标定: {recipe.StationId}",
                VisionErrorCode.NotCalibrated);

        if (recipe.RotationCompensation == RotationCompensationMode.EccentricTool &&
            !string.IsNullOrEmpty(recipe.StationId) &&
            !calibration.RotationCenterProfiles.Any(p =>
                string.Equals(p.StationId, recipe.StationId, StringComparison.OrdinalIgnoreCase)))
            return new RecipeReferenceError(
                $"工位未做旋转中心标定: {recipe.StationId}（EccentricTool 需要）",
                VisionErrorCode.NotCalibrated);

        return null;
    }

    /// <summary>
    /// 启动告警补发：把 AddRobotVision 组装阶段暂存的告警（此时宿主文件日志尚未可用）在
    /// 宿主启动后经注入的 ILogger 输出——同时进入控制台与文件日志，无头部署不丢告警。
    /// </summary>
    private sealed class BootWarningLogService(IReadOnlyList<string> warnings, ILogger<BootWarningLogService> log)
        : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var warning in warnings)
                log.LogWarning("{Warning}", warning);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
