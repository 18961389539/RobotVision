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
///
/// <see cref="AddRobotVision"/> 只做启动校验与"按子系统串起注册"，具体注册分散到
/// <c>RegisterXxx</c> 私有方法（见 <see cref="ServiceCollectionExtensions.Registrations"/>），
/// 每块职责单一、改动面局部。
/// </summary>
public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddRobotVision(this IServiceCollection services, AppConfig cfg)
    {
        // 启动值域校验 + 目录准备：非法值直接启动失败，避免带病运行（与 AppSettingsStore 保存校验同规则）。
        var bootWarnings = new List<string>();
        PrepareStartup(cfg, bootWarnings);
        var paths = ResolvePaths(cfg, bootWarnings);

        services.AddSingleton(cfg);
        RegisterChat(services, cfg);
        RegisterConfigStores(services, cfg);
        RegisterDeviceRegistries(services, cfg);
        RegisterRecipeLoader(services, paths);
        RegisterCameras(services, cfg);
        RegisterInference(services, cfg, paths);
        RegisterLighting(services, cfg);
        RegisterCalibration(services, cfg, paths);
        RegisterAngleStrategyAndPersistence(services, cfg, paths);
        RegisterVisionService(services, cfg);
        RegisterTcpServer(services, cfg);
        RegisterAppSettingsStore(services);
        services.AddHostingRuntimeFacades();

        // 组装阶段的启动告警（目录缺失等）经宿主日志管道补发，进入文件日志（无头部署不丢）。
        if (bootWarnings.Count > 0)
            services.AddHostedService(sp => new BootWarningLogService(
                bootWarnings, sp.GetRequiredService<ILogger<BootWarningLogService>>()));

        return services;
    }
}
