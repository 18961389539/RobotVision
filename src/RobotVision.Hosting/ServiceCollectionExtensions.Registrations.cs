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
using RobotVision.JlVision;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting;

using RobotVision.Hosting.Cameras;
using RobotVision.Hosting.Chat;
using RobotVision.Hosting.Lighting;

/// <summary>
/// <see cref="ServiceCollectionExtensions"/> 的注册实现：每个 <c>RegisterXxx</c> 负责一个子系统的
/// DI 装配，彼此独立，改一处不牵动其余。工厂委托体保持原样。
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>启动期已解析并准备好的目录路径。</summary>
    private readonly record struct StartupPaths(
        string Recipes, string Models, string Calibration, string Metrics);

    /// <summary>值域校验 + 数据根 + PlcDebug 安全复位；非法值直接抛，告警写进 <paramref name="bootWarnings"/>。</summary>
    private static void PrepareStartup(AppConfig cfg, List<string> bootWarnings)
    {
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

        // AlwaysOk 只允许本会话从通讯/设置页打开，启动不从磁盘恢复，避免产线默认伪装 OK。
        if (cfg.PlcDebug.AlwaysOk)
        {
            bootWarnings.Add(
                "已忽略配置文件中的 PlcDebug.AlwaysOk：该开关不会在启动时恢复。" +
                "需要联调时请在通讯页临时开启。");
            cfg.PlcDebug.AlwaysOk = false;
        }
    }

    /// <summary>解析配方/模型/标定/指标目录；缺失记启动告警；建标定目录。</summary>
    private static StartupPaths ResolvePaths(AppConfig cfg, List<string> bootWarnings)
    {
        var recipes = cfg.ResolveAndPrepareRecipesFolder();
        var models = cfg.ResolveModelsFolder();
        var calibration = cfg.ResolveCalibrationFolder();
        var metrics = cfg.ResolveMetricsFolder();
        foreach (var (label, resolved) in new[]
                 {
                     ("配方目录", recipes), ("模型目录", models), ("标定目录", calibration),
                 })
            if (!Directory.Exists(resolved))
                bootWarnings.Add($"{label} 不存在，将以 exe 目录为基准创建/使用: {resolved}");
        Directory.CreateDirectory(calibration);
        return new StartupPaths(recipes, models, calibration, metrics);
    }

    private static void RegisterChat(IServiceCollection services, AppConfig cfg)
    {
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
    }

    private static void RegisterConfigStores(IServiceCollection services, AppConfig cfg)
    {
        services.AddSingleton<IRecipeTestService, RecipeTestService>();
        services.AddSingleton(new CameraConfigStore(cfg, ApplicationPaths.UserSettingsPath));
        services.AddSingleton(new LightingConfigStore(cfg, ApplicationPaths.UserSettingsPath));
    }

    /// <summary>
    /// 相机/光源工厂注册表：内置类型已注册，第三方品牌/控制器在启动早期调
    /// <c>*TypeRegistry.Default.Register(...)</c> 一行接入，此处与 UI 均从注册表查询。
    /// </summary>
    private static void RegisterDeviceRegistries(IServiceCollection services, AppConfig cfg)
    {
        services.AddSingleton(CameraTypeRegistry.Default);
        CameraTypeRegistry.Default.Register(new FileCameraFactory(cfg));
        services.AddSingleton(LightControllerTypeRegistry.Default);
    }

    private static void RegisterRecipeLoader(IServiceCollection services, StartupPaths paths)
    {
        services.AddSingleton(sp =>
        {
            var loader = new RecipeLoader(paths.Recipes);
            var recipeLog = sp.GetRequiredService<ILogger<RecipeLoader>>();
            loader.AfterMaterialize = recipe =>
            {
                try { JlShapeTeachCache.Warm(recipe); }
                catch (Exception ex)
                {
                    ServiceCollectionExtensionsLog.RecipeShapeMatchWarmFailed(recipeLog, ex, recipe.Name);
                }

                try { JlNccTeachCache.Warm(recipe); }
                catch (Exception ex)
                {
                    ServiceCollectionExtensionsLog.RecipeRotationWarmFailed(recipeLog, ex, recipe.Name);
                }
            };
            loader.AfterDelete = name =>
            {
                JlShapeTeachCache.Remove(name);
                JlNccTeachCache.Remove(name);
            };
            loader.ReferenceValidator = recipe => ValidateRecipeReferences(
                recipe,
                sp.GetRequiredService<CameraManager>(),
                sp.GetRequiredService<ModelManager>(),
                sp.GetRequiredService<CalibrationManager>(),
                sp.GetRequiredService<LightingManager>());
            return loader;
        });
    }

    private static void RegisterCameras(IServiceCollection services, AppConfig cfg)
    {
        services.AddSingleton(sp =>
        {
            var log = sp.GetRequiredService<ILogger<CameraManager>>();
            var registry = sp.GetRequiredService<CameraTypeRegistry>();
            var manager = new CameraManager(log);
            foreach (var camera in cfg.Cameras)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(camera.Id))
                    {
                        ServiceCollectionExtensionsLog.CameraEntryMissingId(log);
                        continue;
                    }
                    if (manager.IsRegistered(camera.Id))
                    {
                        ServiceCollectionExtensionsLog.CameraDuplicateId(log, camera.Id);
                        continue;
                    }
                    if (!registry.TryGet(camera.Type, out var factory))
                    {
                        ServiceCollectionExtensionsLog.CameraNoFactory(log, camera.Id, camera.Type);
                        manager.Register(new FailedCamera(camera.Id, InferCameraKind(camera.Type),
                            $"相机 {camera.Id} 类型 {camera.Type} 无工厂"));
                        continue;
                    }

                    if (AppConfigExtensions.IsHardwareCameraType(camera.Type) &&
                        string.IsNullOrWhiteSpace(camera.DeviceId))
                    {
                        ServiceCollectionExtensionsLog.CameraMissingDeviceId(log, camera.Id);
                    }

                    // 超时预算软校验（与具体类型无关：所有带 GrabTimeoutMs 语义的相机通用）
                    if (camera.GrabTimeoutMs > 0 && camera.GrabTimeoutMs >= cfg.TimeoutMs)
                        ServiceCollectionExtensionsLog.CameraGrabTimeoutExceedsTotal(
                            log, camera.Id, camera.GrabTimeoutMs, cfg.TimeoutMs);

                    var instance = factory.Create(camera, log);
                    manager.Register(instance);
                    ServiceCollectionExtensionsLog.CameraRegistered(log, camera.Id, camera.Type);
                }
                catch (Exception ex)
                {
                    var kind = InferCameraKind(camera.Type);
                    var message = ex is VisionException vex
                        ? vex.Message
                        : $"相机 {camera.Id} 初始化失败: {ex.Message}";
                    manager.Register(new FailedCamera(camera.Id, kind, message));
                    ServiceCollectionExtensionsLog.CameraInitFailed(
                        log, ex, camera.Id, (int)VisionErrorCode.CameraInitFailed);
                }
            }
            return manager;
        });
    }

    /// <summary>推理引擎工厂 + 模型管理：Provider 可配（默认 OpenVinoGpu，GPU 失败回退 CPU 粘性）。</summary>
    private static void RegisterInference(IServiceCollection services, AppConfig cfg, StartupPaths paths)
    {
        services.AddSingleton<IInferenceEngineFactory>(sp =>
            new YoloDotNetEngineFactory(
                cfg.Inference.Provider,
                sp.GetService<ILogger<YoloDotNetEngineFactory>>()));
        services.AddSingleton(sp => new ModelManager(
            paths.Models,
            sp.GetRequiredService<IInferenceEngineFactory>(),
            cfg.Inference.MaxSessions));
    }

    /// <summary>光源控制器：None 为虚拟实现；真实控制器注册进 <see cref="LightControllerTypeRegistry"/> 后自动创建。</summary>
    private static void RegisterLighting(IServiceCollection services, AppConfig cfg)
    {
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
                        ServiceCollectionExtensionsLog.LightEntryMissingId(log);
                        continue;
                    }
                    if (manager.IsRegistered(light.Id))
                    {
                        ServiceCollectionExtensionsLog.LightDuplicateId(log, light.Id);
                        continue;
                    }
                    if (!registry.TryGet(light.Type, out var factory))
                    {
                        ServiceCollectionExtensionsLog.LightNoFactory(log, light.Id, light.Type);
                        manager.Register(new FailedLightController(light.Id,
                            $"光源控制器 {light.Id} 类型 {light.Type} 无工厂"));
                        continue;
                    }

                    var instance = factory.Create(light, log);
                    manager.Register(instance);
                    ServiceCollectionExtensionsLog.LightRegistered(log, light.Id, light.Type);
                }
                catch (Exception ex)
                {
                    manager.Register(new FailedLightController(light.Id,
                        $"光源控制器 {light.Id} 初始化失败: {ex.Message}"));
                    ServiceCollectionExtensionsLog.LightInitFailed(
                        log, ex, light.Id, (int)VisionErrorCode.LightNotRegistered);
                }
            }
            return manager;
        });
    }

    private static void RegisterCalibration(IServiceCollection services, AppConfig cfg, StartupPaths paths)
    {
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
            foreach (var (file, error) in calibration.LoadDirectory(paths.Calibration))
                ServiceCollectionExtensionsLog.CalibrationFileLoadFailed(calibLog, file, error);
            foreach (var warning in calibration.QualityWarnings)
                ServiceCollectionExtensionsLog.CalibrationQualityWarning(calibLog, warning);
            return calibration;
        });
    }

    /// <summary>角度策略注册表 + 精修注册表 + 失败图/结果库/留存/健康等持久化组件。</summary>
    private static void RegisterAngleStrategyAndPersistence(IServiceCollection services, AppConfig cfg, StartupPaths paths)
    {
        services.AddSingleton<AngleStrategyFactory>();
        services.AddSingleton(AngleStrategyTypeRegistry.Default);
        services.AddSingleton(SegmentRefineRuntimeRegistry.Default);
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

        services.AddSingleton(sp =>
            new ProcessHealthStore(cfg.ProcessHealth, paths.Metrics,
                sp.GetRequiredService<ILogger<ProcessHealthStore>>()));
        services.AddSingleton<AssetIntegrityChecker>();
    }

    private static void RegisterVisionService(IServiceCollection services, AppConfig cfg)
    {
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
    }

    private static void RegisterTcpServer(IServiceCollection services, AppConfig cfg)
    {
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
                PlcAlwaysOkMode = false,
                PlcDebugDefaultX = cfg.PlcDebug.DefaultX,
                PlcDebugDefaultY = cfg.PlcDebug.DefaultY,
                PlcDebugDefaultRz = cfg.PlcDebug.DefaultRz,
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
    }

    private static void RegisterAppSettingsStore(IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var store = new AppSettingsStore(sp.GetRequiredService<AppConfig>(), ApplicationPaths.UserSettingsPath);
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
                tcp.PlcAlwaysOkMode = updated.PlcDebug.AlwaysOk;
                tcp.PlcDebugDefaultX = updated.PlcDebug.DefaultX;
                tcp.PlcDebugDefaultY = updated.PlcDebug.DefaultY;
                tcp.PlcDebugDefaultRz = updated.PlcDebug.DefaultRz;

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
                    ServiceCollectionExtensionsLog.RuntimeSyncRestartRequired(
                        sp.GetRequiredService<ILogger<AppSettingsStore>>());
            };
            return store;
        });
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
                ServiceCollectionExtensionsLog.BootWarning(log, warning);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
