using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
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

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.15);
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

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.15);
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

    [Fact]
    public void CanonicalExtraWarp_UnifiesTabMatchCenter()
    {
        const int w = 640, h = 480;
        using var src = new Mat(h, w, MatType.CV_8UC3, new Scalar(240, 240, 240));
        using var tab = new Mat(48, 200, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(tab, new Point(10, 14), new Point(190, 28), new Scalar(200, 200, 200), -1);
        Cv2.Rectangle(tab, new Point(88, 28), new Point(112, 44), new Scalar(25, 25, 25), -1);
        var tx = (w - tab.Width) / 2;
        var ty = (h - tab.Height) / 2;
        tab.CopyTo(src[new Rect(tx, ty, tab.Width, tab.Height)]);
        Point2f[] contour =
        [
            new(120, 180),
            new(520, 180),
            new(520, 300),
            new(120, 300),
        ];

        var crop0 = MaskTemplateMatcher.UprightCrop(src, contour, 0.4);
        var crop180 = MaskTemplateMatcher.UprightCrop(src, contour, 0.4, extraWarpDeg: 180);
        using (crop0.Upright)
        using (crop180.Upright)
        {
            var m0 = MaskTemplateMatcher.MatchBest(crop0.Upright, tab, 5, 0.2);
            var m180 = MaskTemplateMatcher.MatchBest(crop180.Upright, tab, 5, 0.2);
            Assert.True(m0 is not null,
                $"0° 裁剪未匹配 {crop0.Upright.Width}x{crop0.Upright.Height} warp={crop0.WarpAngleDeg:0.1}");
            Assert.True(m180 is not null,
                $"180° 裁剪未匹配 {crop180.Upright.Width}x{crop180.Upright.Height} warp={crop180.WarpAngleDeg:0.1}");

            var naive0 = MaskTemplateMatcher.MapUprightToSource(crop0, m0.CenterInUpright);
            var naive180 = MaskTemplateMatcher.MapUprightToSource(crop180, m180.CenterInUpright);
            Assert.True(Math.Abs(naive0.Y - naive180.Y) > 8,
                $"未对齐时应出现模板中心偏置（{naive0.Y:0.0} vs {naive180.Y:0.0}）");

            var c0 = CanonicalCenter(src, contour, tab, extraWarp: 0);
            var c180 = CanonicalCenter(src, contour, tab, extraWarp: 180);
            Assert.InRange(c0.X - c180.X, -3, 3);
            Assert.InRange(c0.Y - c180.Y, -3, 3);
        }
    }

    [Fact]
    public void UprightCrop_LocalPatch_MatchesFullImageWarp()
    {
        using var src = new Mat(1800, 2400, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Rectangle(src, new Rect(1100, 800, 240, 90), new Scalar(20, 180, 240), -1);
        Cv2.Rectangle(src, new Rect(1280, 820, 40, 50), new Scalar(0, 0, 220), -1);
        Point2f[] contour =
        [
            new(1100, 800), new(1340, 800), new(1340, 890), new(1100, 890),
        ];

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.15);
        using (crop.Upright)
        using (var expected = FullImageUprightCrop(src, contour, 0.15))
        {
            Assert.Equal(expected.Width, crop.Upright.Width);
            Assert.Equal(expected.Height, crop.Upright.Height);
            using var diff = new Mat();
            Cv2.Absdiff(crop.Upright, expected, diff);
            var mean = Cv2.Mean(diff);
            Assert.True(mean.Val0 + mean.Val1 + mean.Val2 < 3.0,
                $"局部转正应与整图 WarpAffine 一致，通道均值差 {mean.Val0:0.00}/{mean.Val1:0.00}/{mean.Val2:0.00}");
        }
    }

    [Fact]
    public void UprightCrop_CropIsMuchSmallerThanSource()
    {
        using var src = new Mat(2000, 3000, MatType.CV_8UC3, Scalar.All(0));
        Point2f[] contour =
        [
            new(1400, 900), new(1680, 900), new(1680, 1000), new(1400, 1000),
        ];
        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.15);
        using (crop.Upright)
        {
            Assert.True(crop.Upright.Width < src.Width / 3, $"转正窗宽 {crop.Upright.Width} 不应接近整幅 {src.Width}");
            Assert.True(crop.Upright.Height < src.Height / 3, $"转正窗高 {crop.Upright.Height} 不应接近整幅 {src.Height}");
        }
    }

    [Fact]
    public void MatchPeak_OffsetFeature_IsNotHousingCenter()
    {
        const int w = 640, h = 480;
        using var src = new Mat(h, w, MatType.CV_8UC3, new Scalar(240, 240, 240));
        using var feature = PaintOffsetFeature();
        const int fx = 360, fy = 216;
        feature.CopyTo(src[new Rect(fx, fy, feature.Width, feature.Height)]);
        Point2f[] contour =
        [
            new(120, 180), new(520, 180), new(520, 300), new(120, 300),
        ];
        var housing = MaskHousing.Fit(contour);
        var expect = new Point2d(fx + feature.Width / 2.0, fy + feature.Height / 2.0);

        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.4);
        using (crop.Upright)
        {
            var match = MaskTemplateMatcher.MatchBest(crop.Upright, feature, 5, 0.2);
            Assert.NotNull(match);
            var peak = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);
            Assert.InRange(peak.X, expect.X - 16, expect.X + 16);
            Assert.InRange(peak.Y, expect.Y - 16, expect.Y + 16);
            Assert.True(Math.Abs(peak.X - housing.Center.X) > 40,
                $"匹配峰 X={peak.X:0.0} 不应落在壳体中心 {housing.Center.X:0.0}");
        }
    }

    [Fact]
    public void AxisAlignedCrop_MatchPeak_TranslatesToSource()
    {
        using var src = new Mat(400, 600, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(src, new Rect(120, 160, 300, 80), new Scalar(200, 200, 200), -1);
        using var feature = PaintOffsetFeature();
        const int fx = 340, fy = 176;
        feature.CopyTo(src[new Rect(fx, fy, feature.Width, feature.Height)]);
        Point2f[] contour =
        [
            new(120, 160), new(420, 160), new(420, 240), new(120, 240),
        ];
        var expect = new Point2d(fx + feature.Width / 2.0, fy + feature.Height / 2.0);

        var crop = MaskTemplateMatcher.AxisAlignedCrop(src, contour, 0.15);
        using (crop.Upright)
        {
            Assert.Equal(0, crop.WarpAngleDeg);
            var match = MaskTemplateMatcher.MatchBest(crop.Upright, feature, 5, 0.3);
            Assert.NotNull(match);
            var peak = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);
            Assert.InRange(peak.X, expect.X - 8, expect.X + 8);
            Assert.InRange(peak.Y, expect.Y - 8, expect.Y + 8);
        }
    }

    private static Mat PaintOffsetFeature()
    {
        var mat = new Mat(48, 80, MatType.CV_8UC3, new Scalar(230, 230, 230));
        Cv2.Rectangle(mat, new Point(6, 10), new Point(74, 22), new Scalar(40, 90, 200), -1);
        Cv2.Rectangle(mat, new Point(28, 22), new Point(52, 42), new Scalar(20, 20, 20), -1);
        return mat;
    }

    private static Point2d CanonicalCenter(Mat src, Point2f[] contour, Mat template, double extraWarp)
    {
        var crop = MaskTemplateMatcher.UprightCrop(src, contour, 0.4, extraWarp);
        using (crop.Upright)
        {
            var match = MaskTemplateMatcher.MatchBest(crop.Upright, template, 5, 0.2);
            Assert.NotNull(match);
            if (!MaskTemplateMatcher.NeedsUprightAlign(match))
                return MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);

            var recrop = MaskTemplateMatcher.UprightCrop(src, contour, 0.4, extraWarp + 180);
            using (recrop.Upright)
            {
                var rematch = MaskTemplateMatcher.MatchBest(
                    recrop.Upright, template, 5, 0.2, orientationBranchDeg: 0);
                Assert.NotNull(rematch);
                Assert.False(MaskTemplateMatcher.IsOrientationFlip(rematch.RotationDeg));
                return MaskTemplateMatcher.MapUprightToSource(recrop, rematch.CenterInUpright);
            }
        }
    }

    private static Mat FullImageUprightCrop(Mat src, IReadOnlyList<Point2f> contour, double marginRatio)
    {
        var rect = Cv2.MinAreaRect(contour);
        var warpAngleDeg = MaskHousing.WarpFromMinAreaRect(rect);
        using var m = Cv2.GetRotationMatrix2D(rect.Center, -warpAngleDeg, 1.0);
        using var rotated = new Mat();
        Cv2.WarpAffine(src, rotated, m, src.Size(), InterpolationFlags.Linear, BorderTypes.Reflect101);
        // 与 UprightCrop 同口径：显式长/短边（MinAreaRect 的 Width/Height 表示顺序不保证）
        var longLen = Math.Max(rect.Size.Width, rect.Size.Height);
        var shortLen = Math.Min(rect.Size.Width, rect.Size.Height);
        var cropW = (int)Math.Ceiling(longLen * (1 + 2 * marginRatio));
        var cropH = (int)Math.Ceiling(shortLen * (1 + 2 * marginRatio));
        var x = Math.Clamp((int)Math.Floor(rect.Center.X - cropW / 2.0), 0, Math.Max(0, rotated.Width - 1));
        var y = Math.Clamp((int)Math.Floor(rect.Center.Y - cropH / 2.0), 0, Math.Max(0, rotated.Height - 1));
        cropW = Math.Min(cropW, rotated.Width - x);
        cropH = Math.Min(cropH, rotated.Height - y);
        return rotated[new Rect(x, y, cropW, cropH)].Clone();
    }
}
