using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>SIFT 精修：示教不对称件（避免齿列周期误配），查询旋转后应跟上。</summary>
public sealed class MaskSiftRefineTests
{
    private const int W = 480;
    private const int H = 360;

    [Fact]
    public void Identity_RecoversTeachPose()
    {
        using var img = Paint(0);
        var contour = Contour(0);
        using var teachMat = Crop(img, contour);
        using var teach = MaskSiftRefine.BuildTeach(teachMat);
        Assert.NotNull(teach);
        var r = MaskSiftRefine.Refine(img, contour, teach);
        Assert.NotNull(r);
        Assert.InRange(r.AngleDeg, -2.0, 2.0);
        Assert.InRange(r.Center.X, W / 2.0 - 10, W / 2.0 + 10);
        Assert.InRange(r.Center.Y, H / 2.0 - 12, H / 2.0 + 12);
        Assert.True(r.Inliers >= 8, $"内点 {r.Inliers}");
    }

    [Theory]
    [InlineData(12)]
    [InlineData(37)]
    [InlineData(-18)]
    [InlineData(180)]
    public void FollowsRotation(double deg)
    {
        using var teachImg = Paint(0);
        var teachContour = Contour(0);
        using var teachMat = Crop(teachImg, teachContour);
        using var teach = MaskSiftRefine.BuildTeach(teachMat);
        Assert.NotNull(teach);

        using var img = Paint(deg);
        var contour = Contour(deg);
        var r = MaskSiftRefine.Refine(img, contour, teach);
        Assert.NotNull(r);
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - deg));
        Assert.True(err < 3.0,
            $"转 {deg}°：得 {r.AngleDeg:0.00}，误差 {err:0.00}° 内点 {r.Inliers} 匹配 {MaskSiftRefine.LastDebug.Matches}");
        Assert.InRange(r.Center.X, W / 2.0 - 16, W / 2.0 + 16);
        Assert.InRange(r.Center.Y, H / 2.0 - 20, H / 2.0 + 20);
    }

    [Fact]
    public void EmptyTeach_ReturnsNull()
    {
        using var blank = new Mat(80, 80, MatType.CV_8UC1, Scalar.All(30));
        Assert.Null(MaskSiftRefine.BuildTeach(blank));
    }

    private static Mat Paint(double rotateDeg)
    {
        var img = new Mat(H, W, MatType.CV_8UC1, new Scalar(24));
        FillPart(img, rotateDeg, 210);
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
        return img;
    }

    private static Point2f[] Contour(double rotateDeg)
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        FillPart(mask, rotateDeg, 255);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        return contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();
    }

    private static Mat Crop(Mat img, Point2f[] contour)
    {
        var box = Cv2.BoundingRect(contour);
        var pad = 12;
        var x = Math.Max(0, box.X - pad);
        var y = Math.Max(0, box.Y - pad);
        var w = Math.Min(img.Width - x, box.Width + 2 * pad);
        var h = Math.Min(img.Height - y, box.Height + 2 * pad);
        return new Mat(img, new Rect(x, y, w, h)).Clone();
    }

    /// <summary>横条 + 右下圆点 + 左上缺口，避免周期齿列让 SIFT 配错。</summary>
    private static void FillPart(Mat img, double rotateDeg, byte value)
    {
        Cv2.FillConvexPoly(img, RectCorners(W / 2.0, H / 2.0, 110, 28, rotateDeg), new Scalar(value));
        var notch = RectCorners(W / 2.0 - 70, H / 2.0 - 18, 18, 12, rotateDeg);
        Cv2.FillConvexPoly(img, notch, new Scalar(24));
        var c = RotatePoint(W / 2.0 + 78, H / 2.0 + 18, rotateDeg);
        Cv2.Circle(img, c, 11, new Scalar(value), -1);
        var c2 = RotatePoint(W / 2.0 + 40, H / 2.0 - 8, rotateDeg);
        Cv2.Circle(img, c2, 6, new Scalar(40), -1);
    }

    private static Point RotatePoint(double x, double y, double deg)
    {
        var ox = W / 2.0;
        var oy = H / 2.0;
        var rad = deg * Math.PI / 180.0;
        var dx = x - ox;
        var dy = y - oy;
        return new Point(
            (int)Math.Round(ox + dx * Math.Cos(rad) - dy * Math.Sin(rad)),
            (int)Math.Round(oy + dx * Math.Sin(rad) + dy * Math.Cos(rad)));
    }

    private static Point[] RectCorners(double cx, double cy, double hw, double hh, double deg)
    {
        var a = RotatePoint(cx - hw, cy - hh, deg);
        var b = RotatePoint(cx + hw, cy - hh, deg);
        var c = RotatePoint(cx + hw, cy + hh, deg);
        var d = RotatePoint(cx - hw, cy + hh, deg);
        return [a, b, c, d];
    }
}
