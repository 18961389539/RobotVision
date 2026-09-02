using Microsoft.Extensions.DependencyInjection;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting;

public static class HostingRuntimeExtensions
{
    /// <summary>注册 WPF/宿主用的运行时门面（委托已有 Infrastructure 管理器）。</summary>
    public static IServiceCollection AddHostingRuntimeFacades(this IServiceCollection services)
    {
        services.AddSingleton<ICameraRuntime>(sp => new CameraRuntime(sp.GetRequiredService<CameraManager>()));
        services.AddSingleton<ICalibrationRuntime>(sp => new CalibrationRuntime(sp.GetRequiredService<CalibrationManager>()));
        services.AddSingleton<ILightingRuntime>(sp => new LightingRuntime(sp.GetRequiredService<LightingManager>()));
        services.AddSingleton<IModelRuntime>(sp => new ModelRuntime(sp.GetRequiredService<ModelManager>()));
        services.AddSingleton<IInferenceRuntime>(sp => new InferenceRuntime(sp.GetRequiredService<IInferenceEngineFactory>()));
        services.AddSingleton<IRecipeSetupAnalysisService, RecipeSetupAnalysisService>();
        services.AddSingleton<IMaskTemplateTeachService, MaskTemplateTeachService>();
        services.AddSingleton<IImageFileReader, ImageFileReader>();
        services.AddSingleton<IMonitorPreviewService, MonitorPreviewService>();
        services.AddSingleton<ICalibrationWizardService>(sp => new CalibrationWizardService(
            sp.GetRequiredService<ICameraRuntime>(),
            sp.GetRequiredService<ICalibrationRuntime>()));
        services.AddSingleton<IModelTestService>(sp => new ModelTestService(sp.GetRequiredService<ModelManager>()));
        services.AddSingleton<ISegmentRefineGuidance, SegmentRefineGuidance>();
        services.AddSingleton<ITcpRuntime>(sp => new TcpRuntime(sp.GetRequiredService<TcpServerManager>()));
        services.AddSingleton<IAngleStrategyCatalog>(sp =>
            new AngleStrategyCatalog(sp.GetRequiredService<AngleStrategyTypeRegistry>()));
        return services;
    }

    public static IInferenceRuntime AsInferenceRuntime(this IInferenceEngineFactory factory) =>
        new InferenceRuntime(factory);

    public static ICameraRuntime AsRuntimeFacade(this CameraManager manager) => new CameraRuntime(manager);

    public static ICalibrationRuntime AsRuntimeFacade(this CalibrationManager manager) => new CalibrationRuntime(manager);

    public static ILightingRuntime AsRuntimeFacade(this LightingManager manager) => new LightingRuntime(manager);

    public static IModelRuntime AsRuntimeFacade(this ModelManager manager) => new ModelRuntime(manager);

    public static ITcpRuntime AsRuntimeFacade(this TcpServerManager manager) => new TcpRuntime(manager);

    public static IAngleStrategyCatalog AsRuntimeFacade(this AngleStrategyTypeRegistry registry) =>
        new AngleStrategyCatalog(registry);
}
