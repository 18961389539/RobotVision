using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
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

        /// <summary>false = 模拟"串口没打开/协议不对"的发送失败。用于验证熄灯不再被静默吞掉。</summary>
        public bool TurnOffSucceeds { get; set; } = true;

        public bool Apply(LightingConfig lighting)
        {
            ApplyCount++;
            LastConfig = lighting;
            return ApplySucceeds;
        }

        public bool SendRaw(string command)
        {
            return true;
        }

        public bool TurnOff()
        {
            TurnOffCount++;
            return TurnOffSucceeds;
        }

        public void Dispose() => DisposedCount++;
    }

    /// <summary>带传输层诊断信息的假控制器：验证失败原因会被带进异常消息。</summary>
    private sealed class DiagnosticLight(string id) : ILightController, ILightDiagnostics
    {
        public string Id { get; } = id;

        public LightControllerKind Kind => LightControllerKind.Virtual;

        public string? LastTransportError { get; set; } = "COM5: UnauthorizedAccessException: 拒绝访问";

        public bool Apply(LightingConfig lighting) => false;

        public bool TurnOff() => false;

        public bool SendRaw(string command)
        {
            return true;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>自包含的日志捕获 sink：断言"否则完全静默"的路径确实留了痕。</summary>
    private sealed class CapturingLogger : ILogger<LightingManager>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
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
    public void Apply_TwoChannels_PassesBothToController()
    {
        var manager = new LightingManager();
        var light = new FakeLight("dongguan");
        manager.Register(light);

        var lighting = new LightingConfig
        {
            Channels =
            [
                new LightingChannelConfig { Channel = 1, Brightness = 180 },
                new LightingChannelConfig { Channel = 2, Brightness = 90 },
            ],
        };

        using var scope = manager.Apply("dongguan", lighting);
        Assert.Equal(2, light.LastConfig?.Channels.Count);
        Assert.Equal(1, light.LastConfig!.Channels[0].Channel);
        Assert.Equal(180, light.LastConfig.Channels[0].Brightness);
        Assert.Equal(2, light.LastConfig.Channels[1].Channel);
        Assert.Equal(90, light.LastConfig.Channels[1].Brightness);
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
    public void SuppressAutoTurnOff_TurnOffAfterGrabTrue_DisposeKeepsLightOn()
    {
        var manager = new LightingManager { SuppressAutoTurnOff = true };
        var light = new FakeLight("l1");
        manager.Register(light);

        // 配方照常要求熄灯（TurnOffAfterGrab 默认 true），但被临时调试开关屏蔽
        using (var scope = manager.Apply("l1", SampleLighting()))
            Assert.Equal(1, light.ApplyCount);

        Assert.Equal(0, light.TurnOffCount);
    }

    [Fact]
    public void SuppressAutoTurnOff_ManualTurnOffStillWorks()
    {
        var manager = new LightingManager { SuppressAutoTurnOff = true };
        var light = new FakeLight("l1");
        manager.Register(light);

        using (manager.Apply("l1", SampleLighting()))
        {
        }

        // 只屏蔽自动熄灯；光源页手动「熄灯」必须照常生效，否则调试时关不掉灯
        manager.TurnOff("l1");
        Assert.Equal(1, light.TurnOffCount);
    }

    [Fact]
    public void SuppressAutoTurnOffFalse_DefaultBehaviourTurnsOff()
    {
        var manager = new LightingManager();
        Assert.False(manager.SuppressAutoTurnOff);

        var light = new FakeLight("l1");
        manager.Register(light);

        using (manager.Apply("l1", SampleLighting()))
        {
        }

        Assert.Equal(1, light.TurnOffCount);
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

    /// <summary>
    /// 熄灯指令发送失败必须抛 1020，不得静默成功。
    /// 2026-09-14 实况：TurnOff 曾忽略 SendFrame 的返回值，串口压根没打开时 UI 照样显示"已熄灯"，
    /// 而开灯会如实抛错 —— 现场就表现为"能关灯、不能开灯"，把排查方向完全带偏。
    /// </summary>
    [Fact]
    public void TurnOff_SendFailed_ThrowsLightCommandFailed_NotSilentSuccess()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1") { TurnOffSucceeds = false };
        manager.Register(light);

        var ex = Assert.Throws<VisionException>(() => manager.TurnOff("l1"));

        Assert.Equal(VisionErrorCode.LightCommandFailed, ex.ErrorCode);
        Assert.Equal(1, light.TurnOffCount); // 确实尝试过发指令，只是失败
    }

    /// <summary>熄灯失败的异常消息必须带上传输层原因，便于现场判断是没插线还是被占用。</summary>
    [Fact]
    public void TurnOff_SendFailed_MessageCarriesTransportCause()
    {
        var manager = new LightingManager();
        manager.Register(new DiagnosticLight("l1"));

        var ex = Assert.Throws<VisionException>(() => manager.TurnOff("l1"));

        Assert.Contains("COM5", ex.Message, StringComparison.Ordinal);
        Assert.Contains("拒绝访问", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>开灯失败的异常消息同样带传输层原因。</summary>
    [Fact]
    public void TurnOn_SendFailed_MessageCarriesTransportCause()
    {
        var manager = new LightingManager();
        manager.Register(new DiagnosticLight("l1"));

        var ex = Assert.Throws<VisionException>(() => manager.TurnOn("l1", 1, 128));

        Assert.Contains("COM5", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>取图收尾的自动熄灯仍是"尽力而为"：失败不得中断取图流程。</summary>
    [Fact]
    public void TurnOffWhileHoldingGate_SendFailed_IsSwallowed()
    {
        var manager = new LightingManager();
        var light = new FakeLight("l1") { TurnOffSucceeds = false };
        manager.Register(light);

        manager.TurnOffWhileHoldingGate("l1"); // 不抛

        Assert.Equal(1, light.TurnOffCount);
    }

    /// <summary>
    /// 自动熄灯失败必须留痕。这条路径不出现在任何 UI 上，若静默，
    /// 现场只会看到"灯一直亮着"而查无实据（2026-09-14 排查光源时正是吃这个亏）。
    /// </summary>
    [Fact]
    public void TurnOffWhileHoldingGate_Rejected_LogsWarningWithoutThrowing()
    {
        var logger = new CapturingLogger();
        var manager = new LightingManager(logger);
        manager.Register(new DiagnosticLight("l1"));   // TurnOff 恒返回 false，且带传输层原因

        manager.TurnOffWhileHoldingGate("l1");         // 不抛

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains("Auto turn-off after grab was rejected", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Message.Contains("COM5", StringComparison.Ordinal));
    }

    /// <summary>未 Dispose 的作用域会一直持锁，Dispose 必须有界返回，否则进程退不掉。</summary>
    [Fact]
    public void Dispose_WhileScopeStillHoldsGate_ReturnsInsteadOfHanging()
    {
        var logger = new CapturingLogger();
        var manager = new LightingManager(logger);
        manager.Register(new FakeLight("l1"));
        _ = manager.Apply("l1", SampleLighting());   // 故意不 Dispose：门闩一直被持有

        var sw = System.Diagnostics.Stopwatch.StartNew();
        manager.Dispose();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"Dispose 疑似挂死：{sw.Elapsed}");
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains("gate drain timed out", StringComparison.Ordinal));
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
