using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// ROI 裁剪辅助（RoiHelper）测试：
/// - 相对比例 → 像素 Rect 换算（尺寸与偏移量）；
/// - 越界 clamp：X/Y 负值归零、右/下越界截断到图像边界（Round 后求交，防越界 1px 视图）；
/// - 无交集抛 ArgumentException；null ROI 全图直通（偏移 0）；
/// - ToBitmap / CropToVisionImage 的尺寸与偏移输出正确。
/// </summary>
public class RoiHelperTests
{
    [Fact]
    public void Crop_NormalRoi_SizeAndOffsetCorrect()
    {
        using var mat = new Mat(100, 200, MatType.CV_8UC3, Scalar.All(0)); // 200×100
        var roi = new Roi(0.25, 0.5, 0.5, 0.25);

        using var cropped = RoiHelper.Crop(mat, roi, out var ox, out var oy);

        Assert.Equal(100, cropped.Width);  // 0.5×200
        Assert.Equal(25, cropped.Height);  // 0.25×100
        Assert.Equal(50, ox);
        Assert.Equal(50, oy);
    }

    [Fact]
    public void Crop_FullRoi_MatchesImage()
    {
        using var mat = new Mat(100, 200, MatType.CV_8UC3, Scalar.All(0));
        var roi = new Roi(0, 0, 1, 1);

        using var cropped = RoiHelper.Crop(mat, roi, out var ox, out var oy);

        Assert.Equal(200, cropped.Width);
        Assert.Equal(100, cropped.Height);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
    }

    [Fact]
    public void Crop_NegativeOrigin_ClampedToZero()
    {
        using var mat = new Mat(100, 200, MatType.CV_8UC3, Scalar.All(0));
        var roi = new Roi(-0.1, -0.2, 0.5, 0.5);

        using var cropped = RoiHelper.Crop(mat, roi, out var ox, out var oy);

        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(100, cropped.Width); // min(0.5, 1-(-0.1)=1.1)×200 = 100
        Assert.Equal(50, cropped.Height);
    }

    [Fact]
    public void Crop_RightBottomOverflow_ClippedToImageEdge()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        var roi = new Roi(0.6, 0.6, 0.5, 0.5);

        using var cropped = RoiHelper.Crop(mat, roi, out var ox, out var oy);

        // w = min(0.5, 1-0.6=0.4)×100 = 40：右/下越界部分被截断，不产生越界视图
        Assert.Equal(60, ox);
        Assert.Equal(60, oy);
        Assert.Equal(40, cropped.Width);
        Assert.Equal(40, cropped.Height);
    }

    [Fact]
    public void Crop_EdgeTouchingRoi_NoOverflow()
    {
        // 右边缘紧贴：ROI X+W == 1 时不得越界 1px（独立 Round 的经典毛刺场景）
        using var mat = new Mat(50, 100, MatType.CV_8UC3, Scalar.All(0));
        var roi = new Roi(0.99, 0, 0.01, 1);

        using var cropped = RoiHelper.Crop(mat, roi, out var ox, out var oy);

        Assert.Equal(99, ox);
        Assert.Equal(1, cropped.Width);   // 1×100 贴边列
        Assert.Equal(50, cropped.Height);
    }

    [Fact]
    public void Crop_NoIntersection_Throws()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        var roi = new Roi(1, 1, 0.5, 0.5); // 起点在图像外右下角，无交集

        Assert.Throws<ArgumentException>(() => RoiHelper.Crop(mat, roi, out _, out _));
    }

    [Fact]
    public void ToBitmap_NullRoi_FullSize_ZeroOffset()
    {
        using var mat = new Mat(60, 80, MatType.CV_8UC3, Scalar.All(0));

        using var bitmap = RoiHelper.ToBitmap(mat, null, out var ox, out var oy);

        Assert.Equal(80, bitmap.Width);
        Assert.Equal(60, bitmap.Height);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
    }

    [Fact]
    public void ToBitmap_WithRoi_CroppedSize_OffsetCorrect()
    {
        using var mat = new Mat(100, 200, MatType.CV_8UC3, Scalar.All(0));
        var roi = new Roi(0.5, 0, 0.25, 1);

        using var bitmap = RoiHelper.ToBitmap(mat, roi, out var ox, out var oy);

        Assert.Equal(50, bitmap.Width); // 0.25×200
        Assert.Equal(100, bitmap.Height);
        Assert.Equal(100, ox);
        Assert.Equal(0, oy);
    }

    [Fact]
    public void CropToVisionImage_NullRoi_ReturnsNull()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        using var source = VisionImageCv.FromMat(mat, ownsMat: false);

        var cropped = RoiHelper.CropToVisionImage(source, null, out var ox, out var oy);

        Assert.Null(cropped);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
    }

    [Fact]
    public void CropToVisionImage_WithRoi_ReturnsOwnedCroppedImage()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        using var source = VisionImageCv.FromMat(mat, ownsMat: false);
        var roi = new Roi(0.5, 0.5, 0.5, 0.5);

        using var cropped = RoiHelper.CropToVisionImage(source, roi, out var ox, out var oy);

        Assert.NotNull(cropped);
        Assert.Equal(50, cropped!.Width);
        Assert.Equal(50, cropped.Height);
        Assert.Equal(50, ox);
        Assert.Equal(50, oy);
    }

    [Fact]
    public void Crop_FractionalRoi_RoundsConsistently()
    {
        // 像素↔比例往返换算产生 1.0000000000000002 一类浮点毛刺：0.333…×300 = 99.9999… → Round 100
        using var mat = new Mat(300, 300, MatType.CV_8UC3, Scalar.All(0));
        var roi = new Roi(0.3333333333333333, 0.3333333333333333, 0.3333333333333333, 0.3333333333333333);

        using var cropped = RoiHelper.Crop(mat, roi, out var ox, out var oy);

        Assert.Equal(100, ox);
        Assert.Equal(100, oy);
        Assert.Equal(100, cropped.Width);
        Assert.Equal(100, cropped.Height);
    }
}
