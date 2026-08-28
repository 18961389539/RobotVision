using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using Xunit;

namespace RobotVision.Tests;

public class ProcessHealthTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rv_health_" + Guid.NewGuid().ToString("N"));

    public ProcessHealthTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void ConsecutiveFails_CountProcessErrors_ResetOnSuccess()
    {
        var metrics = new VisionMetrics();
        for (var i = 0; i < 3; i++)
            metrics.Record(VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "miss", 10));

        Assert.Equal(3, metrics.GetConsecutiveFails("A01"));

        metrics.Record(VisionResult.Fail("A01", VisionErrorCode.NotCalibrated, "cal", 10));
        Assert.Equal(3, metrics.GetConsecutiveFails("A01"));

        metrics.Record(VisionResult.Fail("A01", VisionErrorCode.RefineFailed, "refine", 10));
        Assert.Equal(4, metrics.GetConsecutiveFails("A01"));

        metrics.Record(VisionResult.Success("A01", [new RobotPose(1, 2, 3)], 10));
        Assert.Equal(0, metrics.GetConsecutiveFails("A01"));
    }

    [Fact]
    public void Store_InhibitsAtLimit_AndPersistsAcrossRestore()
    {
        var cfg = new ProcessHealthConfig { Enabled = true, ConsecutiveFailLimit = 3, InhibitOnLimit = true };
        var store = new ProcessHealthStore(cfg, _dir, NullLogger<ProcessHealthStore>.Instance);
        var metrics = new VisionMetrics();

        for (var i = 0; i < 3; i++)
        {
            var fail = VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "miss", 8);
            metrics.Record(fail);
            store.OnCompleted(fail, metrics);
        }

        Assert.True(store.IsInhibited(metrics, "A01"));
        Assert.True(File.Exists(Path.Combine(_dir, "health.json")));

        var restored = new VisionMetrics();
        store.RestoreInto(restored);
        Assert.Equal(3, restored.GetConsecutiveFails("A01"));
        Assert.True(store.IsInhibited(restored, "A01"));

        restored.ResetConsecutive("A01");
        store.PersistState(restored);
        Assert.False(store.IsInhibited(restored, "A01"));
    }

    [Fact]
    public void LimitZero_NeverInhibits()
    {
        var store = new ProcessHealthStore(
            new ProcessHealthConfig { Enabled = true, ConsecutiveFailLimit = 0, InhibitOnLimit = true },
            _dir, NullLogger<ProcessHealthStore>.Instance);
        var metrics = new VisionMetrics();
        metrics.Record(VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "miss", 1));
        Assert.False(store.IsInhibited(metrics, "A01"));
    }

    [Fact]
    public async Task VisionService_InhibitedRecipe_Returns1018()
    {
        var recipesDir = Path.Combine(_dir, "recipes");
        Directory.CreateDirectory(recipesDir);
        File.WriteAllText(Path.Combine(recipesDir, "A01.json"), """
            { "cameraId": "cam_v", "angleMode": "KeyPointLine", "models": ["m.onnx"] }
            """);

        var store = new ProcessHealthStore(
            new ProcessHealthConfig { Enabled = true, ConsecutiveFailLimit = 1, InhibitOnLimit = true },
            _dir, NullLogger<ProcessHealthStore>.Instance);
        var seed = new VisionMetrics();
        var fail = VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "miss", 1);
        seed.Record(fail);
        store.OnCompleted(fail, seed);

        var cameras = new CameraManager();
        cameras.Register(new VirtualCamera("cam_v", 64, 64, "Bars"));
        var vision = new VisionService(
            new RecipeLoader(recipesDir), cameras, new LightingManager(), new CalibrationManager(),
            new AngleStrategyFactory(new ModelManager(_dir)),
            new FailureImageStore(new FailureImageConfig { Folder = Path.Combine(_dir, "f") },
                NullLogger<FailureImageStore>.Instance),
            NullLogger<VisionService>.Instance,
            assets: null,
            health: store)
        {
            MaxQueueDepth = 2,
            MaxConcurrent = 1,
        };

        var result = await vision.RunAsync("A01", CancellationToken.None);
        Assert.Equal(VisionErrorCode.ProcessUnhealthy, result.ErrorCode);

        var preview = await vision.RunPreviewAsync(new RecipeConfig
        {
            Name = "A01",
            CameraId = "cam_v",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
        }, null, CancellationToken.None);
        Assert.NotEqual(VisionErrorCode.ProcessUnhealthy, preview.ErrorCode);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => vision.RunAsync("A01", CancellationToken.None))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Equal(VisionErrorCode.ProcessUnhealthy, r.ErrorCode));

        var stats = vision.GetRecipeStats().Single(s => s.Recipe == "A01");
        Assert.Equal(1, stats.Failed);
        Assert.Equal(1, stats.ConsecutiveFails);
    }

    [Fact]
    public async Task InhibitedRecipe_DoesNotOccupyQueueForOtherRecipes()
    {
        var recipesDir = Path.Combine(_dir, "recipes");
        Directory.CreateDirectory(recipesDir);
        File.WriteAllText(Path.Combine(recipesDir, "A01.json"), """
            { "cameraId": "cam_v", "angleMode": "KeyPointLine", "models": ["m.onnx"] }
            """);
        File.WriteAllText(Path.Combine(recipesDir, "B01.json"), """
            { "cameraId": "cam_v", "angleMode": "KeyPointLine", "models": ["m.onnx"] }
            """);

        var store = new ProcessHealthStore(
            new ProcessHealthConfig { Enabled = true, ConsecutiveFailLimit = 1, InhibitOnLimit = true },
            _dir, NullLogger<ProcessHealthStore>.Instance);
        var seed = new VisionMetrics();
        var fail = VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "miss", 1);
        seed.Record(fail);
        store.OnCompleted(fail, seed);

        var cameras = new CameraManager();
        cameras.Register(new VirtualCamera("cam_v", 64, 64, "Bars"));
        var vision = new VisionService(
            new RecipeLoader(recipesDir), cameras, new LightingManager(), new CalibrationManager(),
            new AngleStrategyFactory(new ModelManager(_dir)),
            new FailureImageStore(new FailureImageConfig { Folder = Path.Combine(_dir, "f") },
                NullLogger<FailureImageStore>.Instance),
            NullLogger<VisionService>.Instance,
            assets: null,
            health: store)
        {
            MaxQueueDepth = 1,
            MaxConcurrent = 1,
        };

        var blocked = Enumerable.Range(0, 8)
            .Select(_ => vision.RunAsync("A01", CancellationToken.None));
        var other = vision.RunAsync("B01", CancellationToken.None);
        var results = await Task.WhenAll(blocked.Append(other));

        Assert.All(results.Take(8), r => Assert.Equal(VisionErrorCode.ProcessUnhealthy, r.ErrorCode));
        Assert.NotEqual(VisionErrorCode.Busy, results[8].ErrorCode);
        Assert.NotEqual(VisionErrorCode.ProcessUnhealthy, results[8].ErrorCode);
    }

    [Fact]
    public void Restore_WhenDisabled_StillLoadsConsecutive()
    {
        var on = new ProcessHealthStore(
            new ProcessHealthConfig { Enabled = true, ConsecutiveFailLimit = 1, InhibitOnLimit = true },
            _dir, NullLogger<ProcessHealthStore>.Instance);
        var metrics = new VisionMetrics();
        var fail = VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "miss", 1);
        metrics.Record(fail);
        on.OnCompleted(fail, metrics);

        var off = new ProcessHealthStore(
            new ProcessHealthConfig { Enabled = false, ConsecutiveFailLimit = 1, InhibitOnLimit = true },
            _dir, NullLogger<ProcessHealthStore>.Instance);
        var restored = new VisionMetrics();
        off.RestoreInto(restored);
        Assert.Equal(1, restored.GetConsecutiveFails("A01"));
        Assert.False(off.IsInhibited(restored, "A01"));

        off.ApplyConfig(new ProcessHealthConfig { Enabled = true, ConsecutiveFailLimit = 1, InhibitOnLimit = true });
        Assert.True(off.IsInhibited(restored, "A01"));
    }
}
