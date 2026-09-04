using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 双模板连线（DualTemplateCenterLine）：合成两块可区分贴标，
/// 验证模板1 定位、模板1→模板2 连线角度、双 ROI 互斥与校验。
/// </summary>
public class DualTemplateCenterLineTests
{
    private const int W = 400;
    private const int H = 300;

    private static readonly Roi LeftRoi = new(0.12, 0.30, 0.28, 0.40);
    private static readonly Roi RightRoi = new(0.45, 0.30, 0.35, 0.40);

    private static RecipeConfig Recipe(Action<DualTemplateOptions>? tweak = null, Roi? roi = null)
    {
        using var scene = PaintScene();
        using var tplA = Crop(scene, 80, 136, 40, 28);
        using var tplB = Crop(scene, 204, 134, 32, 32);
        var recipe = new RecipeConfig
        {
            Name = "t",
            CameraId = "cam",
            AngleMode = AngleMode.DualTemplateCenterLine,
            Roi = roi,
            DualTemplate =
            {
                TemplateABase64 = MaskTemplateMatcher.EncodeTemplatePng(tplA),
                TemplateBBase64 = MaskTemplateMatcher.EncodeTemplatePng(tplB),
                MatchThreshold = 0.35,
                RefineRangeDeg = 5,
            },
        };
        tweak?.Invoke(recipe.DualTemplate);
        return recipe;
    }

    private static RecipeConfig PairedRecipe(Action<DualTemplateOptions>? tweak = null)
    {
        var recipe = Recipe(tweak, LeftRoi);
        recipe.DualTemplate.SecondaryRoi = RightRoi;
        return recipe;
    }

    /// <summary>黑底：左侧缺角白块（模板1）、右侧白圆环+竖线（模板2）。</summary>
    private static Mat PaintScene()
    {
        var mat = new Mat(H, W, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Point(80, 136), new Point(120, 164), Scalar.White, -1);
        Cv2.Rectangle(mat, new Point(80, 136), new Point(92, 148), Scalar.Black, -1);
        Cv2.Circle(mat, new Point(220, 150), 16, Scalar.White, 3);
        Cv2.Line(mat, new Point(220, 134), new Point(220, 166), Scalar.White, 3);
        return mat;
    }

    private static Mat Crop(Mat scene, int x, int y, int w, int h) =>
        new Mat(scene, new Rect(x, y, w, h)).Clone();

    [Fact]
    public void DualRoi_SecondaryRightOfPrimary_GivesZeroAngle()
    {
        using var mat = PaintScene();
        var poses = Compute(mat, PairedRecipe());

        var pose = Assert.Single(poses);
        Assert.InRange(pose.Cx, 95, 110);
        Assert.InRange(pose.Cy, 145, 155);
        Assert.InRange(pose.AngleDeg, -12, 12);
        Assert.True(pose.Usable);
    }

    [Fact]
    public void MissingTemplate_ReturnsEmpty()
    {
        using var mat = PaintScene();
        var recipe = PairedRecipe(o => o.TemplateBBase64 = "");
        var poses = Compute(mat, recipe);
        Assert.Empty(poses);
    }

    [Fact]
    public void Pose_CarriesOverlayBoxes_InFullImageCoords()
    {
        using var mat = PaintScene();
        var poses = Compute(mat, PairedRecipe());

        var pose = Assert.Single(poses);
        Assert.NotNull(pose.Overlay);
        var overlay = pose.Overlay!;
        Assert.Equal(2, overlay.Boxes.Count);
        Assert.Equal(2, overlay.Baseline.Count);
        Assert.True(overlay.Boxes[1].X > overlay.Boxes[0].X + 40);
        Assert.InRange(overlay.Baseline[0].X, 90, 115);
        Assert.InRange(overlay.Baseline[1].X, 200, 240);
    }

    [Fact]
    public void SecondaryRoi_IgnoresMarkOutsideSecondary()
    {
        using var mat = PaintScene();
        var poses = Compute(mat, Recipe(
            o => o.SecondaryRoi = new Roi(0.02, 0.3, 0.2, 0.4),
            roi: LeftRoi));
        Assert.Empty(poses);
    }

    [Fact]
    public void SecondaryRoi_WithoutPrimaryRoi_ReturnsEmpty()
    {
        using var mat = PaintScene();
        var poses = Compute(mat, Recipe(o => o.SecondaryRoi = RightRoi));
        Assert.Empty(poses);
    }

    [Fact]
    public void BlankImage_ReturnsEmpty()
    {
        using var mat = new Mat(H, W, MatType.CV_8UC3, Scalar.Black);
        var poses = Compute(mat, PairedRecipe());
        Assert.Empty(poses);
    }

    [Fact]
    public void PairDistanceTooSmall_ReturnsEmpty()
    {
        using var mat = PaintScene();
        var poses = Compute(mat, PairedRecipe(o =>
        {
            o.MinPairDistancePx = 500;
            o.MaxPairDistancePx = 800;
        }));
        Assert.Empty(poses);
    }

    [Fact]
    public void Validate_RequiresBothTemplates()
    {
        var recipe = Recipe(o => o.TemplateABase64 = "");
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_SecondaryRoiRequiresPrimaryRoi()
    {
        var recipe = Recipe(o => o.SecondaryRoi = RightRoi);
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_AllowsEmptyModels()
    {
        var recipe = PairedRecipe();
        recipe.Models = [];
        RecipeLoader.Validate(recipe);
    }

    private static List<PixelPose> Compute(Mat mat, RecipeConfig recipe)
    {
        using var image = VisionImageCv.FromMat(mat, ownsMat: false);
        return new DualTemplateCenterLineStrategy().Compute(image, recipe);
    }
}
