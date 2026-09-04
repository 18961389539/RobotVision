using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 双BLOB连线策略（DualBlobCenterLine）单元测试：合成圆形亮/暗斑图，
/// 验证主BLOB质心定位、主→次连线角度、窗口/面积/间距过滤、ROI 偏移与多目标。
/// </summary>
public class DualBlobCenterLineTests
{
    private const int W = 400;
    private const int H = 300;

    private static RecipeConfig Recipe(Action<BlobOptions>? tweak = null, Roi? roi = null)
    {
        var recipe = new RecipeConfig
        {
            Name = "t",
            CameraId = "cam",
            AngleMode = AngleMode.DualBlobCenterLine,
            Roi = roi,
        };
        tweak?.Invoke(recipe.Blob);
        return recipe;
    }

    /// <summary>黑底亮斑（默认 DetectDark=false 用）。</summary>
    private static Mat BrightBlobs(params (int X, int Y, int R)[] circles)
    {
        var mat = new Mat(H, W, MatType.CV_8UC3, Scalar.Black);
        foreach (var (x, y, r) in circles)
            Cv2.Circle(mat, x, y, r, Scalar.White, -1);
        return mat;
    }

    [Fact]
    public void SecondaryRightOfPrimary_GivesZeroAngle()
    {
        using var mat = BrightBlobs((100, 150, 20), (150, 150, 8));
        var poses = Compute(mat, Recipe());

        var pose = Assert.Single(poses);
        Assert.InRange(pose.Cx, 98, 102);
        Assert.InRange(pose.Cy, 148, 152);
        Assert.InRange(pose.AngleDeg, -1.5, 1.5);
    }

    [Fact]
    public void SecondaryAbovePrimary_GivesMinus90()
    {
        // 图像坐标 y 向下：次 BLOB 在主 BLOB 上方 → atan2(-50, 0) = -90°
        using var mat = BrightBlobs((100, 150, 20), (100, 100, 8));
        var poses = Compute(mat, Recipe());

        var pose = Assert.Single(poses);
        Assert.InRange(pose.AngleDeg, -91.5, -88.5);
    }

    [Fact]
    public void NoSecondary_ReturnsEmpty()
    {
        using var mat = BrightBlobs((100, 150, 20));
        var poses = Compute(mat, Recipe());
        Assert.Empty(poses);
    }

    /// <summary>位姿应携带主/次 BLOB 包围盒叠加数据（全图坐标系），供画面绘制。</summary>
    [Fact]
    public void Pose_CarriesOverlayBoxes_InFullImageCoords()
    {
        using var mat = BrightBlobs((100, 150, 20), (150, 150, 8));
        var poses = Compute(mat, Recipe());

        var pose = Assert.Single(poses);
        var boxes = pose.Overlay?.Boxes;
        Assert.NotNull(boxes);
        Assert.Equal(2, boxes.Count);
        // 主 BLOB（圆心 100,150 半径 20 → 包围盒约 (80,130) 41×41）
        Assert.InRange(boxes[0].X, 76, 84);
        Assert.InRange(boxes[0].Y, 126, 134);
        Assert.InRange(boxes[0].Width, 37, 45);
        // 次 BLOB（圆心 150,150 半径 8 → 包围盒约 (142,142) 17×17）
        Assert.InRange(boxes[1].X, 138, 146);
        Assert.InRange(boxes[1].Y, 138, 146);
        Assert.InRange(boxes[1].Width, 13, 21);

        // 角度基线：主质心 → 次质心（与角度计算同两点）
        var baseline = pose.Overlay?.Baseline;
        Assert.NotNull(baseline);
        Assert.Equal(2, baseline.Count);
        Assert.InRange(baseline[0].X, 98, 102);
        Assert.InRange(baseline[0].Y, 148, 152);
        Assert.InRange(baseline[1].X, 148, 152);
        Assert.InRange(baseline[1].Y, 148, 152);
    }

    [Fact]
    public void SecondaryOutsideWindow_ReturnsEmpty()
    {
        // 主包围盒 41×41，外扩 1.0 → 窗口右缘 ≈161；次 BLOB 质心 x=250 在窗外
        using var mat = BrightBlobs((100, 150, 20), (250, 150, 8));
        var poses = Compute(mat, Recipe());
        Assert.Empty(poses);
    }

