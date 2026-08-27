using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Hosting;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 比例标定（手动录入）管线级测试：
/// - GetMappingMode 优先级（多项式 &gt; 外参 &gt; 比例）；
/// - PixelToRobotScale 位置/角度映射（等比与各向异性）与档案校验；
/// - 分辨率锁（换分辨率后 mm/px 失效拒绝静默使用）；
/// - VisionService 分发：比例工位跳过内参去畸变（无内参也能推理）、外参并存时外参优先。
/// </summary>
public class ScaleCalibrationTests : IDisposable
{
    private readonly string _recipeFolder = Path.Combine(Path.GetTempPath(), "rv_scale_" + Guid.NewGuid().ToString("N"));
    private readonly string _replayFolder = Path.Combine(Path.GetTempPath(), "rv_scale_img_" + Guid.NewGuid().ToString("N"));

    public ScaleCalibrationTests()
    {
        Directory.CreateDirectory(_recipeFolder);
        Directory.CreateDirectory(_replayFolder);
        using (var img = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(100)))
            OpenCvSharp.Cv2.ImWrite(Path.Combine(_replayFolder, "f.bmp"), img);

        // 模型缺失配方：取图/分发正常时推理报 1005（ModelNotAvailable）——
        // 用"到达哪一步失败"区分管线走了哪条路径
        File.WriteAllText(Path.Combine(_recipeFolder, "SCALE_ONLY.json"), """
            {
              "cameraId": "cam1",
              "stationId": "st_scale",
              "angleMode": "MaskMinAreaRect",
              "models": [ "no_such_model.onnx" ]
            }
            """);
        File.WriteAllText(Path.Combine(_recipeFolder, "EXT_AND_SCALE.json"), """
            {
              "cameraId": "cam1",
              "stationId": "st_both",
              "angleMode": "MaskMinAreaRect",
              "models": [ "no_such_model.onnx" ]
            }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_recipeFolder, true); } catch (IOException) { }
        try { Directory.Delete(_replayFolder, true); } catch (IOException) { }
    }

    private static ScaleProfile Scale(string stationId = "st_scale", double kx = 0.05, double ky = 0.05,
        int width = 0, int height = 0, string cameraId = "cam1") => new()
    {
        StationId = stationId,
        CameraId = cameraId,
        ScaleX = kx,
        ScaleY = ky,
        Width = width,
        Height = height,
    };

    // ---- GetMappingMode 优先级 ----

    [Fact]
    public void GetMappingMode_ResolvesPriority_PolynomialOverExtrinsicOverScale()
    {
        var manager = new CalibrationManager();
        manager.LoadPolynomial(new PolynomialProfile
        {
            StationId = "st_all", CameraId = "cam1", Width = 64, Height = 64, Order = 2,
            CoefX = [1, 0, 0, 0, 0, 0], CoefY = [0, 1, 0, 0, 0, 0],
        });
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_all", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        manager.LoadScale(Scale("st_all"));

        manager.GetMappingMode("st_all").Should().Be(StationMappingMode.Polynomial);
        manager.GetMappingMode(null).Should().Be(StationMappingMode.None);
        manager.GetMappingMode("").Should().Be(StationMappingMode.None);
        manager.GetMappingMode("nope").Should().Be(StationMappingMode.None);
    }

    [Fact]
    public void GetMappingMode_ExtrinsicWinsOverScale()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_both", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        manager.LoadScale(Scale("st_both"));

        manager.GetMappingMode("st_both").Should().Be(StationMappingMode.Extrinsic);
        manager.QualityWarnings.Should().Contain(w => w.Contains("比例") && w.Contains("外参/多项式"));
    }

    [Fact]
    public void GetMappingMode_ScaleOnlyStation()
    {
        var manager = new CalibrationManager();
        manager.LoadScale(Scale("st_scale"));

        manager.GetMappingMode("st_scale").Should().Be(StationMappingMode.Scale);
    }

    // ---- PixelToRobotScale 映射 ----

    [Fact]
    public void PixelToRobotScale_MapsPosition_IsotropicKeepsAngle()
    {
        var manager = new CalibrationManager();
        manager.LoadScale(Scale(kx: 0.05, ky: 0.05));

        var pose = manager.PixelToRobotScale("st_scale", new PixelPose(1000, 2000, 30, 1));

        pose.X.Should().BeApproximately(50.0, 1e-9);
        pose.Y.Should().BeApproximately(100.0, 1e-9);
        pose.AngleDeg.Should().BeApproximately(30.0, 1e-9); // 等比缩放不改变方向
    }

    [Fact]
    public void PixelToRobotScale_Anisotropic_RecomputesAngle()
    {
        var manager = new CalibrationManager();
        manager.LoadScale(Scale(kx: 0.05, ky: 0.1)); // Y 方向比例是 X 的 2 倍

        var pose = manager.PixelToRobotScale("st_scale", new PixelPose(100, 100, 45, 1));

        pose.X.Should().BeApproximately(5.0, 1e-9);
        pose.Y.Should().BeApproximately(10.0, 1e-9);
        // 方向向量 (cos45°, sin45°) 经 (kx,ky) 缩放后：atan2(0.1·sin45°, 0.05·cos45°) = 63.435°
        pose.AngleDeg.Should().BeApproximately(63.4349, 0.001);
    }

    [Fact]
    public void PixelToRobotScale_CameraMismatch_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadScale(Scale());

        var ex = Assert.Throws<RobotVision.Core.VisionException>(() =>
            manager.PixelToRobotScale("st_scale", new PixelPose(1, 1, 0, 1), cameraId: "cam_other"));

        ex.Message.Should().Contain("不一致");
    }

    [Fact]
    public void PixelToRobotScale_MissingProfile_ThrowsNotCalibrated()
    {
        var manager = new CalibrationManager();

        var ex = Assert.Throws<RobotVision.Core.VisionException>(() =>
            manager.PixelToRobotScale("nope", new PixelPose(1, 1, 0, 1)));

        ex.ErrorCode.Should().Be(VisionErrorCode.NotCalibrated);
    }

    // ---- 分辨率锁 ----

    [Fact]
    public void VerifyScaleResolution_Mismatch_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadScale(Scale(width: 2448, height: 2048));

        var ex = Assert.Throws<RobotVision.Core.VisionException>(() =>
            manager.VerifyScaleResolution("st_scale", 1280, 960));

        ex.Message.Should().Contain("2448x2048");
    }

    [Fact]
    public void VerifyScaleResolution_MatchAndUnrecorded_Pass()
    {
        var manager = new CalibrationManager();
        manager.LoadScale(Scale(width: 1280, height: 960));
        manager.VerifyScaleResolution("st_scale", 1280, 960); // 一致：不抛

        manager.LoadScale(Scale(stationId: "st_nores", width: 0, height: 0));
        manager.VerifyScaleResolution("st_nores", 1280, 960); // 未记录：跳过
    }

    // ---- VisionService 管线分发 ----

    private VisionService CreateService(CalibrationManager calibration)
    {
        var recipes = new RecipeLoader(_recipeFolder);
        var cameras = new CameraManager();
        cameras.Register(new FileCamera("cam1", _replayFolder));

        var failureImages = new FailureImageStore(
            new FailureImageConfig { Folder = Path.Combine(Path.GetTempPath(), "rv_nowhere") },
            NullLogger<FailureImageStore>.Instance);

        return new VisionService(recipes, cameras, new LightingManager(), calibration,
            new AngleStrategyFactory(new RobotVision.Infrastructure.Inference.ModelManager(Path.GetTempPath())),
            failureImages,
            NullLogger<VisionService>.Instance);
    }

    [Fact]
    public async Task ScaleOnlyStation_SkipsUndistort_ReachesInferenceWithoutIntrinsic()
    {
        // 相机无内参档案：若分发错误走入参去畸变路径，会在 Undistort 抛 1004；
        // 比例模式跳过去畸变 → 推理直达模型加载失败 1005
        var calibration = new CalibrationManager();
        calibration.LoadScale(Scale(width: 64, height: 64)); // 分辨率与 64x64 回放图一致

        var service = CreateService(calibration);
        var result = await service.RunAsync("SCALE_ONLY", CancellationToken.None);

        result.ErrorCode.Should().Be(VisionErrorCode.ModelNotAvailable,
            "比例工位应跳过内参去畸变，无内参档案也能推理");
    }

    [Fact]
    public async Task ScaleOnlyStation_ResolutionMismatch_Rejected()
    {
        var calibration = new CalibrationManager();
        calibration.LoadScale(Scale(width: 2448, height: 2048)); // 与 64x64 回放图不一致

        var service = CreateService(calibration);
        var result = await service.RunAsync("SCALE_ONLY", CancellationToken.None);

        result.ErrorCode.Should().Be(VisionErrorCode.NotCalibrated);
        result.Message.Should().Contain("分辨率");
    }

    [Fact]
    public async Task ExtrinsicAndScaleCoexist_ExtrinsicPathWins()
    {
        // 外参 + 比例并存且无内参：走外参路径 → Undistort 报 1004（证明比例未抢管管线）
        var calibration = new CalibrationManager();
        calibration.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_both", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        calibration.LoadScale(Scale(stationId: "st_both", width: 64, height: 64));

        var service = CreateService(calibration);
        var result = await service.RunAsync("EXT_AND_SCALE", CancellationToken.None);

        result.ErrorCode.Should().Be(VisionErrorCode.NotCalibrated,
            "外参路径需要内参去畸变，应先于比例映射失败");
    }

    [Fact]
    public void FormatStageMs_Scale_UsesCloneLabelAndStageDurations()
    {
        // 累计戳 24/25/98/99 → 阶段 24 / 克隆1 / 推理73 / 后处理1（用户截图那种「去畸变 25」是累计误标）
        VisionService.FormatStageMs(StationMappingMode.Scale, 24, 25, 98, 99)
            .Should().Be("取图 24 · 克隆 1 · 推理 73 · 后处理 1");
    }

    [Fact]
    public void FormatStageMs_Extrinsic_KeepsUndistortLabel()
    {
        VisionService.FormatStageMs(StationMappingMode.Extrinsic, 24, 32, 90, 91)
            .Should().Be("取图 24 · 去畸变 8 · 推理 58 · 后处理 1");
    }

    [Fact]
    public void FormatStageMs_OmitsSubMillisecondBreakdown()
    {
        VisionService.FormatStageMs(new PipelineStageMs
        {
            Mode = StationMappingMode.Scale,
            GrabMs = 24,
            PrepMs = 1,
            InferMs = 73,
            PostMs = 1,
            RecipeMs = 0.2,
            ConvertMs = 0.4,
            RefineMs = 0.1,
        }).Should().Be("取图 24 · 克隆 1 · 推理 73 · 后处理 1");
    }

    [Fact]
    public void FormatStageMs_GrabAndInferBreakdown_InParentheses()
    {
        VisionService.FormatStageMs(new PipelineStageMs
        {
            Mode = StationMappingMode.Scale,
            GrabMs = 642,
            PrepMs = 9,
            InferMs = 350,
            PostMs = 9,
            RecipeMs = 3,
            LightOnMs = 8,
            StabilizeMs = 50,
            GateWaitMs = 180,
            AcquireMs = 390,
            ConvertMs = 11,
            SegmentMs = 280,
            RefineMs = 70,
        }).Should().Be(
            "取图 642（配方 3 · 点亮 8 · 稳定 50 · 等锁 180 · 采集 390 · 转图 11） · 克隆 9 · 推理 350（分割 280 · 精修 70） · 后处理 9");
    }
}
