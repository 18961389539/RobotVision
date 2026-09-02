using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.IntegrationTests;

/// <summary>
/// VisionService 全链路集成测试（不经 TCP，直接调用管线）：
/// 配方加载/校验、错误码语义（1001/1004/1005/1014/1015/1016）、
/// OnArm 位姿校验（1012）、健康指标与配方统计。
/// </summary>
[Collection("Serial")]
public class VisionPipelineIntegrationTests
{
    [Fact]
    public async Task UnknownRecipe_Returns1001()
    {
        await using var server = await TestServer.StartAsync();

        var result = await server.Vision.RunAsync("NOPE", CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be(VisionErrorCode.UnknownRecipe);
        result.Message.Should().Contain("配方不存在");
    }

    [Fact]
    public async Task MissingStationId_WithoutPassthrough_Returns1004()
    {
        await using var server = await TestServer.StartAsync();
        // 用回放图（检出目标）→ 管线进入后处理 → PixelToRobot 缺 stationId → 1004
        // （0 目标时会先返回 1007，无法覆盖到该检查）
        server.WriteRecipe("NOSTATION", """
            {"cameraId": "cam_file", "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);

        var result = await server.Vision.RunAsync("NOSTATION", CancellationToken.None);

        result.ErrorCode.Should().Be(VisionErrorCode.NotCalibrated);
    }

    [Fact]
    public async Task DisabledRecipe_Returns1015()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("OFF", """
            {"cameraId": "cam_virtual", "enabled": false, "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);

        var result = await server.Vision.RunAsync("OFF", CancellationToken.None);

        result.ErrorCode.Should().Be(VisionErrorCode.RecipeDisabled);
    }

    [Fact]
    public async Task InvalidRecipeReferences_Returns1016()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("BADREF", """
            {"cameraId": "no_such_camera", "angleMode": "MaskMinAreaRect", "models": ["x.onnx"]}
            """);

        var result = await server.Vision.RunAsync("BADREF", CancellationToken.None);

        // 相机未注册：引用校验失败（1016）或运行时报 1002
        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().BeOneOf(VisionErrorCode.InvalidRecipeConfig, VisionErrorCode.CameraNotRegistered);
    }

    [Fact]
    public async Task OnArmStation_WithoutPose_Returns1014()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("ONARM", """
            {"cameraId": "cam_virtual", "stationId": "st_onarm", "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);
        await WriteOnArmCalibration(server);

        var result = await server.Vision.RunAsync("ONARM", pose: null, CancellationToken.None);

        result.ErrorCode.Should().Be(VisionErrorCode.PoseRequired);
    }

    [Fact]
    public async Task OnArmStation_WithMismatchedPose_Returns1012()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("ONARM", """
            {"cameraId": "cam_virtual", "stationId": "st_onarm", "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);
        await WriteOnArmCalibration(server);

        // 上报位姿与示教位姿（100, 100, 0）偏差巨大 → 1012
        var pose = new TcpClientPose(500, 500, 45);
        var result = await server.Vision.RunAsync("ONARM", pose, CancellationToken.None);

        result.ErrorCode.Should().Be(VisionErrorCode.PoseMismatch);
    }

    [Fact]
    public async Task Health_AndRecipeStats_AreRecorded()
    {
        await using var server = await TestServer.StartAsync();

        await server.Vision.RunAsync("NOPE", CancellationToken.None);
        await server.Vision.RunAsync("NOPE", CancellationToken.None);

        var (total, failed, timedOut, avgMs, p95Ms) = server.Vision.Health;
        total.Should().Be(2);
        failed.Should().Be(2);
        timedOut.Should().Be(0);
        avgMs.Should().BeGreaterThan(0);
        p95Ms.Should().BeGreaterThan(0);

        var stats = server.Vision.GetRecipeStats();
        stats.Should().ContainSingle(s => s.Recipe == "NOPE");
        stats[0].Total.Should().Be(2);
        stats[0].Failed.Should().Be(2);
    }

    [Fact]
    public async Task FrameProcessed_Subscriber_ReceivesSnapshot()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("VIRTUAL_PREVIEW", """
            {"cameraId": "cam_virtual", "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(VisionFrameSnapshot snapshot) => tcs.TrySetResult(snapshot.RecipeName);
        server.Vision.FrameProcessed += Handler;
        try
        {
            // 推理完成（0 目标 → 1007）后快照发布；首次加载模型较慢，放宽超时
            var result = await server.Vision.RunAsync("VIRTUAL_PREVIEW", CancellationToken.None);
            result.ErrorCode.Should().Be(VisionErrorCode.NoTargetFound);

            var recipe = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
            recipe.Should().Be("VIRTUAL_PREVIEW");
        }
        finally
        {
            server.Vision.FrameProcessed -= Handler;
        }
    }

    [Fact]
    public async Task RunPreview_DoesNotPublishFrameProcessed()
    {
        await using var server = await TestServer.StartAsync();
        var fired = 0;
        void Handler(VisionFrameSnapshot _) => Interlocked.Increment(ref fired);
        server.Vision.FrameProcessed += Handler;
        PreviewRunResult? preview = null;
        try
        {
            preview = await server.Vision.RunPreviewAsync(new RecipeConfig
            {
                Name = "preview",
                CameraId = "cam_virtual",
                AngleMode = AngleMode.KeyPointLine,
                Models = ["a01_kpt.onnx"],
                Keypoint = { IndexA = 0, IndexB = 1 },
            }, null, CancellationToken.None);

            preview.Result.ErrorCode.Should().Be(VisionErrorCode.NoTargetFound);
            await Task.Delay(200);
            fired.Should().Be(0);
        }
        finally
        {
            server.Vision.FrameProcessed -= Handler;
            preview!.Frame?.Dispose();
        }
    }

    [Fact]
    public async Task RunPreview_ReturnsFrame_WhenInferenceCompletes()
    {
        await using var server = await TestServer.StartAsync();
        var preview = await server.Vision.RunPreviewAsync(new RecipeConfig
        {
            Name = "preview",
            CameraId = "cam_virtual",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["a01_kpt.onnx"],
            Keypoint = { IndexA = 0, IndexB = 1 },
        }, null, CancellationToken.None);
        try
        {
            preview.Frame.Should().NotBeNull();
            preview.Frame!.UndistortedImage.Width.Should().BeGreaterThan(0);
            preview.Frame.PixelPoses.Should().NotBeNull();
        }
        finally
        {
            preview.Frame?.Dispose();
        }
    }

    /// <summary>注入 OnArm 外参档案（示教位姿 100,100,RZ=0）。</summary>
    private static async Task WriteOnArmCalibration(TestServer server)
    {
        var calibration = server.Provider.GetRequiredService<CalibrationManager>();
        calibration.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_onarm",
            CameraId = "cam_virtual",
            Affine = [1, 0, 0, 0, 1, 0],
            Rms = 0.01,
            MaxResidual = 0.02,
            MountType = CameraMountType.OnArm,
            TeachTcpX = 100,
            TeachTcpY = 100,
            TeachRzDeg = 0,
            HasTeachPose = true,
            Width = 128,
            Height = 96,
        });
        await Task.CompletedTask;
    }
}
