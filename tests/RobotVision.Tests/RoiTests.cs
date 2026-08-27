using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// ROI 检测区域测试：配方值域校验、Clone 保留、裁剪几何（相对比例 → 像素 + 偏移量）。
/// </summary>
public class RoiTests
{
    [Fact]
    public void Validate_ValidRoi_Passes()
    {
        var recipe = new RecipeConfig
        {
            Name = "r",
            CameraId = "cam",
            Models = ["m.onnx"],
            Roi = new Roi(0.1, 0.1, 0.5, 0.5),
        };
        RecipeLoader.Validate(recipe); // 不抛
    }

    [Fact]
    public void Validate_BoundaryFloatNoise_WithinTolerance_Passes()
    {
        // X+W = 1.0000000000000002（像素↔比例往返的 ULP 级毛刺）：容差内放行，不误杀贴边 ROI
        var w = Math.BitIncrement(1.0) - 0.5;
        var recipe = new RecipeConfig
        {
            Name = "r",
            CameraId = "cam",
            Models = ["m.onnx"],
            Roi = new Roi(0.5, 0.5, w, 0.5),
        };
        RecipeLoader.Validate(recipe); // 不抛
    }

    [Theory]
    [InlineData(-0.1, 0, 0.5, 0.5)]
    [InlineData(0, 0, 1.1, 0.5)]
    [InlineData(0, 0, 0, 0.5)]
    [InlineData(0.8, 0, 0.3, 0.5)] // X+Width > 1
    [InlineData(0, 0.9, 0.5, 0.2)] // Y+Height > 1
    public void Validate_InvalidRoi_Throws(double x, double y, double w, double h)
    {
        var recipe = new RecipeConfig
        {
            Name = "r",
            CameraId = "cam",
            Models = ["m.onnx"],
            Roi = new Roi(x, y, w, h),
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Clone_PreservesRoi()
    {
        var recipe = new RecipeConfig { Roi = new Roi(0.2, 0.3, 0.4, 0.5) };
        var clone = recipe.Clone();
        Assert.Equal(recipe.Roi, clone.Roi);
    }

    [Fact]
    public void Clone_NullRoi_StaysNull()
    {
        Assert.Null(new RecipeConfig().Clone().Roi);
    }

    [Fact]
    public void Clone_PreservesTemplateRoi()
    {
        var recipe = new RecipeConfig
        {
            Template = new TemplateOptions { Roi = new Roi(0.1, 0.2, 0.3, 0.4) },
        };
        Assert.Equal(recipe.Template.Roi, recipe.Clone().Template.Roi);
    }

    [Fact]
    public void Validate_InvalidTemplateRoi_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "r",
            CameraId = "cam",
            Models = ["m.onnx"],
            Template = new TemplateOptions { Roi = new Roi(0.8, 0, 0.3, 0.5) },
        };
        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("template.roi", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Crop_RelativeToPixels_WithOffset()
    {
        // 1280×960 图，ROI = (0.25, 0.5, 0.5, 0.25)
        using var image = new Mat(960, 1280, MatType.CV_8UC3, Scalar.All(0));
        using var cropped = RoiHelper.Crop(image, new Roi(0.25, 0.5, 0.5, 0.25), out var ox, out var oy);

        Assert.Equal(320, ox);
        Assert.Equal(480, oy);
        Assert.Equal(640, cropped.Width);   // 1280 × 0.5
        Assert.Equal(240, cropped.Height);  // 960 × 0.25
    }

    [Fact]
    public void Crop_ClampsToImageBounds()
    {
        using var image = new Mat(100, 200, MatType.CV_8UC3, Scalar.All(0));
        // ROI 超出边界：X+Width > 1 → 宽度收窄到右边界
        using var cropped = RoiHelper.Crop(image, new Roi(0.6, 0, 0.6, 1), out var ox, out _);

        Assert.Equal(120, ox);
        Assert.Equal(80, cropped.Width);   // (1 - 0.6) × 200
        Assert.Equal(100, cropped.Height);
    }
}
