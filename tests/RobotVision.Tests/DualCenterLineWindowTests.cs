using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Core.Models;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 双模型窗口配对（CropWindowPairing）单元测试：窗口外扩几何与窗内最近邻选择为纯函数，
/// 直接验证边界裁剪、距离门槛与偏移换算；推理路径需真实模型，不在单测覆盖。
/// </summary>
public class DualCenterLineWindowTests
{
    [Fact]
    public void ExpandWindow_ExpandsAllSides()
    {
        // box 40×20，ratio=1 → 四边各外扩 40/20
        var w = DualCenterLineStrategy.ExpandWindow(
            new PixelBox(100, 100, 40, 20), 1.0, 1000, 1000);

        Assert.Equal(new PixelBox(60, 80, 120, 60), w);
    }

    [Fact]
    public void ExpandWindow_ClipsAtImageBorder()
    {
        var w = DualCenterLineStrategy.ExpandWindow(
            new PixelBox(0, 0, 40, 40), 1.0, 100, 100);

        Assert.Equal(0, w.Left);
        Assert.Equal(0, w.Top);
        Assert.Equal(80, w.Right);
        Assert.Equal(80, w.Bottom);
    }

    [Fact]
    public void ExpandWindow_ExtremeRatio_FillsWholeImage()
    {
        // box 20×20，ratio=5 → 四边各外扩 100，左右缘同时触界
        var w = DualCenterLineStrategy.ExpandWindow(
            new PixelBox(40, 40, 20, 20), 5.0, 100, 100);

        Assert.Equal(new PixelBox(0, 0, 100, 100), w);
    }

    [Fact]
    public void SelectNearest_PicksClosestToCenterA()
    {
        // A 中心 (100,100)；两个候选：近 (110,100) 远 (160,100)
        var boxes = new[]
        {
            new PixelBox(155, 95, 10, 10),
            new PixelBox(105, 95, 10, 10),
        };
        var index = DualCenterLineStrategy.SelectNearest(
            new ImagePoint(100, 100), boxes, 0, 0, 800);

        Assert.Equal(1, index);
    }

    [Fact]
    public void SelectNearest_AppliesWindowOffset()
    {
        // 窗口左上角 (200,50)：候选中心为窗内 (10,10) → 全局 (210,60)
        var boxes = new[] { new PixelBox(5, 5, 10, 10) };
        var index = DualCenterLineStrategy.SelectNearest(
            new ImagePoint(210, 60), boxes, 200, 50, 800);

        Assert.Equal(0, index);
    }

    [Fact]
    public void SelectNearest_BeyondMaxDistance_ReturnsMinusOne()
    {
        var boxes = new[] { new PixelBox(500, 500, 20, 20) };
        var index = DualCenterLineStrategy.SelectNearest(
            new ImagePoint(100, 100), boxes, 0, 0, 50);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void SelectNearest_EmptyCandidates_ReturnsMinusOne()
    {
        var index = DualCenterLineStrategy.SelectNearest(
            new ImagePoint(100, 100), [], 0, 0, 800);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void Validate_WindowPairing_RejectsBadExpandRatio()
    {
        var recipe = new RecipeConfig
        {
            Name = "t",
            CameraId = "cam",
            AngleMode = AngleMode.DualCenterLine,
            Models = ["a.onnx", "b.onnx"],
            DualModel = new DualModelOptions { CropWindowPairing = true, CropExpandRatio = 0 },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_WindowPairingOff_SkipsExpandRatioCheck()
    {
        // 旧配方无 CropExpandRatio 字段语义（默认 1.0）；关闭窗口配对时不校验该值
        var recipe = new RecipeConfig
        {
            Name = "t",
            CameraId = "cam",
            AngleMode = AngleMode.DualCenterLine,
            Models = ["a.onnx", "b.onnx"],
            DualModel = new DualModelOptions { CropWindowPairing = false, CropExpandRatio = 99 },
        };
        RecipeLoader.Validate(recipe); // 不抛异常即通过
    }

    [Fact]
    public void Clone_CarriesWindowPairingFields()
    {
        var recipe = new RecipeConfig
        {
            Name = "t",
            DualModel = new DualModelOptions { CropWindowPairing = true, CropExpandRatio = 2.5 },
        };
        var clone = recipe.Clone();

        Assert.True(clone.DualModel.CropWindowPairing);
        Assert.Equal(2.5, clone.DualModel.CropExpandRatio);
    }
}
