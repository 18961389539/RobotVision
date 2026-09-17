using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// TRIGGER 失败自动重拍测试：
/// - RetryPolicy 配置规范化（钳制/错误码门控/开关）；
/// - 重拍成功（第 1 次抖动失败 → 第 2 次成功，一次 TRIGGER 只记一次结果）；
/// - 全部失败（耗满 MaxAttempts，失败留存只在最终尝试落盘）；
/// - 不可重试码 / 关闭开关 / 试触发 → 不重拍。
/// </summary>
[Collection("Serial")]
public class RetryPolicyTests : IDisposable
{
    private readonly string _recipeFolder = Path.Combine(Path.GetTempPath(), "rv_retry_" + Guid.NewGuid().ToString("N"));
    private readonly string _replayFolder = Path.Combine(Path.GetTempPath(), "rv_retry_img_" + Guid.NewGuid().ToString("N"));
    private readonly string _modelFolder = Path.Combine(Path.GetTempPath(), "rv_retry_models_" + Guid.NewGuid().ToString("N"));

    public RetryPolicyTests()
    {
        Directory.CreateDirectory(_recipeFolder);
        Directory.CreateDirectory(_replayFolder);
        Directory.CreateDirectory(_modelFolder);
        using (var img = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(100)))
            OpenCvSharp.Cv2.ImWrite(Path.Combine(_replayFolder, "f.bmp"), img);
        File.WriteAllBytes(Path.Combine(_modelFolder, "hit_seg.onnx"), [1, 2, 3, 4]);

