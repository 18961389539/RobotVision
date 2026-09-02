using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 最小外接矩形长边策略（MaskMinAreaRectStrategy）测试：
/// - 掩码轮廓长轴方向：水平矩形 0°、竖直矩形 90°、中心 = 长条中心；
/// - ContourLocal（相对包围盒）还原到全图坐标（加 box 原点），ROI 再加裁剪偏移；
/// - 过滤：轮廓点 &lt;4、包围盒面积 &lt;400px² 不产出位姿；
/// - 多目标按置信度降序。
/// </summary>
public class MaskMinAreaRectStrategyTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "rv_mmar_" + Guid.NewGuid().ToString("N"));

    private readonly FakeInferenceEngine _engine = new();
    private readonly ModelManager _manager;
    private readonly string _modelPath;

    public MaskMinAreaRectStrategyTests()
    {
        Directory.CreateDirectory(_folder);
        _modelPath = Path.Combine(_folder, "seg.onnx");
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

    private RecipeConfig MakeRecipe() => new()
    {
        Name = "seg-recipe",
        Models = [Path.GetFileName(_modelPath)],
        Confidence = 0.25,
        Iou = 0.45,
        AngleMode = AngleMode.MaskMinAreaRect,
        Segmentation = new SegmentationOptions { PixelConfidence = 0.5 },
    };

    private static VisionImage MakeImage(int width = 100, int height = 100) =>
        VisionImage.AllocateZero(width, height, 3);

    /// <summary>横条掩码：包围盒 (0,0,40,10)，局部轮廓 = 矩形四角。</summary>
    private static InstanceSegmentation HorizontalBar() => new(
        new PixelBox(0, 0, 40, 10), 0.9, "part",
        [new ImagePoint(0, 0), new ImagePoint(40, 0), new ImagePoint(40, 10), new ImagePoint(0, 10)],
        []);

    /// <summary>竖条掩码：包围盒 (0,0,10,40)。</summary>
    private static InstanceSegmentation VerticalBar() => new(
        new PixelBox(0, 0, 10, 40), 0.85, "part",
        [new ImagePoint(0, 0), new ImagePoint(10, 0), new ImagePoint(10, 40), new ImagePoint(0, 40)],
        []);

    [Fact]
    public void Compute_HorizontalBar_AngleZero_CenterMatches()
    {
        _engine.OnSegmentation = _ => new[] { HorizontalBar() };
        var strategy = new MaskMinAreaRectStrategy(_manager);
        using var image = MakeImage();

        var pose = Assert.Single(strategy.Compute(image, MakeRecipe()));
        Assert.Equal(20, pose.Cx, 1);
        Assert.Equal(5, pose.Cy, 1);
        Assert.Equal(0, pose.AngleDeg, 1); // 长边水平 → [0,180) 内为 0°
        Assert.Equal(0.9, pose.Score);
        Assert.NotNull(pose.Overlay);
        Assert.Equal(4, pose.Overlay!.Contour!.Count);
    }

    [Fact]
    public void Compute_VerticalBar_Angle90()
    {
        _engine.OnSegmentation = _ => new[] { VerticalBar() };
        var strategy = new MaskMinAreaRectStrategy(_manager);
        using var image = MakeImage();

        var pose = Assert.Single(strategy.Compute(image, MakeRecipe()));
        Assert.Equal(90, pose.AngleDeg, 1); // 长边竖直 → 90°
    }

    [Fact]
    public void Compute_BoxOffset_ContourTranslatedToFullImage()
    {
        // 包围盒原点 (5,5)：局部轮廓 (0,0)..(40,10) → 全图 (5,5)..(45,15)
        _engine.OnSegmentation = _ => new[]
        {
            new InstanceSegmentation(new PixelBox(5, 5, 40, 10), 0.9, "part",
                [new ImagePoint(0, 0), new ImagePoint(40, 0), new ImagePoint(40, 10), new ImagePoint(0, 10)],
                []),
        };
        var strategy = new MaskMinAreaRectStrategy(_manager);
        using var image = MakeImage();

        var pose = Assert.Single(strategy.Compute(image, MakeRecipe()));
        Assert.Equal(25, pose.Cx, 1); // 中心 (5+20, 5+5)
        Assert.Equal(10, pose.Cy, 1);
        Assert.Equal(5, pose.Overlay!.Contour![0].X, 3);
        Assert.Equal(5, pose.Overlay.Contour[0].Y, 3);
        Assert.Equal(45, pose.Overlay.Contour[2].X, 3);
    }

    [Fact]
    public void Compute_WithRoi_OffsetBackToFullImage()
    {
        // 图像 100×100，ROI (0.25,0.25,0.5,0.5) → 裁剪 25..75，偏移 (25,25)
        _engine.OnSegmentation = _ => new[] { HorizontalBar() };
        var recipe = MakeRecipe();
        recipe.Roi = new Roi(0.25, 0.25, 0.5, 0.5);
        var strategy = new MaskMinAreaRectStrategy(_manager);
        using var image = MakeImage(100, 100);

        var pose = Assert.Single(strategy.Compute(image, recipe));
        Assert.Equal(45, pose.Cx, 1); // 20+25
        Assert.Equal(30, pose.Cy, 1); // 5+25
    }

    [Fact]
    public void Compute_FewerThanFourContourPoints_Skipped()
    {
        _engine.OnSegmentation = _ => new[]
        {
            new InstanceSegmentation(new PixelBox(0, 0, 40, 10), 0.9, "part",
                [new ImagePoint(0, 0), new ImagePoint(40, 0), new ImagePoint(40, 10)], []),
        };
        var strategy = new MaskMinAreaRectStrategy(_manager);
        using var image = MakeImage();

        Assert.Empty(strategy.Compute(image, MakeRecipe()));
    }

    [Fact]
    public void Compute_SmallBoxArea_Skipped()
    {
        // 包围盒 10×10 = 100px² < MinMaskAreaPx(400) → 碎目标不产出位姿
        _engine.OnSegmentation = _ => new[]
        {
            new InstanceSegmentation(new PixelBox(0, 0, 10, 10), 0.9, "part",
                [new ImagePoint(0, 0), new ImagePoint(10, 0), new ImagePoint(10, 10), new ImagePoint(0, 10)], []),
        };
        var strategy = new MaskMinAreaRectStrategy(_manager);
        using var image = MakeImage();

        Assert.Empty(strategy.Compute(image, MakeRecipe()));
    }

    [Fact]
    public void Compute_MultipleResults_OrderedByScoreDesc()
    {
        _engine.OnSegmentation = _ => new[]
        {
            new InstanceSegmentation(new PixelBox(0, 30, 40, 10), 0.5, "low",
                [new ImagePoint(0, 30), new ImagePoint(40, 30), new ImagePoint(40, 40), new ImagePoint(0, 40)], []),
            new InstanceSegmentation(new PixelBox(0, 0, 40, 10), 0.95, "high",
                [new ImagePoint(0, 0), new ImagePoint(40, 0), new ImagePoint(40, 10), new ImagePoint(0, 10)], []),
        };
        var strategy = new MaskMinAreaRectStrategy(_manager);
        using var image = MakeImage();

        var poses = strategy.Compute(image, MakeRecipe());
        Assert.Equal(2, poses.Count);
        Assert.Equal(0.95, poses[0].Score);
        Assert.Equal(0.5, poses[1].Score);
    }
}
