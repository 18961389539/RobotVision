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

    [Fact]
    public void SelectHybridOrientation_OneGrayNull_UsesTheOther()
    {
        var edge = new MaskTemplateMatchResult(0.9, 12, new Point2d(10, 20));
        var a = new MaskTemplateMatchResult(0.4, 12, new Point2d(11, 21));

        var fromA = MaskTemplateMatcher.SelectHybridOrientation(edge, a, null);
        Assert.Equal(edge.Score, fromA.Score);
        Assert.Equal(a.RotationDeg, fromA.RotationDeg);
        Assert.Equal(a.CenterInUpright, fromA.CenterInUpright);

        var b = new MaskTemplateMatchResult(0.5, -168, new Point2d(9, 19));
        var fromB = MaskTemplateMatcher.SelectHybridOrientation(edge, null, b);
        Assert.Equal(b.RotationDeg, fromB.RotationDeg);
        Assert.Equal(b.CenterInUpright, fromB.CenterInUpright);
    }

    [Fact]
    public void SelectHybridOrientation_BothNull_ReturnsEdge()
    {
        var edge = new MaskTemplateMatchResult(0.88, 3, new Point2d(1, 2));
        var picked = MaskTemplateMatcher.SelectHybridOrientation(edge, null, null);
        Assert.Same(edge, picked);
    }

    [Fact]
    public void MapSourceToUpright_RoundTripsWithMapUprightToSource()
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

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0);
        using (crop.Upright)
        {
            var srcPt = new Point2d(cx, cy);
            var up = MaskTemplateMatcher.MapSourceToUpright(crop, srcPt);
            var back = MaskTemplateMatcher.MapUprightToSource(crop, up);
            Assert.InRange(back.X, srcPt.X - 1, srcPt.X + 1);
            Assert.InRange(back.Y, srcPt.Y - 1, srcPt.Y + 1);
        }
    }

    [Fact]
    public void CropUprightBySourceRect_LeftHalfOfBlob_IsNarrowerThanFullCrop()
    {
        using var src = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(0));
        const double cx = 400, cy = 280, halfW = 90, halfH = 35;
        Point2f[] contour =
        [
            new((float)(cx - halfW), (float)(cy - halfH)),
            new((float)(cx + halfW), (float)(cy - halfH)),
            new((float)(cx + halfW), (float)(cy + halfH)),
            new((float)(cx - halfW), (float)(cy + halfH)),
        ];

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0);
        using (crop.Upright)
        {
            using var feature = MaskTemplateMatcher.CropUprightBySourceRect(
                crop, cx - halfW, cy - halfH, halfW, halfH * 2);
            var fullArea = crop.Upright.Width * crop.Upright.Height;
            var featArea = feature.Width * feature.Height;
            Assert.True(featArea < fullArea, $"特征 {feature.Width}x{feature.Height} 应小于全目标 {crop.Upright.Width}x{crop.Upright.Height}");
            Assert.True(feature.Width >= 8 && feature.Height >= 8);
        }
    }

    [Fact]
    public void CropUprightBySourceRect_FarOutsideBlob_Throws()
    {
        using var src = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(0));
        Point2f[] contour =
        [
            new(300, 200), new(500, 200), new(500, 280), new(300, 280),
        ];
        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0);
        using (crop.Upright)
        {
            Assert.Throws<InvalidOperationException>(() =>
                MaskTemplateMatcher.CropUprightBySourceRect(crop, 1, 1, 12, 12));
        }
    }

    [Fact]
    public void SelectHybridOrientation_PicksHigherGrayScore()
    {
        var edge = new MaskTemplateMatchResult(0.9, 0, new Point2d(0, 0));
        var low = new MaskTemplateMatchResult(0.2, 0, new Point2d(1, 1));
        var high = new MaskTemplateMatchResult(0.8, 180, new Point2d(2, 2));
        var picked = MaskTemplateMatcher.SelectHybridOrientation(edge, low, high);
        Assert.Equal(180, picked.RotationDeg);
        Assert.Equal(edge.Score, picked.Score);
    }
}
