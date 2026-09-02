using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Hosting;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// VisionService 队列与超时行为测试：
/// - 排队阶段超时 → 立即返回 Timeout 且放弃排队；
/// - 处理阶段不可取消 → 后台跑完释放管线，后续请求不受影响；
/// - 队列深度超限 → 立即 Busy。
/// REAL 系列用例改用 Fake 推理引擎（可注入 400ms 耗时占管线），不依赖仓库真实 ONNX，
/// 本地/CI/无模型环境行为一致。
/// </summary>
[Collection("Serial")]
public class VisionServiceQueueTests : IDisposable
{
    private readonly string _recipeFolder = Path.Combine(Path.GetTempPath(), "rv_vsq_" + Guid.NewGuid().ToString("N"));
    private readonly string _replayFolder = Path.Combine(Path.GetTempPath(), "rv_vsq_replay_" + Guid.NewGuid().ToString("N"));

    public VisionServiceQueueTests()
    {
        Directory.CreateDirectory(_recipeFolder);
        Directory.CreateDirectory(_replayFolder);
        using (var img = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(100)))
            OpenCvSharp.Cv2.ImWrite(Path.Combine(_replayFolder, "f.bmp"), img);

        WriteRecipe("SLOW", """
            {
              "cameraId": "cam1",
              "angleMode": "MaskMinAreaRect",
              "models": [ "vsq_missing_model.onnx" ]
            }
            """);
        WriteRecipe("REAL", """
            {
              "cameraId": "cam1",
              "angleMode": "KeyPointLine",
              "models": [ "fake_pose.onnx" ],
              "keypointIndexA": 0,
              "keypointIndexB": 1
            }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_recipeFolder, true); } catch (IOException) { }
        try { Directory.Delete(_replayFolder, true); } catch (IOException) { }
    }

    private void WriteRecipe(string name, string json) =>
        File.WriteAllText(Path.Combine(_recipeFolder, name + ".json"), json);

    private VisionService CreateService(int maxDepth, int maxConcurrent = 1, string? failureFolder = null)
    {
        var recipes = new RecipeLoader(_recipeFolder);
        var cameras = new CameraManager();
        cameras.Register(new FileCamera("cam1", _replayFolder));

        var calibration = new CalibrationManager();
        calibration.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 64,
            Height = 64,
            CameraMatrix = [100, 0, 32, 0, 100, 32, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });

        var failureImages = new FailureImageStore(
            new FailureImageConfig { Folder = failureFolder ?? Path.Combine(Path.GetTempPath(), "rv_nowhere") },
            NullLogger<FailureImageStore>.Instance);

        // Fake 推理引擎:不依赖真实 ONNX(CI/沙箱无模型环境一致);
        // REAL 配方(KeyPointLine→Pose)返回空结果→1007,注入 400ms 耗时占住唯一并发槽,
        // 供排队/超限/busy 语义测试;占位模型文件仅通过 File.Exists 检查
        var modelFolder = Path.Combine(Path.GetTempPath(), "rv_vsq_models_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modelFolder);
        File.WriteAllBytes(Path.Combine(modelFolder, "fake_pose.onnx"), [1, 2, 3, 4]);
        var engineFactory = new FakeInferenceEngineFactory(() => new FakeInferenceEngine
        {
            OnPose = _ => { Thread.Sleep(400); return []; },
            OnObjectDetection = _ => { Thread.Sleep(400); return []; },
            OnSegmentation = _ => { Thread.Sleep(400); return []; },
        });
        var models = new RobotVision.Infrastructure.Inference.ModelManager(modelFolder, engineFactory);

        return new VisionService(recipes, cameras, new LightingManager(), calibration,
            new AngleStrategyFactory(models),
            failureImages,
            NullLogger<VisionService>.Instance)
        {
            MaxQueueDepth = maxDepth,
            MaxConcurrent = maxConcurrent,
        };
    }

    [Fact]
    public async Task PreCancelledRequest_ReturnsQueueTimeoutWithoutProcessing()
    {
        var service = CreateService(maxDepth: 4);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.RunAsync("SLOW", cts.Token);

        Assert.False(result.Ok);
        // 排队阶段取消 → 1010 排队超时（区别于 1008 处理超时）
        Assert.Equal(VisionErrorCode.QueueTimeout, result.ErrorCode);
        Assert.Equal(0, service.QueueDepth);
    }

    [Fact]
    public async Task TimeoutWhileQueued_ReturnsFast_PipelineSurvives()
    {

        var service = CreateService(maxDepth: 4, maxConcurrent: 1);

        // 阻塞任务：首次加载真实模型（约 700ms），占住唯一并发槽
        var blocker = service.RunAsync("REAL", CancellationToken.None);

        // 第二个请求排队，150ms 超时：应在排队阶段放弃并快速返回 1010 排队超时
        using var cts = new CancellationTokenSource(150);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timedOut = await service.RunAsync("REAL", cts.Token);
        sw.Stop();

        Assert.Equal(VisionErrorCode.QueueTimeout, timedOut.ErrorCode);
        Assert.True(sw.Elapsed.TotalMilliseconds < 600,
            $"排队超时应立即返回，实际 {sw.Elapsed.TotalMilliseconds}ms");

        // 阻塞任务正常完成（64x64 灰图无目标 → 1007）
        var blockerResult = await blocker;
        Assert.Equal(VisionErrorCode.NoTargetFound, blockerResult.ErrorCode);

        // 超时风暴之后，管线未被破坏，新请求正常处理（模型已缓存）
        var next = await service.RunAsync("REAL", CancellationToken.None);
        Assert.Equal(VisionErrorCode.NoTargetFound, next.ErrorCode);
        Assert.Equal(0, service.QueueDepth);
    }

    [Fact]
    public async Task QueueDepthExceeded_ReturnsBusyImmediately()
    {

        var service = CreateService(maxDepth: 2, maxConcurrent: 1);

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.RunAsync("REAL", CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // 1 个执行 + 1 个排队，其余 3 个立即 Busy
        var busy = results.Count(r => r.ErrorCode == VisionErrorCode.Busy);
        Assert.Equal(3, busy);
        Assert.All(results.Where(r => r.ErrorCode == VisionErrorCode.Busy), r => Assert.False(r.Ok));
        // 未被拒的请求均正常完成
        Assert.All(results.Where(r => r.ErrorCode != VisionErrorCode.Busy),
            r => Assert.Equal(VisionErrorCode.NoTargetFound, r.ErrorCode));
    }

    [Fact]
    public async Task FailureImage_SavedOnModelFailure()
    {
        // 模型缺失（1005）发生在取图之后：失败现场（去畸变图 + 元数据）应落盘
        var failureFolder = Path.Combine(Path.GetTempPath(), "rv_vsq_fail_" + Guid.NewGuid().ToString("N"));
        var service = CreateService(maxDepth: 4, failureFolder: failureFolder);

        try
        {
            var result = await service.RunAsync("SLOW", CancellationToken.None);
            Assert.Equal(VisionErrorCode.ModelNotAvailable, result.ErrorCode);

            // 失败留存已异步化（后台落盘），等待文件出现再断言；
            // 同进程并发类测试会挤占线程池，放宽到 15s
            // 失败留存已异步化(后台落盘),固定等待 2s 后断言(轮询曾偶发空目录误判)
            await Task.Delay(2000);
            var pngs = Directory.Exists(failureFolder) ? Directory.GetFiles(failureFolder, "*.png") : [];
            var jsons = Directory.GetFiles(failureFolder, "*.json");
            Assert.Single(pngs);
            Assert.Single(jsons);
            Assert.EndsWith("_SLOW_1005.png", Path.GetFileName(pngs[0]), StringComparison.Ordinal);
            Assert.Contains("\"Recipe\": \"SLOW\"", File.ReadAllText(jsons[0]), StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(failureFolder, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task SequentialRequests_AllComplete()
    {
        var service = CreateService(maxDepth: 4);

        var result = await service.RunAsync("SLOW", CancellationToken.None);
        Assert.Equal(VisionErrorCode.ModelNotAvailable, result.ErrorCode);

        var result2 = await service.RunAsync("SLOW", CancellationToken.None);
        Assert.Equal(VisionErrorCode.ModelNotAvailable, result2.ErrorCode);

        Assert.Equal(0, service.QueueDepth);
    }

    [Fact]
    public async Task UnknownRecipe_Returns1001_NoFailureImage()
    {
        // 配方不存在发生在取图之前：无现场可留
        var failureFolder = Path.Combine(Path.GetTempPath(), "rv_vsq_fail2_" + Guid.NewGuid().ToString("N"));
        var service = CreateService(maxDepth: 4, failureFolder: failureFolder);

        var result = await service.RunAsync("NOPE", CancellationToken.None);

        Assert.Equal(VisionErrorCode.UnknownRecipe, result.ErrorCode);
        Assert.False(Directory.Exists(failureFolder));
    }

    [Fact]
    public async Task Health_TracksRecentOutcomes()
    {
        var service = CreateService(maxDepth: 4);

        await service.RunAsync("SLOW", CancellationToken.None);   // 1005 业务失败
        await service.RunAsync("SLOW", CancellationToken.None);   // 1005 业务失败

        var (total, failed, timedOut, avgMs, p95Ms) = service.Health;
        Assert.Equal(2, total);
        Assert.Equal(2, failed);
        Assert.Equal(0, timedOut);
        Assert.True(avgMs > 0, "平均耗时应 > 0");
        Assert.True(p95Ms > 0, "P95 应 > 0");
    }

    [Fact]
    public async Task QueueDepthExceeded_RecordsBusyInHealth()
    {
        var recipes = new RecipeLoader(_recipeFolder);
        var cameras = new CameraManager();
        cameras.Register(new FileCamera("cam1", _replayFolder, intervalMs: 400));
        var calibration = new CalibrationManager();
        calibration.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 64,
            Height = 64,
            CameraMatrix = [100, 0, 32, 0, 100, 32, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });
        var failureImages = new FailureImageStore(
            new FailureImageConfig { Folder = Path.Combine(Path.GetTempPath(), "rv_nowhere") },
            NullLogger<FailureImageStore>.Instance);
        var service = new VisionService(recipes, cameras, new LightingManager(), calibration,
            new AngleStrategyFactory(new RobotVision.Infrastructure.Inference.ModelManager(Path.GetTempPath())),
            failureImages,
            NullLogger<VisionService>.Instance)
        {
            MaxQueueDepth = 1,
            MaxConcurrent = 1,
        };

        var blocker = service.RunAsync("SLOW", CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!service.IsProcessing && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.True(service.IsProcessing, "占位请求应已进入取图");

        var rejected = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => service.RunAsync("SLOW", CancellationToken.None)));
        Assert.All(rejected, r => Assert.Equal(VisionErrorCode.Busy, r.ErrorCode));

        await blocker;
        var (total, failed, timedOut, _, _) = service.Health;
        Assert.Equal(5, total);
        Assert.Equal(5, failed);
        Assert.Equal(0, timedOut);
    }
}