    [Fact]
    public void SecondaryBeyondMaxDistance_ReturnsEmpty()
    {
        using var mat = BrightBlobs((100, 150, 20), (150, 150, 8));
        var poses = Compute(mat,
            Recipe(o => o.MaxPairDistancePx = 30));
        Assert.Empty(poses);
    }

    [Fact]
    public void DarkBlobs_WithDetectDark_Work()
    {
        using var mat = new Mat(H, W, MatType.CV_8UC3, Scalar.White);
        Cv2.Circle(mat, 100, 150, 20, Scalar.Black, -1);
        Cv2.Circle(mat, 150, 150, 8, Scalar.Black, -1);
        var poses = Compute(mat,
            Recipe(o => o.DetectDark = true));

        var pose = Assert.Single(poses);
        Assert.InRange(pose.Cx, 98, 102);
        Assert.InRange(pose.AngleDeg, -1.5, 1.5);
    }

    [Fact]
    public void PrimaryBelowMinArea_ReturnsEmpty()
    {
        // r=5 面积 ≈78 < 默认 MinArea=200
        using var mat = BrightBlobs((100, 150, 5), (150, 150, 8));
        var poses = Compute(mat, Recipe());
        Assert.Empty(poses);
    }

    [Fact]
    public void SecondaryBelowMinArea_ReturnsEmpty()
    {
        // 次 BLOB r=2 面积 ≈13；把次面积下限抬到 50 后应被过滤
        using var mat = BrightBlobs((100, 150, 20), (150, 150, 2));
        var poses = Compute(mat,
            Recipe(o => o.SecondaryMinArea = 50));
        Assert.Empty(poses);
    }

    [Fact]
    public void Roi_OffsetsCoordinatesBackToFullImage()
    {
        // ROI 左上角 (100,75)；主 BLOB 画在全图 (200,150) → ROI 内 (100,75)
        using var mat = BrightBlobs((200, 150, 20), (250, 150, 8));
        var roi = new Roi(0.25, 0.25, 0.5, 0.5);
        var poses = Compute(mat, Recipe(roi: roi));

        var pose = Assert.Single(poses);
        Assert.InRange(pose.Cx, 198, 202);
        Assert.InRange(pose.Cy, 148, 152);
        Assert.InRange(pose.AngleDeg, -1.5, 1.5);
    }

    [Fact]
    public void TwoTargets_ReturnTwoPoses()
    {
        using var mat = BrightBlobs((100, 100, 20), (150, 100, 8), (100, 220, 25), (155, 220, 8));
        var poses = Compute(mat, Recipe());

        Assert.Equal(2, poses.Count);
        // 按主面积降序：r=25 的目标排前
        Assert.InRange(poses[0].Cy, 218, 222);
        Assert.InRange(poses[1].Cy, 98, 102);
    }

    [Fact]
    public void TouchingBlobs_OpenKernelSeparates()
    {
        // 主 r=20 @(100,150)、次 r=8 @(140,150)，2px 细桥连接 → 无开运算时是一个连通域
        using var mat = BrightBlobs((100, 150, 20), (140, 150, 8));
        Cv2.Rectangle(mat, new Rect(118, 149, 14, 2), Scalar.White, -1);

        var merged = Compute(mat, Recipe());
        Assert.Empty(merged);

        var separated = Compute(mat,
            Recipe(o => o.OpenKernelSize = 3));
        var pose = Assert.Single(separated);
        Assert.InRange(pose.AngleDeg, -3, 3);
    }

    [Fact]
    public void FixedThreshold_UsedWhenOtsuOff()
    {
        // 灰斑（灰度 200）在灰底（灰度 60）上：固定阈值 128 可分；Otsu 亦可，但此处验证固定路径
        using var mat = new Mat(H, W, MatType.CV_8UC3, new Scalar(60, 60, 60));
        Cv2.Circle(mat, 100, 150, 20, new Scalar(200, 200, 200), -1);
        Cv2.Circle(mat, 150, 150, 8, new Scalar(200, 200, 200), -1);
        var poses = Compute(mat,
            Recipe(o => { o.UseOtsu = false; o.Threshold = 128; }));

        Assert.Single(poses);
    }

