using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>开源 Chamfer 形状匹配：示教不对称件，只在分割转正窗内精修。</summary>
public sealed class MaskShapeMatchTests
{
    private const int W = 480;
    private const int H = 360;

    [Fact]
    public void Identity_RecoversTeachPose()
    {
        using var img = Paint(0);
        var contour = Contour(0);
        var model = Teach(img, contour);
        Assert.NotNull(model);
        Assert.True(Math.Abs(model.PolarDelta) >= 12, $"示教头尾灰度差太弱 {model.PolarDelta:0.0}");
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8);
        Assert.True(attempt.Pose is not null,
            $"未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00}");
        var r = attempt.Pose!;
        Assert.InRange(r.AngleDeg, -8.0, 8.0);
        AssertNearPart(r.Center, contour);
    }

    [Theory]
    [InlineData(37)]
    [InlineData(180)]
    public void FollowsRotation(double deg)
    {
        using var teachImg = Paint(0);
        var teachContour = Contour(0);
        var model = Teach(teachImg, teachContour);
        Assert.NotNull(model);

        using var img = Paint(deg);
        var contour = Contour(deg);
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 12);
        Assert.True(attempt.Pose is not null,
            $"转 {deg}° 未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00} 残差 {MaskShapeMatch.LastDebug.ResidualDeg:0.00}° " +
            $"极性 {MaskShapeMatch.LastDebug.PolarTeach:0.0}/{MaskShapeMatch.LastDebug.Polar0:0.0}/{MaskShapeMatch.LastDebug.Polar180:0.0}");
        var r = attempt.Pose!;
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - deg));
        Assert.True(err < 8.0,
            $"转 {deg}°：得 {r.AngleDeg:0.00}，误差 {err:0.00}° 命中 {r.HitRate:0.00} 均距 {r.MeanDistPx:0.00} " +
            $"极性 {MaskShapeMatch.LastDebug.PolarTeach:0.0}/{MaskShapeMatch.LastDebug.Polar0:0.0}/{MaskShapeMatch.LastDebug.Polar180:0.0}");
        AssertNearPart(r.Center, contour);
    }

    [Fact]
    public void IgnoresDistractorOutsideSegmentBox()
    {
        using var teachImg = Paint(0);
        var model = Teach(teachImg, Contour(0));
        Assert.NotNull(model);

        using var img = Paint(0);
        FillPart(img, 0, 210, offsetX: -170, offsetY: -110);
        var contour = Contour(0);
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8);
        Assert.True(attempt.Pose is not null,
            $"未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00}");
        var r = attempt.Pose!;
        Assert.InRange(r.AngleDeg, -8.0, 8.0);
        AssertNearPart(r.Center, contour);
    }

    [Fact]
    public void EmptyTeach_ReturnsNull()
    {
        using var blank = new Mat(80, 80, MatType.CV_8UC1, Scalar.All(30));
        Assert.Null(MaskShapeMatch.BuildTeach(blank));
    }

    private static void AssertNearPart(Point2d center, Point2f[] contour)
    {
        var housing = MaskHousing.FitObb(contour);
        var dist = Math.Sqrt(
            (center.X - housing.Center.X) * (center.X - housing.Center.X)
            + (center.Y - housing.Center.Y) * (center.Y - housing.Center.Y));
        Assert.True(dist < 52,
            $"中心 ({center.X:0.0},{center.Y:0.0}) 距壳体 ({housing.Center.X:0.0},{housing.Center.Y:0.0}) {dist:0.0}px");
    }

    private static MaskShapeMatch.ShapeModel Teach(Mat img, Point2f[] contour)
    {
        var crop = MaskTemplateMatcher.UprightCrop(img, contour, 0);
        try
        {
            var model = MaskShapeMatch.BuildTeach(crop.Upright);
            Assert.True(model is { PointCount: >= 24 }, $"示教点数 {model?.PointCount}");
            return model!;
        }
        finally
        {
            crop.Upright.Dispose();
        }
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

    /// <summary>横条 + 右下圆点 + 左上缺口，避免 0/180 对称。</summary>
    private static void FillPart(Mat img, double rotateDeg, byte value, double offsetX = 0, double offsetY = 0)
    {
        var cx = W / 2.0 + offsetX;
        var cy = H / 2.0 + offsetY;
        Cv2.FillConvexPoly(img, RectCorners(cx, cy, 110, 28, rotateDeg, cx, cy), new Scalar(value));
        var notch = RectCorners(cx - 70, cy - 18, 18, 12, rotateDeg, cx, cy);
        Cv2.FillConvexPoly(img, notch, new Scalar(24));
        var c = RotatePoint(cx + 78, cy + 18, rotateDeg, cx, cy);
        Cv2.Circle(img, c, 11, new Scalar(value), -1);
        var c2 = RotatePoint(cx + 40, cy - 8, rotateDeg, cx, cy);
        Cv2.Circle(img, c2, 6, new Scalar(40), -1);
    }

    private static Point RotatePoint(double x, double y, double deg, double ox, double oy)
    {
        var rad = deg * Math.PI / 180.0;
        var dx = x - ox;
        var dy = y - oy;
        return new Point(
            (int)Math.Round(ox + dx * Math.Cos(rad) - dy * Math.Sin(rad)),
            (int)Math.Round(oy + dx * Math.Sin(rad) + dy * Math.Cos(rad)));
    }

    private static Point[] RectCorners(
        double cx, double cy, double hw, double hh, double deg, double ox, double oy)
    {
        var a = RotatePoint(cx - hw, cy - hh, deg, ox, oy);
        var b = RotatePoint(cx + hw, cy - hh, deg, ox, oy);
        var c = RotatePoint(cx + hw, cy + hh, deg, ox, oy);
        var d = RotatePoint(cx - hw, cy + hh, deg, ox, oy);
        return [a, b, c, d];
    }
}
