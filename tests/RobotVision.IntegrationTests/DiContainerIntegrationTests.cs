using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.Hosting.Chat;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.IntegrationTests;

/// <summary>
/// DI 容器集成测试：AddRobotVision 完整组装后可解析全部核心服务、
/// 单例语义、RuntimeSync 热应用、配方引用校验器（联动管理器）。
/// </summary>
public class DiContainerIntegrationTests
{
    [Fact]
    public async Task AddRobotVision_ResolvesAllCoreServices()
    {
        await using var server = await TestServer.StartAsync();
        var sp = server.Provider;

        sp.GetRequiredService<AppConfig>().Should().BeSameAs(server.Cfg);
        sp.GetRequiredService<TcpServerManager>().Should().NotBeNull();
        sp.GetRequiredService<VisionService>().Should().NotBeNull();
        sp.GetRequiredService<RecipeLoader>().Should().NotBeNull();
        sp.GetRequiredService<CameraManager>().Should().NotBeNull();
        sp.GetRequiredService<ModelManager>().Should().NotBeNull();
        sp.GetRequiredService<CalibrationManager>().Should().NotBeNull();
        sp.GetRequiredService<LightingManager>().Should().NotBeNull();
        sp.GetRequiredService<AngleStrategyFactory>().Should().NotBeNull();
        sp.GetRequiredService<FailureImageStore>().Should().NotBeNull();
        sp.GetRequiredService<ResultLogStore>().Should().NotBeNull();
        sp.GetRequiredService<SqliteResultStore>().Should().NotBeNull();
        sp.GetRequiredService<AppSettingsStore>().Should().NotBeNull();
        sp.GetRequiredService<IInferenceEngineFactory>().Should().NotBeNull();
        sp.GetRequiredService<CameraTypeRegistry>().Should().NotBeNull();
        sp.GetRequiredService<LightControllerTypeRegistry>().Should().NotBeNull();
        sp.GetRequiredService<AngleStrategyTypeRegistry>().Should().NotBeNull();
        sp.GetRequiredService<ChatAgent>().Should().NotBeNull();
        var tools = sp.GetRequiredService<ChatToolRegistry>().Specs.Select(t => t.Name).ToList();
        tools.Should().Contain(["capture_frame", "query_results", "manage_recipe", "set_camera", "update_settings",
            "manage_model", "manage_calibration", "manage_files", "convert_pose", "system_info", "web_search", "web_fetch"]);
    }

    [Fact]
    public async Task AddRobotVision_ServicesAreSingletons()
    {
        await using var server = await TestServer.StartAsync();
        var sp = server.Provider;

        sp.GetRequiredService<VisionService>().Should().BeSameAs(sp.GetRequiredService<VisionService>());
        sp.GetRequiredService<TcpServerManager>().Should().BeSameAs(sp.GetRequiredService<TcpServerManager>());
        sp.GetRequiredService<CameraManager>().Should().BeSameAs(sp.GetRequiredService<CameraManager>());
    }

    [Fact]
    public async Task AddRobotVision_RegistersConfiguredCamera()
    {
        await using var server = await TestServer.StartAsync();
        var cameras = server.Provider.GetRequiredService<CameraManager>();

        cameras.CameraIds.Should().Contain("cam_virtual");
        cameras.TryGet("cam_virtual", out var camera).Should().BeTrue();
        camera!.Kind.Should().Be(CameraKind.Virtual);
    }

    [Fact]
    public async Task AddRobotVision_DoesNotRestorePlcAlwaysOkFromConfig()
    {
        await using var server = await TestServer.StartAsync((cfg, _) => cfg.PlcDebug.AlwaysOk = true);
        server.Cfg.PlcDebug.AlwaysOk.Should().BeFalse();
        server.Tcp.PlcAlwaysOkMode.Should().BeFalse();
    }