    [Fact]
    public void GrayscaleInput_Works()
    {
        using var mat = new Mat(H, W, MatType.CV_8UC1, Scalar.Black);
        Cv2.Circle(mat, 100, 150, 20, Scalar.White, -1);
        Cv2.Circle(mat, 150, 150, 8, Scalar.White, -1);
        var poses = Compute(mat, Recipe());
        Assert.Single(poses);
    }

    // ---- RecipeLoader.Validate 值域校验 ----

    [Fact]
    public void Validate_BlobMode_AllowsZeroModels()
    {
        var recipe = Recipe();
        RecipeLoader.Validate(recipe); // 不抛异常即通过
    }

    [Fact]
    public void Validate_BlobMode_RejectsInvertedAreaRange()
    {
        var recipe = Recipe(o => { o.MinArea = 1000; o.MaxArea = 100; });
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_BlobMode_RejectsBadPairDistance()
    {
        var recipe = Recipe(o => { o.MinPairDistancePx = 100; o.MaxPairDistancePx = 50; });
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_BlobMode_RejectsBadExpandRatio()
    {
        var recipe = Recipe(o => o.CropExpandRatio = 0);
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_OtherModes_StillRequireModels()
    {
        var recipe = Recipe();
        recipe.AngleMode = AngleMode.MaskMinAreaRect;
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void SecondaryRoi_PairsAcrossDistantRegions()
    {
        using var mat = BrightBlobs((100, 150, 20), (250, 150, 8));
        var poses = Compute(mat, Recipe(
            o => o.SecondaryRoi = new Roi(0.55, 0.3, 0.3, 0.4),
            roi: new Roi(0.15, 0.3, 0.3, 0.4)));

        var pose = Assert.Single(poses);
        Assert.InRange(pose.Cx, 98, 102);
        Assert.InRange(pose.AngleDeg, -1.5, 1.5);
    }

    [Fact]
    public void SecondaryRoi_IgnoresBlobOutsideSecondary()
    {
        using var mat = BrightBlobs((100, 150, 20), (150, 150, 8));
        var poses = Compute(mat, Recipe(
            o => o.SecondaryRoi = new Roi(0.7, 0.3, 0.25, 0.4),
            roi: new Roi(0.1, 0.3, 0.3, 0.4)));
        Assert.Empty(poses);
    }

    [Fact]
    public void SecondaryRoi_WithoutPrimaryRoi_ReturnsEmpty()
    {
        using var mat = BrightBlobs((100, 150, 20), (250, 150, 8));
        var poses = Compute(mat, Recipe(o => o.SecondaryRoi = new Roi(0.55, 0.3, 0.3, 0.4)));
        Assert.Empty(poses);
    }

    [Fact]
    public void SecondaryRoi_DoesNotDetectBlob2AsPrimary()
    {
        // 两个同等大斑：若 BLOB1 扫全图会出两个位姿。互斥 ROI 后只应留下 ROI1 里的那一个。
        using var mat = BrightBlobs((100, 150, 20), (250, 150, 20));
        var poses = Compute(mat, Recipe(
            o => o.SecondaryRoi = new Roi(0.5, 0.2, 0.45, 0.6),
            roi: new Roi(0.05, 0.2, 0.4, 0.6)));

        var pose = Assert.Single(poses);
        Assert.InRange(pose.Cx, 98, 102);
        Assert.InRange(pose.Cy, 148, 152);
        Assert.InRange(pose.AngleDeg, -1.5, 1.5);
    }

    [Fact]
    public void Validate_BlobMode_RejectsBadSecondaryRoi()
    {
        var recipe = Recipe(
            o => o.SecondaryRoi = new Roi(0.9, 0.9, 0.5, 0.5),
            roi: new Roi(0.1, 0.1, 0.3, 0.3));
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_BlobMode_SecondaryRoiRequiresPrimaryRoi()
    {
        var recipe = Recipe(o => o.SecondaryRoi = new Roi(0.55, 0.3, 0.3, 0.4));
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    private static List<PixelPose> Compute(Mat mat, RecipeConfig recipe)
    {
        using var image = VisionImageCv.FromMat(mat, ownsMat: false);
        return new DualBlobCenterLineStrategy().Compute(image, recipe);
    }
}
