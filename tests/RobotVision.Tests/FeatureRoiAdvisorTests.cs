using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Teach;
using Xunit;

namespace RobotVision.Tests;

public sealed class FeatureRoiAdvisorTests
{
    private const int W = 480;
    private const int H = 360;

    [Fact]
    public void TabOnPlusShort_SuggestsRoiTowardTab()
    {
        using var img = new Mat(H, W, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(img, new Point(130, 152), new Point(350, 208), new Scalar(80, 80, 80), -1);
        Cv2.Rectangle(img, new Point(220, 208), new Point(260, 232), new Scalar(30, 30, 30), -1);
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Point(130, 152), new Point(350, 208), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Point(220, 208), new Point(260, 232), Scalar.All(255), -1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();

        var crop = MaskTemplateMatcher.UprightCrop(img, contour, 0.05);
        using (crop.Upright)
        {
            var roi = FeatureRoiAdvisor.Suggest(crop, W, H, contour);
            Assert.NotNull(roi);
            var cx = roi.X + roi.Width / 2.0;
            var cy = roi.Y + roi.Height / 2.0;
            Assert.InRange(cx, 130.0 / W, 350.0 / W);
            Assert.InRange(cy, 140.0 / H, 250.0 / H);
            Assert.True(roi.Width < 0.5 && roi.Height < 0.5, "建议框应是局部特征而不是整机");
            var ranked = FeatureRoiAdvisor.Rank(crop, W, H, contour);
            Assert.NotEmpty(ranked);
            Assert.Equal(roi, ranked[0].Roi);
            Assert.True(ranked[0].SizePx >= 16, $"最小窗口应 ≥16，实际 {ranked[0].SizePx}");
            Assert.True(ranked.All(c => c.SizePx != 8), "不应出现 8×8 档");
        }
    }

    [Fact]
    public void Suggest_StaysInsideMaskBoundingRect_NotPcbBelow()
    {
        using var img = new Mat(H, W, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(img, new Point(130, 152), new Point(350, 208), new Scalar(80, 80, 80), -1);
        Cv2.Rectangle(img, new Point(220, 208), new Point(260, 232), new Scalar(30, 30, 30), -1);
        for (var x = 130; x < 350; x += 18)
            Cv2.Rectangle(img, new Point(x, 258), new Point(x + 10, 310), new Scalar(15, 15, 15), -1);

        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Point(130, 152), new Point(350, 208), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Point(220, 208), new Point(260, 232), Scalar.All(255), -1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();

        var crop = MaskTemplateMatcher.UprightCrop(img, contour, 0.05);
        using (crop.Upright)
        {
            var roi = FeatureRoiAdvisor.Suggest(crop, W, H, contour);
            Assert.NotNull(roi);
            var bottom = (roi.Y + roi.Height) * H;
            Assert.True(bottom <= 240, $"建议框不应落到 mask 外 PCB 区，实际底边 y={bottom:0}");
        }
    }

    [Fact]
    public void PickOverlapping_HitsWhenCenterMissesYoloBox()
    {
        var box = new PixelBox(100, 80, 200, 40);
        var contour = new ImagePoint[]
        {
            new(0, 0), new(200, 0), new(200, 40), new(0, 40),
            new(90, 40), new(110, 70), new(90, 40),
        };
        var onPart = new InstanceSegmentation(box, 0.9, "a", contour, []);
        var elsewhere = new InstanceSegmentation(
            new PixelBox(800, 400, 80, 80), 0.99, "b",
            [new(0, 0), new(80, 0), new(80, 80), new(0, 80)], []);

        // 中心在 YOLO 盒下方（凸起上），旧逻辑会判失败；框仍与轮廓相交。
        var feature = new Roi(180.0 / 1200, 115.0 / 900, 40.0 / 1200, 30.0 / 900);
        var hits = FeatureRoiAdvisor.PickOverlapping([onPart, elsewhere], feature, 1200, 900);
        Assert.Single(hits);
        Assert.Same(onPart, hits[0]);
    }

    [Fact]
    public void PickOverlapping_CopiedDetectionRoi_PicksInstanceInside()
    {
        var part = new InstanceSegmentation(
            new PixelBox(50, 40, 120, 36), 0.8, "a",
            [new(0, 0), new(120, 0), new(120, 36), new(0, 36)], []);
        var detection = new Roi(0, 0, 1, 1);
        var hits = FeatureRoiAdvisor.PickOverlapping([part], detection, 640, 480);
        Assert.Single(hits);
    }

    [Fact]
    public void PickOverlapping_MissesWhenRectIsAway()
    {
        var part = new InstanceSegmentation(
            new PixelBox(50, 40, 120, 36), 0.8, "a",
            [new(0, 0), new(120, 0), new(120, 36), new(0, 36)], []);
        var far = new Roi(0.8, 0.8, 0.1, 0.1);
        Assert.Empty(FeatureRoiAdvisor.PickOverlapping([part], far, 640, 480));
    }

    [Fact]
    public void IsDrawable_RejectsOneByOnePlaceholder()
    {
        Assert.False(FeatureRoiAdvisor.IsDrawable(new Roi(0, 0, 1.0 / 2448, 1.0 / 2048), 2448, 2048));
        Assert.True(FeatureRoiAdvisor.IsDrawable(new Roi(0.35, 0.35, 0.3, 0.3), 2448, 2048));
    }

    [Fact]
    public void Suggest_OnLargeImage_DoesNotInflateCenterOffThinBar()
    {
        const int W = 2448;
        const int H = 2048;
        using var img = new Mat(H, W, MatType.CV_8UC3, new Scalar(240, 240, 240));
        const int x0 = 900, y0 = 980, x1 = 1500, y1 = 1024;
        Cv2.Rectangle(img, new Point(x0, y0), new Point(x1, y1), new Scalar(80, 80, 80), -1);
        Cv2.Rectangle(img, new Point(1180, y1), new Point(1260, y1 + 28), new Scalar(30, 30, 30), -1);
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Point(x0, y0), new Point(x1, y1), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Point(1180, y1), new Point(1260, y1 + 28), Scalar.All(255), -1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();

        var crop = MaskTemplateMatcher.UprightCrop(img, contour, 0.05);
        using (crop.Upright)
        {
            var roi = FeatureRoiAdvisor.Suggest(crop, W, H, contour);
            Assert.NotNull(roi);
            var cx = (roi.X + roi.Width / 2.0) * W;
            var cy = (roi.Y + roi.Height / 2.0) * H;
            Assert.InRange(cx, x0 - 8, x1 + 8);
            Assert.InRange(cy, y0 - 8, y1 + 36);
            Assert.True(roi.Height * H < 0.02 * H,
                $"高度不应被撑到全图 2%（{roi.Height * H:0}px / {H}）");
            var pad = new InstanceSegmentation(
                new PixelBox(x0, y0, x1 - x0, y1 - y0 + 28), 0.9, "a",
                contour.Select(p => new ImagePoint(p.X - x0, p.Y - y0)).ToArray(), []);
            Assert.NotEmpty(FeatureRoiAdvisor.PickOverlapping([pad], roi!, W, H));
        }
    }

    [Fact]
    public void SmoothRectangle_ReturnsNull()
    {
        using var img = new Mat(200, 320, MatType.CV_8UC3, new Scalar(200, 200, 200));
        Cv2.Rectangle(img, new Point(40, 70), new Point(280, 130), new Scalar(190, 190, 190), -1);
        Point2f[] contour = [new(40, 70), new(280, 70), new(280, 130), new(40, 130)];
        var crop = MaskTemplateMatcher.UprightCrop(img, contour, 0.05);
        using (crop.Upright)
            Assert.Null(FeatureRoiAdvisor.Suggest(crop, img.Width, img.Height, contour));
    }
}
