using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
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
            var roi = FeatureRoiAdvisor.Suggest(crop, W, H);
            Assert.NotNull(roi);
            var cx = roi.X + roi.Width / 2.0;
            var cy = roi.Y + roi.Height / 2.0;
            Assert.InRange(cx, 130.0 / W, 350.0 / W);
            Assert.InRange(cy, 140.0 / H, 250.0 / H);
            Assert.True(roi.Width < 0.5 && roi.Height < 0.5, "建议框应是局部特征而不是整机");
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
            Assert.Null(FeatureRoiAdvisor.Suggest(crop, img.Width, img.Height));
    }
}
