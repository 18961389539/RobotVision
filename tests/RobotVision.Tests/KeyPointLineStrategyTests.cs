using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 关键点连线策略（KeyPointLineStrategy）测试：
/// - 两关键点连线角度/中心计算（水平 0°、对角 45°）与置信度透传；
/// - ROI 裁剪后坐标偏移回全图（关键点/中心/框全部加偏移）；
/// - 过滤：关键点置信度不足、索引越界、基线过短（&lt;3px）均不产出位姿；
/// - 多目标按置信度降序。
/// 推理引擎为 Fake（DetectedTask=null 跳过任务校验），模型文件仅占位。
/// </summary>
public class KeyPointLineStrategyTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "rv_kpt_" + Guid.NewGuid().ToString("N"));

    private readonly FakeInferenceEngine _engine = new();
    private readonly ModelManager _manager;
    private readonly string _modelPath;

    public KeyPointLineStrategyTests()
    {
        Directory.CreateDirectory(_folder);
        _modelPath = Path.Combine(_folder, "kpt.onnx");
        File.WriteAllText(_modelPath, "fake-onnx-bytes");
        _manager = new ModelManager(_folder, new FakeInferenceEngineFactory(() => _engine));
    }

    public void Dispose()
    {
        _engine.Dispose();
        _manager.Dispose();
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private RecipeConfig MakeRecipe(double minKeypointConfidence = 0.3) => new()
    {
        Name = "kpt-recipe",
        Models = [Path.GetFileName(_modelPath)],
        Confidence = 0.25,
        Iou = 0.45,
        AngleMode = AngleMode.KeyPointLine,
        Keypoint = new KeypointOptions { IndexA = 0, IndexB = 1, MinConfidence = minKeypointConfidence },
    };

    /// <summary>造一张非空测试图（推理输入，不关心内容）。</summary>
    private static VisionImage MakeImage(int width = 100, int height = 100) =>
        VisionImage.AllocateZero(width, height, 3);

    [Fact]
    public void Compute_HorizontalKeypoints_AngleZero_CenterMidpoint()
    {
        _engine.OnPose = _ => new[]
        {
            new PoseDetectionResult(new PixelBox(0, 0, 40, 20), 0.9, "obj",
                [new KeypointDetection(10, 10, 0.9), new KeypointDetection(30, 10, 0.9)]),
        };
        var strategy = new KeyPointLineStrategy(_manager);
        using var image = MakeImage();

        var poses = strategy.Compute(image, MakeRecipe());

        var pose = Assert.Single(poses);
        Assert.Equal(20, pose.Cx, 3);   // 中点
        Assert.Equal(10, pose.Cy, 3);
        Assert.Equal(0, pose.AngleDeg, 3); // 水平向右 = 0°
        Assert.Equal(0.9, pose.Score);
        Assert.NotNull(pose.Overlay);
        Assert.Equal(2, pose.Overlay!.KeyPoints!.Count);
        Assert.Single(pose.Overlay.Boxes!);
    }

    [Fact]
    public void Compute_DiagonalKeypoints_Angle45()
    {
        _engine.OnPose = _ => new[]
        {
            new PoseDetectionResult(new PixelBox(0, 0, 20, 20), 0.8, "obj",
                [new KeypointDetection(10, 10, 0.9), new KeypointDetection(20, 20, 0.9)]),
        };
        var strategy = new KeyPointLineStrategy(_manager);
        using var image = MakeImage();

        var pose = Assert.Single(strategy.Compute(image, MakeRecipe()));
        Assert.Equal(45, pose.AngleDeg, 3);
    }

    [Fact]
    public void Compute_WithRoi_OffsetBackToFullImage()
    {
        // 图像 100×100，ROI (0.5,0.5,0.5,0.5) → 裁剪 50..100，偏移 (50,50)
        _engine.OnPose = _ => new[]
        {
            new PoseDetectionResult(new PixelBox(0, 0, 40, 20), 0.9, "obj",
                [new KeypointDetection(10, 10, 0.9), new KeypointDetection(30, 10, 0.9)]),
        };
        var recipe = MakeRecipe();
        recipe.Roi = new Roi(0.5, 0.5, 0.5, 0.5);
        var strategy = new KeyPointLineStrategy(_manager);
        using var image = MakeImage(100, 100);

        var pose = Assert.Single(strategy.Compute(image, recipe));

        // 关键点 (10,10)→(60,60)、(30,10)→(80,60)：中心 (70,60)
        Assert.Equal(70, pose.Cx, 3);
        Assert.Equal(60, pose.Cy, 3);
        Assert.Equal(0, pose.AngleDeg, 3);
        Assert.Equal(60, pose.Overlay!.KeyPoints![0].X, 3);
        Assert.Equal(60, pose.Overlay.KeyPoints[0].Y, 3);
        Assert.Equal(50, pose.Overlay.Boxes![0].X, 3); // box 左上也加偏移
        Assert.Equal(50, pose.Overlay.Boxes[0].Y, 3);
    }

    [Fact]
    public void Compute_LowKeypointConfidence_Filtered()
    {
        // IndexB 置信度 0.4 < MinConfidence 0.5 → 整条跳过
        _engine.OnPose = _ => new[]
        {
            new PoseDetectionResult(new PixelBox(0, 0, 40, 20), 0.9, "obj",
                [new KeypointDetection(10, 10, 0.9), new KeypointDetection(30, 10, 0.4)]),
        };
        var strategy = new KeyPointLineStrategy(_manager);
        using var image = MakeImage();

        Assert.Empty(strategy.Compute(image, MakeRecipe(minKeypointConfidence: 0.5)));
    }

    [Fact]
    public void Compute_KeypointIndexOutOfRange_Skipped()
    {
        // 关键点只有 1 个，但配方要求 IndexB=1 → 越界跳过
        _engine.OnPose = _ => new[]
        {
            new PoseDetectionResult(new PixelBox(0, 0, 10, 10), 0.9, "obj",
                [new KeypointDetection(10, 10, 0.9)]),
        };
        var strategy = new KeyPointLineStrategy(_manager);
        using var image = MakeImage();

        Assert.Empty(strategy.Compute(image, MakeRecipe()));
    }

    [Fact]
    public void Compute_ShortBaseline_Skipped()
    {
        // 两点距离 2px < MinBaselinePx(3) → 基线过短，角度不可靠，跳过
        _engine.OnPose = _ => new[]
        {
            new PoseDetectionResult(new PixelBox(0, 0, 10, 10), 0.9, "obj",
                [new KeypointDetection(10, 10, 0.9), new KeypointDetection(12, 10, 0.9)]),
        };
        var strategy = new KeyPointLineStrategy(_manager);
        using var image = MakeImage();

        Assert.Empty(strategy.Compute(image, MakeRecipe()));
    }

    [Fact]
    public void Compute_MultipleResults_OrderedByScoreDesc()
    {
        _engine.OnPose = _ => new[]
        {
            new PoseDetectionResult(new PixelBox(0, 0, 40, 20), 0.5, "low",
                [new KeypointDetection(10, 10, 0.9), new KeypointDetection(30, 10, 0.9)]),
            new PoseDetectionResult(new PixelBox(0, 30, 40, 20), 0.95, "high",
                [new KeypointDetection(10, 40, 0.9), new KeypointDetection(30, 40, 0.9)]),
        };
        var strategy = new KeyPointLineStrategy(_manager);
        using var image = MakeImage();

        var poses = strategy.Compute(image, MakeRecipe());
        Assert.Equal(2, poses.Count);
        Assert.Equal(0.95, poses[0].Score);
        Assert.Equal(0.5, poses[1].Score);
    }
}
