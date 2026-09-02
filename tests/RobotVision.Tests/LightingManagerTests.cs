using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 光源模块测试：
/// - LightingManager 注册/查找/点亮/熄灯作用域语义；
/// - 未配置照明时零开销（空管理器也不抛错，与旧版行为一致）；
/// - 配方照明值域校验（lightControllerId 与 lighting 成对、通道/亮度边界）。
/// </summary>
public class LightingManagerTests
{
    /// <summary>测试用记录型假控制器：记录 Apply/TurnOff 调用与最近配置。</summary>
    private sealed class FakeLight(string id) : ILightController
    {
        public string Id { get; } = id;

        public LightControllerKind Kind => LightControllerKind.Virtual;

        public int ApplyCount;

        public int TurnOffCount;

        public int DisposedCount;

        public LightingConfig? LastConfig;

        public bool ApplySucceeds { get; set; } = true;

        public bool Apply(LightingConfig lighting)
        {
            ApplyCount++;
            LastConfig = lighting;
            return ApplySucceeds;
        }

        public void SendRaw(string command)
        {
        }

        public void TurnOff() => TurnOffCount++;

        public void Dispose() => DisposedCount++;
    }

    private static LightingConfig SampleLighting(int brightness = 128) => new()
    {
        Channels = [new LightingChannelConfig { Channel = 1, Brightness = brightness }],
        StabilizeDelayMs = 10,
    };

    [Fact]
    public void Apply_WithoutConfig_ReturnsInactiveScope_AndNeverTouchesController()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1");
        manager.Register(light);