        // 比例标定工位（无内参也能跑通完整映射）：重拍后第二次成功需要输出机器人坐标
        File.WriteAllText(Path.Combine(_recipeFolder, "HIT_RETRY.json"), """
            {
              "cameraId": "cam1",
              "stationId": "st_scale",
              "angleMode": "MaskMinAreaRect",
              "models": [ "hit_seg.onnx" ],
              "confidence": 0.25
            }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_recipeFolder, true); } catch (IOException) { }
        try { Directory.Delete(_replayFolder, true); } catch (IOException) { }
        try { Directory.Delete(_modelFolder, true); } catch (IOException) { }
    }

    /// <summary>分割调用计数器（引擎可能被缓存复用，闭包共享计数保证跨尝试累计）。</summary>
    private sealed class RetryCounter
    {
        public int Calls;
    }

    private (VisionService Service, RetryCounter Counter) CreateService(
        RetryConfig? retry = null,
        Func<int, IReadOnlyList<InstanceSegmentation>>? onSegmentation = null,
        string? failureFolder = null)
    {
        var recipes = new RecipeLoader(_recipeFolder);
        var cameras = new CameraManager();
        cameras.Register(new FileCamera("cam1", _replayFolder));

        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st_scale",
            CameraId = "cam1",
            ScaleX = 0.05,
            ScaleY = 0.05,
            Width = 64,
            Height = 64,
        });

        var failureImages = new FailureImageStore(
            new FailureImageConfig { Folder = failureFolder ?? Path.Combine(Path.GetTempPath(), "rv_nowhere") },
            NullLogger<FailureImageStore>.Instance);

        var counter = new RetryCounter();
        var engineFactory = new FakeInferenceEngineFactory(() =>
        {
            var engine = new FakeInferenceEngine
            {
                OnSegmentation = image =>
                {
                    // ModelManager 加载后空跑一帧 640×640 预热（真实回放图 64×64）：不计数
                    if (image.Width != 640)
                        counter.Calls++;
                    return onSegmentation?.Invoke(counter.Calls) ?? [];
                },
            };
            return engine;
        });
        var models = new ModelManager(_modelFolder, engineFactory);

        var service = new VisionService(recipes, cameras, new LightingManager(), calibration,
            new AngleStrategyFactory(models),
            failureImages,
            NullLogger<VisionService>.Instance,
            retry: retry is null ? null : new RetryPolicy(retry));
        return (service, counter);
    }

    private static InstanceSegmentation HitBox() => new(
        new PixelBox(0, 0, 40, 10), 0.9, "part",
        [new ImagePoint(0, 0), new ImagePoint(40, 0), new ImagePoint(40, 10), new ImagePoint(0, 10)],
        []);

    // ---- RetryPolicy 纯单元 ----

    [Fact]
    public void RetryPolicy_NormalizesConfig()
    {
        var p = new RetryPolicy(new RetryConfig
        {
            Enabled = true,
            MaxAttempts = 9,
            DelayMs = 99_999,
            ErrorCodes = [(int)VisionErrorCode.RefineFailed],
        });
        Assert.Equal(5, p.MaxAttempts);   // 钳制上限
        Assert.Equal(5000, p.DelayMs);    // 钳制上限
        Assert.True(p.IsRetryable(VisionErrorCode.RefineFailed));
        Assert.False(p.IsRetryable(VisionErrorCode.NoTargetFound));
        Assert.False(p.IsRetryable(VisionErrorCode.ModelNotAvailable));

        var clamped = new RetryPolicy(new RetryConfig { MaxAttempts = 0, DelayMs = -1 });
        Assert.Equal(1, clamped.MaxAttempts);
        Assert.Equal(0, clamped.DelayMs);
    }

    [Fact]
    public void RetryPolicy_DefaultCodes_IncludeRefineFailedAndNoTarget()
    {
        var p = new RetryPolicy(new RetryConfig());
        Assert.True(p.IsRetryable(VisionErrorCode.RefineFailed));
        Assert.True(p.IsRetryable(VisionErrorCode.NoTargetFound));
        Assert.False(p.IsRetryable(VisionErrorCode.ModelNotAvailable));
        Assert.False(p.IsRetryable(VisionErrorCode.NotCalibrated));
    }

    [Fact]
    public void RetryPolicy_Disabled_RejectsAllCodes()
    {
        var p = new RetryPolicy(new RetryConfig
        {
            Enabled = false,
            ErrorCodes = [(int)VisionErrorCode.NoTargetFound, (int)VisionErrorCode.RefineFailed],
        });
        Assert.False(p.IsRetryable(VisionErrorCode.NoTargetFound));
        Assert.False(p.IsRetryable(VisionErrorCode.RefineFailed));
    }

    [Fact]
    public void RetryPolicy_ApplyConfig_HotSwaps()
    {
        var p = new RetryPolicy(new RetryConfig { Enabled = true, MaxAttempts = 3, DelayMs = 200 });
        Assert.True(p.IsRetryable(VisionErrorCode.NoTargetFound));
        Assert.Equal(3, p.MaxAttempts);

        p.ApplyConfig(new RetryConfig { Enabled = false, MaxAttempts = 2, DelayMs = 0 });
        Assert.False(p.IsRetryable(VisionErrorCode.NoTargetFound));
        Assert.Equal(2, p.MaxAttempts);
        Assert.Equal(0, p.DelayMs);
    }

    // ---- 管线集成：重拍循环 ----

    [Fact]
    public async Task Retry_FirstFailsThenSucceeds_ReturnsOk_RecordsOnce()
    {
        var (service, counter) = CreateService(
            retry: new RetryConfig { MaxAttempts = 3, DelayMs = 0 },
            onSegmentation: calls => calls >= 2 ? [HitBox()] : []);

        var result = await service.RunAsync("HIT_RETRY", CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(2, counter.Calls); // 第 1 次 1007 → 重拍第 2 次成功，未耗满 3 次
        var p = Assert.Single(result.Poses);
        Assert.Equal(1, p.X, 1);    // 20px * 0.05
        Assert.Equal(0.25, p.Y, 1); // 5px * 0.05
        var (total, failed, _, _, _) = service.Health;
        Assert.Equal(1, total);     // 一次 TRIGGER 只记一次结果
        Assert.Equal(0, failed);
    }

    [Fact]
    public async Task Retry_AllAttemptsFail_ReturnsFailure_SingleCaptureWithPoseCount()
    {
        var failureFolder = Path.Combine(Path.GetTempPath(), "rv_retry_fail_" + Guid.NewGuid().ToString("N"));
        var (service, counter) = CreateService(
            retry: new RetryConfig { MaxAttempts = 3, DelayMs = 0 },
            failureFolder: failureFolder);

        try
        {
            var result = await service.RunAsync("HIT_RETRY", CancellationToken.None);

            Assert.False(result.Ok);
            Assert.Equal(VisionErrorCode.NoTargetFound, result.ErrorCode);
            Assert.Equal(3, counter.Calls); // 耗满 3 次
            var (total, failed, _, _, _) = service.Health;
            Assert.Equal(1, total);
            Assert.Equal(1, failed);

            // 失败留存只在最终尝试落盘（中间失败不刷留存）
            TestWait.Until(
                () => Directory.Exists(failureFolder) &&
                      Directory.GetFiles(failureFolder, "*.png").Length == 1 &&
                      Directory.GetFiles(failureFolder, "*.json").Length == 1,
                TimeSpan.FromSeconds(5), description: "等待最终失败留存落盘");
            var text = File.ReadAllText(Directory.GetFiles(failureFolder, "*.json").Single());
            Assert.Contains("\"PixelPoseCount\": 0", text, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(failureFolder, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Retry_NonRetryableCode_NoRetry()
    {
        // 只把 1019 列为可重试：1007 不在列表 → 不重拍
        var (service, counter) = CreateService(
            retry: new RetryConfig { MaxAttempts = 3, ErrorCodes = [(int)VisionErrorCode.RefineFailed] });

        var result = await service.RunAsync("HIT_RETRY", CancellationToken.None);

        Assert.Equal(VisionErrorCode.NoTargetFound, result.ErrorCode);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Retry_Disabled_NoRetry()
    {
        var (service, counter) = CreateService(
            retry: new RetryConfig { Enabled = false, MaxAttempts = 3, DelayMs = 0 });

        var result = await service.RunAsync("HIT_RETRY", CancellationToken.None);

        Assert.Equal(VisionErrorCode.NoTargetFound, result.ErrorCode);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task ApplyRetry_HotSwapsPolicy_WithoutRestart()
    {
        var (service, counter) = CreateService(
            retry: new RetryConfig { MaxAttempts = 3, DelayMs = 0 });

        var before = await service.RunAsync("HIT_RETRY", CancellationToken.None);
        Assert.Equal(VisionErrorCode.NoTargetFound, before.ErrorCode);
        Assert.Equal(3, counter.Calls); // 初始策略重拍 3 次

        // 设置页保存后热切换：关闭重拍立即生效，无需重启
        service.ApplyRetry(new RetryConfig { Enabled = false, MaxAttempts = 3, DelayMs = 0 });
        counter.Calls = 0;
        var after = await service.RunAsync("HIT_RETRY", CancellationToken.None);
        Assert.Equal(VisionErrorCode.NoTargetFound, after.ErrorCode);
        Assert.Equal(1, counter.Calls); // 热切换后不再重拍
    }

    [Fact]
    public async Task Preview_DoesNotRetry()
    {
        var (service, counter) = CreateService(
            retry: new RetryConfig { MaxAttempts = 3, DelayMs = 0 });

        var recipe = new RecipeConfig
        {
            Name = "HIT_RETRY",
            CameraId = "cam1",
            StationId = "st_scale",
            Models = ["hit_seg.onnx"],
            Confidence = 0.25,
            Iou = 0.45,
            AngleMode = AngleMode.MaskMinAreaRect,
        };
        var result = await service.RunPreviewAsync(recipe, null, CancellationToken.None);

        Assert.Equal(VisionErrorCode.NoTargetFound, result.Result.ErrorCode);
        Assert.Equal(1, counter.Calls); // 试触发不重拍
    }
}
