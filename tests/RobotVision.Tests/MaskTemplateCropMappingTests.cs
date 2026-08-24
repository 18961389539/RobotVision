using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>转正裁剪中心必须经裁剪原点 + 与 WarpAffine 相同的角做逆变换，才能映回源图。</summary>
public sealed class MaskTemplateCropMappingTests
{
    [Fact]
    public void MapUprightToSource_AxisAlignedRect_ReturnsRectCenter()
    {
        using var src = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(0));
        const double cx = 400, cy = 280;
        Point2f[] contour =
        [
            new((float)(cx - 90), (float)(cy - 35)),
            new((float)(cx + 90), (float)(cy - 35)),
            new((float)(cx + 90), (float)(cy + 35)),
            new((float)(cx - 90), (float)(cy + 35)),
        ];

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.3);
        using (crop.Upright)
        {
            var mapped = MaskTemplateMatcher.MapUprightToSource(crop,
                new Point2d(crop.Upright.Width / 2.0, crop.Upright.Height / 2.0));
            Assert.InRange(mapped.X, cx - 4, cx + 4);
            Assert.InRange(mapped.Y, cy - 4, cy + 4);
        }
    }

    [Fact]
    public void MapUprightToSource_DoesNotTreatCropCoordsAsFullImage()
    {
        using var src = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(0));
        const double cx = 500, cy = 360;
        Point2f[] contour =
        [
            new((float)(cx - 70), (float)(cy - 25)),
            new((float)(cx + 70), (float)(cy - 25)),
            new((float)(cx + 70), (float)(cy + 25)),
            new((float)(cx - 70), (float)(cy + 25)),
        ];

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.3);
        using (crop.Upright)
        {
            Assert.True(crop.CropOriginX > 50, "裁剪原点应远离图像左上角，否则无法暴露旧映射错误");
            var cropCenter = new Point2d(crop.Upright.Width / 2.0, crop.Upright.Height / 2.0);
            var mapped = MaskTemplateMatcher.MapUprightToSource(crop, cropCenter);
            var naiveDx = Math.Abs(cropCenter.X - cx);
            Assert.True(Math.Abs(mapped.X - cx) < naiveDx / 2,
                $"回投应接近矩形中心（映射 {mapped.X:0.0} vs 真值 {cx}，裁剪坐标 {cropCenter.X:0.0}）");
        }
    }
}