        using var scope = manager.Apply(null, null);
        Assert.False(scope.IsActive);
        Assert.Equal(0, scope.StabilizeDelayMs);
        Assert.Equal(0, light.ApplyCount);
        Assert.Equal(0, light.TurnOffCount);
    }

    [Fact]
    public void Apply_ConfigButNoControllerId_IsNoOp_EvenWhenManagerEmpty()
    {
        // 空管理器（未注册任何光源）也不抛错：旧版无光源配方行为完全不变
        var manager = new LightingManager();

        using var scope = manager.Apply(null, SampleLighting());
        Assert.False(scope.IsActive);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Apply_ControllerNotRegistered_ThrowsLightNotRegistered()
    {
        var manager = new LightingManager();

        var ex = Assert.Throws<VisionException>(() => manager.Apply("missing", SampleLighting()));
        Assert.Equal(VisionErrorCode.LightNotRegistered, ex.ErrorCode);
    }

    [Fact]
    public void Apply_SendFailed_ThrowsLightCommandFailed_AndDoesNotTurnOff()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1") { ApplySucceeds = false };
        manager.Register(light);

        var ex = Assert.Throws<VisionException>(() => manager.Apply("l1", SampleLighting()));
        Assert.Equal(VisionErrorCode.LightCommandFailed, ex.ErrorCode);
        Assert.Equal(1, light.ApplyCount);
        Assert.Equal(0, light.TurnOffCount);
    }

    [Fact]
    public void TurnOn_SendFailed_ThrowsLightCommandFailed()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1") { ApplySucceeds = false };
        manager.Register(light);

        var ex = Assert.Throws<VisionException>(() => manager.TurnOn("l1", 1, 128));
        Assert.Equal(VisionErrorCode.LightCommandFailed, ex.ErrorCode);
    }

    [Fact]
    public void Apply_RegisteredController_AppliesAndScopeDisposeTurnsOff()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1");
        manager.Register(light);

        var scope = manager.Apply("l1", SampleLighting(brightness: 200));
        Assert.True(scope.IsActive);
        Assert.Equal(10, scope.StabilizeDelayMs);
        Assert.Equal(1, light.ApplyCount);
        Assert.Equal(200, light.LastConfig?.Channels[0].Brightness);

        scope.Dispose();
        Assert.Equal(1, light.TurnOffCount);

        // 重复 Dispose 幂等
        scope.Dispose();
        Assert.Equal(1, light.TurnOffCount);
    }

    [Fact]
    public void Apply_TurnOffAfterGrabFalse_DisposeKeepsLightOn()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1");
        manager.Register(light);

        var lighting = SampleLighting();
        lighting.TurnOffAfterGrab = false;

        using var scope = manager.Apply("l1", lighting);
        Assert.Equal(0, light.TurnOffCount);
    }

    [Fact]
    public void Apply_SecondCallBlocksUntilScopeDisposed()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1");
        manager.Register(light);

        using var scope1 = manager.Apply("l1", SampleLighting());
        var started = new ManualResetEventSlim(false);
        var finished = new ManualResetEventSlim(false);

        var worker = Task.Run(() =>
        {
            started.Set();
            using var scope2 = manager.Apply("l1", SampleLighting());
            finished.Set();
        });

        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(finished.Wait(TimeSpan.FromMilliseconds(50)));

        scope1.Dispose();
        Assert.True(finished.Wait(TimeSpan.FromSeconds(2)));
        worker.GetAwaiter().GetResult();
        Assert.Equal(2, light.ApplyCount);
    }

    [Fact]
    public void Register_SameId_OverwritesAndDisposesOld()
    {
        var manager = new LightingManager();
        var oldLight = new FakeLight("l1");
        manager.Register(oldLight);

        manager.Register(new FakeLight("l1"));

        Assert.Equal(1, manager.Count);
        Assert.True(manager.IsRegistered("l1"));
        Assert.Equal(1, oldLight.DisposedCount); // 旧实例被释放
    }

    [Fact]
    public void NoopController_IsIdempotent()
    {
        var controller = new NoopLightController("light_none");
        Assert.Equal(LightControllerKind.None, controller.Kind);

        controller.Apply(SampleLighting());
        controller.Apply(SampleLighting());
        controller.TurnOff();
        controller.TurnOff();
        controller.Dispose();
    }

    [Fact]
    public void Validate_LightControllerIdWithoutLighting_Rejected()
    {
        var recipe = new RecipeConfig { Name = "R", CameraId = "cam", Models = ["m.onnx"] };
        recipe.LightControllerId = "light1";

        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("lighting", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_LightingWithoutControllerId_Rejected()
    {
        var recipe = new RecipeConfig { Name = "R", CameraId = "cam", Models = ["m.onnx"] };
        recipe.Lighting = SampleLighting();

        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("lightControllerId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BrightnessOutOfRange_Rejected()
    {
        var recipe = new RecipeConfig { Name = "R", CameraId = "cam", Models = ["m.onnx"] };
        recipe.LightControllerId = "light1";
        recipe.Lighting = SampleLighting(brightness: 300);

        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("亮度", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ChannelZeroOrNegative_Rejected()
    {
        var recipe = new RecipeConfig { Name = "R", CameraId = "cam", Models = ["m.onnx"] };
        recipe.LightControllerId = "light1";
        recipe.Lighting = new LightingConfig
        {
            Channels = [new LightingChannelConfig { Channel = 0, Brightness = 128 }],
        };

        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("通道号", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NegativeStabilizeDelay_Rejected()
    {
        var recipe = new RecipeConfig { Name = "R", CameraId = "cam", Models = ["m.onnx"] };
        recipe.LightControllerId = "light1";
        recipe.Lighting = SampleLighting();
        recipe.Lighting.StabilizeDelayMs = -5;

        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("stabilizeDelayMs", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ValidLighting_Passes()
    {
        var recipe = new RecipeConfig { Name = "R", CameraId = "cam", Models = ["m.onnx"] };
        recipe.LightControllerId = "light1";
        recipe.Lighting = SampleLighting();

        RecipeLoader.Validate(recipe); // 不抛即为通过
    }

    [Fact]
    public void TurnOn_TurnsOnWithChannelAndBrightness_KeepsOnUntilTurnOff()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1");
        manager.Register(light);

        manager.TurnOn("l1", channel: 2, brightness: 180);

        Assert.Equal(1, light.ApplyCount);
        Assert.Equal(2, light.LastConfig?.Channels[0].Channel);
        Assert.Equal(180, light.LastConfig?.Channels[0].Brightness);
        Assert.False(light.LastConfig?.TurnOffAfterGrab); // 手动模式不自动熄灯
        Assert.Equal(0, light.TurnOffCount);

        manager.TurnOff("l1");
        Assert.Equal(1, light.TurnOffCount);
    }

    [Fact]
    public void TurnOn_ClampsChannelAndBrightness()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1");
        manager.Register(light);

        manager.TurnOn("l1", channel: 0, brightness: 999);

        Assert.Equal(1, light.LastConfig?.Channels[0].Channel);
        Assert.Equal(255, light.LastConfig?.Channels[0].Brightness);
    }

    [Fact]
    public void TurnOn_UnregisteredController_ThrowsLightNotRegistered()
    {
        var manager = new LightingManager();

        Assert.Throws<VisionException>(() => manager.TurnOn("missing", 1, 128));
        Assert.Throws<VisionException>(() => manager.TurnOff("missing"));
    }

    [Fact]
    public void RecipeClone_LightingIsIndependentDeepCopy()
    {
        var recipe = new RecipeConfig { Name = "R", CameraId = "cam", Models = ["m.onnx"] };
        recipe.LightControllerId = "light1";
        recipe.Lighting = SampleLighting();

        var clone = recipe.Clone();
        clone.Lighting!.Channels[0].Brightness = 1;
        clone.Lighting.StabilizeDelayMs = 99;

        Assert.Equal(128, recipe.Lighting.Channels[0].Brightness);
        Assert.Equal(10, recipe.Lighting.StabilizeDelayMs);
    }

    // ---- VisionService 管线集成（点亮时机验证）----

    private static (VisionService Service, FakeLight Light) CreatePipeline(string recipeJson)
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_light_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "L1.json"), recipeJson);

        var replay = Path.Combine(Path.GetTempPath(), "rv_light_replay_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(replay);
        using (var img = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(100)))
            OpenCvSharp.Cv2.ImWrite(Path.Combine(replay, "f.bmp"), img);

        var cameras = new CameraManager();
        cameras.Register(new FileCamera("cam1", replay));

        var calibration = new CalibrationManager();
        calibration.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 64,
            Height = 64,
            CameraMatrix = [100, 0, 32, 0, 100, 32, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });

        var lighting = new LightingManager();
        var light = new FakeLight("light1");
        lighting.Register(light);

        var failureImages = new FailureImageStore(
            new FailureImageConfig { Folder = Path.Combine(Path.GetTempPath(), "rv_nowhere") },
            NullLogger<FailureImageStore>.Instance);

        var service = new VisionService(
            new RecipeLoader(folder), cameras, lighting, calibration,
            new AngleStrategyFactory(new RobotVision.Infrastructure.Inference.ModelManager(Path.GetTempPath())),
            failureImages,
            NullLogger<VisionService>.Instance);

        return (service, light);
    }

    [Fact]
    public async Task Pipeline_RecipeWithoutLighting_NeverTouchesController()
    {
        var (service, light) = CreatePipeline("""
            {
              "cameraId": "cam1",
              "angleMode": "MaskMinAreaRect",
              "models": [ "no_such_model.onnx" ]
            }
            """);

        var result = await service.RunAsync("L1", CancellationToken.None);

        Assert.Equal(VisionErrorCode.ModelNotAvailable, result.ErrorCode); // 流程走到推理才失败
        Assert.Equal(0, light.ApplyCount);
        Assert.Equal(0, light.TurnOffCount);
    }

    [Fact]
    public async Task Pipeline_RecipeWithLighting_AppliesBeforeGrab_AndTurnsOffAfter()
    {
        var (service, light) = CreatePipeline("""
            {
              "cameraId": "cam1",
              "angleMode": "MaskMinAreaRect",
              "models": [ "no_such_model.onnx" ],
              "lightControllerId": "light1",
              "lighting": {
                "channels": [ { "channel": 1, "brightness": 200 } ],
                "stabilizeDelayMs": 5,
                "turnOffAfterGrab": true
              }
            }
            """);

        var result = await service.RunAsync("L1", CancellationToken.None);

        Assert.Equal(VisionErrorCode.ModelNotAvailable, result.ErrorCode);
        Assert.Equal(1, light.ApplyCount);
        Assert.Equal(200, light.LastConfig?.Channels[0].Brightness);
        Assert.Equal(1, light.TurnOffCount); // 管线结束即熄灯（作用域 Dispose）
    }

    [Fact]
    public async Task Pipeline_LightingControllerNotRegistered_Returns1006()
    {
        var (service, _) = CreatePipeline("""
            {
              "cameraId": "cam1",
              "angleMode": "MaskMinAreaRect",
              "models": [ "no_such_model.onnx" ],
              "lightControllerId": "missing_light",
              "lighting": {
                "channels": [ { "channel": 1, "brightness": 128 } ]
              }
            }
            """);

        var result = await service.RunAsync("L1", CancellationToken.None);

        Assert.Equal(VisionErrorCode.LightNotRegistered, result.ErrorCode);
    }

    [Fact]
    public async Task Pipeline_LightingSendFailed_Returns1020_AndDoesNotGrabInference()
    {
        var (service, light) = CreatePipeline("""
            {
              "cameraId": "cam1",
              "angleMode": "MaskMinAreaRect",
              "models": [ "no_such_model.onnx" ],
              "lightControllerId": "light1",
              "lighting": {
                "channels": [ { "channel": 1, "brightness": 128 } ]
              }
            }
            """);
        light.ApplySucceeds = false;

        var result = await service.RunAsync("L1", CancellationToken.None);

        Assert.Equal(VisionErrorCode.LightCommandFailed, result.ErrorCode);
        Assert.Equal(1, light.ApplyCount);
        Assert.Equal(0, light.TurnOffCount);
    }
}