    [Fact]
    public async Task RuntimeSync_AppliesSavedSettings_Hot()
    {
        await using var server = await TestServer.StartAsync();
        var store = server.Provider.GetRequiredService<AppSettingsStore>();
        var tcp = server.Provider.GetRequiredService<TcpServerManager>();
        var vision = server.Provider.GetRequiredService<VisionService>();

        store.Save(new ServiceSettingsValues(
            TimeoutMs: 1234,
            MaxQueueDepth: 6,
            MaxConcurrent: 2,
            TcpBacklog: 16,
            MaxConnections: 0,
            FailureEnabled: true,
            FailureRetainedCount: 100,
            IpAddress: "127.0.0.1",
            TcpPort: server.Port,
            IpWhitelist: []));

        tcp.TimeoutMs.Should().Be(1234);
        vision.MaxQueueDepth.Should().Be(6);
    }

    [Fact]
    public async Task RecipeReferenceValidator_DetectsUnregisteredCamera()
    {
        // 直接构造配方调用校验器（RecipeLoader.Get 会先抛校验异常，不走该路径）
        await using var server = await TestServer.StartAsync();

        var loader = server.Provider.GetRequiredService<RecipeLoader>();
        var validator = loader.ReferenceValidator;
        validator.Should().NotBeNull();

        var recipe = new RecipeConfig { CameraId = "ghost_cam" };
        var error = validator!(recipe);
        error.HasValue.Should().BeTrue();
        var detail = error!.Value;
        detail.Message.Should().Contain("相机未注册");
        detail.Code.Should().Be(VisionErrorCode.CameraNotRegistered);
    }

    [Fact]
    public async Task RecipeReferenceValidator_AcceptsScaleOnlyStation()
    {
        await using var server = await TestServer.StartAsync((cfg, root) =>
        {
            var models = Path.Combine(root, "models");
            Directory.CreateDirectory(models);
            File.WriteAllText(Path.Combine(models, "test.onnx"), "fake");
            cfg.ModelsFolder = models;
        });

        var calibration = server.Provider.GetRequiredService<CalibrationManager>();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "1",
            CameraId = "cam_virtual",
            ScaleX = 0.05,
            ScaleY = 0.05,
            Width = 640,
            Height = 480,
        });

        var loader = server.Provider.GetRequiredService<RecipeLoader>();
        var error = loader.ReferenceValidator!(new RecipeConfig
        {
            Name = "Cage",
            CameraId = "cam_virtual",
            StationId = "1",
            Models = ["test.onnx"],
        });

        error.Should().BeNull();
    }

    [Fact]
    public async Task RecipeReferenceValidator_RejectsUnknownStation()
    {
        await using var server = await TestServer.StartAsync((cfg, root) =>
        {
            var models = Path.Combine(root, "models");
            Directory.CreateDirectory(models);
            File.WriteAllText(Path.Combine(models, "test.onnx"), "fake");
            cfg.ModelsFolder = models;
        });

        var loader = server.Provider.GetRequiredService<RecipeLoader>();
        var error = loader.ReferenceValidator!(new RecipeConfig
        {
            Name = "Bad",
            CameraId = "cam_virtual",
            StationId = "missing",
            Models = ["test.onnx"],
        });

        error.Should().NotBeNull();
        error!.Value.Message.Should().Contain("比例标定");
        error.Value.Code.Should().Be(VisionErrorCode.NotCalibrated);
    }

    [Theory]
    [InlineData(AngleMode.DualBlobCenterLine)]
    [InlineData(AngleMode.DualTemplateCenterLine)]
    public async Task RecipeReferenceValidator_ModelFree_IgnoresMissingOnnx(AngleMode mode)
    {
        await using var server = await TestServer.StartAsync();
        var loader = server.Provider.GetRequiredService<RecipeLoader>();

        var error = loader.ReferenceValidator!(new RecipeConfig
        {
            Name = "House",
            CameraId = "cam_virtual",
            AngleMode = mode,
            Models = ["gone.onnx", ""],
        });

        error.Should().BeNull();
    }
}
