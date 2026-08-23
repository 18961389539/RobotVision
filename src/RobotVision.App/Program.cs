using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Core.Recipe;

var builder = Host.CreateApplicationBuilder(args);

// 无论工作目录在哪，都从 exe 旁读取 appsettings.json
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false);

var cfg = builder.Configuration.Get<AppConfig>() ?? new AppConfig();
builder.Logging.AddRobotVisionFileLogging(cfg);
builder.Services.AddRobotVision(cfg);
builder.Services.AddHostedService<TcpHostedService>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

var cameraManager = host.Services.GetRequiredService<CameraManager>();
var calibrationManager = host.Services.GetRequiredService<CalibrationManager>();
var recipeLoader = host.Services.GetRequiredService<RecipeLoader>();

var recipeErrors = recipeLoader.LoadAll();
foreach (var (recipeName, error) in recipeErrors)
    logger.LogWarning("配方 {Recipe} 加载失败: {Error}", recipeName, error);

// TCP 监听状态不在启动日志中预判（端口可能被占用），改由 StartAsync 后据实输出
logger.LogInformation(
    "RobotVision 已启动 | 相机 {Cameras} 台 | 内参 {Intrinsics} 份 | 外参 {Extrinsics} 份 | 旋转中心 {Rotations} 份 | 配方 {Recipes} 个（{Bad} 个无效）| 配方目录 {RecipesFolder}",
    cameraManager.Count,
    calibrationManager.IntrinsicCount, calibrationManager.ExtrinsicCount,
    calibrationManager.RotationCenterCount,
    recipeLoader.LoadedCount, recipeErrors.Count, cfg.ResolveRecipesFolder());

if (cameraManager.Count == 0)
    logger.LogWarning("没有可用相机：请检查 appsettings.json 的 Cameras 配置与回放图片目录");

// RunAsync = StartAsync + WaitForShutdownAsync；拆开是为了在 TcpHostedService 启动
// （监听开始/失败）后，依据 tcp.IsRunning 输出真实的 TCP 状态日志，而非无条件声称已监听
await host.StartAsync();

var tcp = host.Services.GetRequiredService<TcpServerManager>();
if (tcp.IsRunning)
    logger.LogInformation("TCP 服务已监听 {ListenEndPoint} | 白名单 {Whitelist} 条 | 连接上限 {MaxConnections}",
        tcp.ListenEndPoint, tcp.IpWhitelist.Count, tcp.MaxConnections);
else
    logger.LogWarning("TCP 服务启动失败（{ListenEndPoint} 端口可能被占用），机器人链路不可用；视觉服务继续运行",
        tcp.ListenEndPoint);

await host.WaitForShutdownAsync();
