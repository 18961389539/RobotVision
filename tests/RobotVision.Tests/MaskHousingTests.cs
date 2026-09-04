using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

public sealed class MaskHousingTests
{
    private const int W = 480;
    private const int H = 360;
    private const int BodyW = 220;
    private const int BodyH = 56;
    private const int TabW = 44;
    private const int TabH = 18;

    [Fact]
    public void TabContour_CenterCloserToBodyThanFullObb()
    {
        var contour = BodyAndTabContour(tabOnPlusShort: true);
        var full = Cv2.MinAreaRect(contour);
        var housing = MaskHousing.Fit(contour);
        Assert.True(full.Center.Y > H / 2.0 + 2, $"带凸起的外接矩形应下移，实际 {full.Center.Y:0.1}");
        Assert.True(
            Math.Abs(housing.Center.Y - H / 2.0) < Math.Abs(full.Center.Y - H / 2.0) - 0.5,
            $"壳体中心应比整段外接矩形更靠近壳体，housingY={housing.Center.Y:0.1} fullY={full.Center.Y:0.1}");
    }

    [Fact]
    public void SymmetricRect_Unchanged()
    {
        Point2f[] contour =
        [
            new(100, 100), new(300, 100), new(300, 160), new(100, 160),
        ];
        var housing = MaskHousing.Fit(contour);
        Assert.InRange(housing.Center.X, 198, 202);
        Assert.InRange(housing.Center.Y, 128, 132);
    }

    [Fact]
    public void FatFilledRect_OccupancyDoesNotShrinkHousing()
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        var expand = 22.0;
        var hh = BodyH / 2.0 + expand / 2.0;
        var cy = H / 2.0 + expand / 2.0;
        Cv2.FillConvexPoly(mask, RectCorners(W / 2.0, cy, BodyW / 2.0, hh), Scalar.All(255));
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();
        var obb = MaskHousing.FitObb(contour);
        var housing = MaskHousing.Fit(contour);
        Assert.InRange(housing.ShortLen, obb.ShortLen * 0.92, obb.ShortLen * 1.08);
        Assert.InRange(housing.Center.Y, obb.Center.Y - 3, obb.Center.Y + 3);
    }

    [Fact]
    public void ProbeCount_ScalesWithLength()
    {
        Assert.Equal(8, MaskHousing.ProbeCount(50));
        Assert.InRange(MaskHousing.ProbeCount(220), 14, 18);
        Assert.Equal(24, MaskHousing.ProbeCount(800));
    }

    [Fact]
    public void AdaptiveRange_ShrinksForSlenderHousing()
    {
        var slender = new HousingFrame(new Point2f(0, 0), 0, 200, 40);
        Assert.Equal(3, MaskHousing.AdaptiveRefineRange(5, slender));
        var square = new HousingFrame(new Point2f(0, 0), 0, 80, 70);
        Assert.Equal(8, MaskHousing.AdaptiveRefineRange(8, square));
    }

    [Fact]
    public void Fit_NegativeSlenderBar_CanonWarpInMinus90To90()
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        var cx = W / 2.0;
        var cy = H / 2.0;
        var rad = -37.0 * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2f Rot(double x, double y) => new(
            (float)(cx + x * cos - y * sin),
            (float)(cy + x * sin + y * cos));
        Point2f[] contour =
        [
            Rot(-110, -28), Rot(110, -28), Rot(110, 28), Rot(-110, 28),
        ];
        Cv2.FillConvexPoly(mask, contour.Select(p => new Point((int)p.X, (int)p.Y)).ToArray(), Scalar.All(255));
        Cv2.FindContours(mask, out var found, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var pts = found.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();
        var housing = MaskHousing.Fit(pts);
        Assert.InRange(housing.WarpAngleDeg, -42, -32);
        Assert.True(housing.WarpAngleDeg > -90 && housing.WarpAngleDeg < 90);
    }

    private static Point2f[] BodyAndTabContour(bool tabOnPlusShort)
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        Cv2.FillConvexPoly(mask, RectCorners(W / 2.0, H / 2.0, BodyW / 2.0, BodyH / 2.0), Scalar.All(255));
        var cy = H / 2.0 + (tabOnPlusShort ? BodyH / 2.0 + TabH / 2.0 : -(BodyH / 2.0 + TabH / 2.0));
        Cv2.FillConvexPoly(mask, RectCorners(W / 2.0, cy, TabW / 2.0, TabH / 2.0), Scalar.All(255));
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        return contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();
    }

    private static Point[] RectCorners(double cx, double cy, double hw, double hh) =>
    [
        new((int)Math.Round(cx - hw), (int)Math.Round(cy - hh)),
        new((int)Math.Round(cx + hw), (int)Math.Round(cy - hh)),
        new((int)Math.Round(cx + hw), (int)Math.Round(cy + hh)),
        new((int)Math.Round(cx - hw), (int)Math.Round(cy + hh)),
    ];
}
