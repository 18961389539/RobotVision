using OpenCvSharp;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 旋转矩形鲁棒拟合基准（对标 HALCON fit_rectangle2_contour_xld）。
/// 阈值取合成 ground truth 上实测裕量，防精度/鲁棒性回归。
/// </summary>
public sealed class RotatedRectFitterTests
{
    private const int W = 480;
    private const int H = 360;

    [Theory]
    [InlineData(0)]
    [InlineData(17.3)]
    [InlineData(45)]
    [InlineData(88)]
    [InlineData(133)]
    public void AxisAlignedRect_RecoversGeometry(double angleDeg)
    {
        var contour = RotatedRectContour(W / 2.0, H / 2.0, 200, 50, angleDeg, samplesPerEdge: 40);
        var fit = RotatedRectFitter.Fit(contour);
        Assert.True(fit.Ok);
        Assert.InRange(fit.Center.X, W / 2.0 - 1.5, W / 2.0 + 1.5);
        Assert.InRange(fit.Center.Y, H / 2.0 - 1.5, H / 2.0 + 1.5);
        Assert.InRange(fit.LongLen, 198, 202);
        Assert.InRange(fit.ShortLen, 48, 52);
        var err = UndirectedErr(fit.AngleDeg, angleDeg);
        Assert.True(err < 0.5, $"角误差 {err:0.000}° @ {angleDeg}°");
    }

    [Fact]
    public void NoisyContour_WithinSubPixelRms()
    {
        var rng = new Random(42);
        var basePts = RotatedRectContour(W / 2.0, H / 2.0, 180, 44, 22.0, 50);
        var noisy = basePts.Select(p =>
        {
            var nx = (float)(p.X + Gaussian(rng) * 0.8);
            var ny = (float)(p.Y + Gaussian(rng) * 0.8);
            return new Point2f(nx, ny);
        }).ToArray();
        var fit = RotatedRectFitter.Fit(noisy);
        Assert.True(fit.Ok);
        Assert.True(fit.RmsPx < 1.2, $"RMS={fit.RmsPx:0.00}px");
        Assert.True(UndirectedErr(fit.AngleDeg, 22.0) < 0.4);
    }

    [Fact]
    public void TabOutliers_AfterCorePoints_CenterCloserToBodyThanMinAreaRect()
    {
        var contour = BodyAndTabContour(tabOnPlusShort: true);
        var core = MaskHousing.CorePoints(contour);
        var obb = Cv2.MinAreaRect(contour);
        var fit = RotatedRectFitter.Fit(core);
        Assert.True(fit.Ok);
        Assert.True(
            Math.Abs(fit.Center.Y - H / 2.0) < Math.Abs(obb.Center.Y - H / 2.0) - 0.5,
            $"鲁棒拟合中心应更接近壳体 fitY={fit.Center.Y:0.1} obbY={obb.Center.Y:0.1}");
        var housing = MaskHousing.Fit(contour);
        Assert.InRange(housing.Center.Y, fit.Center.Y - 2, fit.Center.Y + 2);
    }

    [Fact]
    public void ClipEndPoints_ReducesCornerOutlierPull()
    {
        var contour = RotatedRectContour(W / 2.0, H / 2.0, 160, 40, 5.0, 30).ToList();
        // 四角各加 3 个离群点（模拟圆角/毛刺）
        foreach (var c in CornerOutliers(W / 2.0, H / 2.0, 160, 40, 5.0))
            contour.AddRange(c);

        var plain = RotatedRectFitter.Fit(contour, clipEndPoints: 0);
        var clipped = RotatedRectFitter.Fit(contour, clipEndPoints: 2);
        Assert.True(plain.Ok && clipped.Ok);
        Assert.True(clipped.RmsPx <= plain.RmsPx + 0.05,
            $"clip 应降低 RMS plain={plain.RmsPx:0.00} clip={clipped.RmsPx:0.00}");
        Assert.True(UndirectedErr(clipped.AngleDeg, 5.0) <= UndirectedErr(plain.AngleDeg, 5.0) + 0.15);
    }

    [Fact]
    public void TooFewPoints_ReturnsNotOk()
    {
        Point2f[] tiny = [new(0, 0), new(10, 0), new(10, 5)];
        var fit = RotatedRectFitter.Fit(tiny);
        Assert.False(fit.Ok);
    }

    private static double UndirectedErr(double got, double truth)
    {
        var d = Math.Abs(got - truth);
        return Math.Min(d, 180 - d);
    }

    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private static Point2f[] RotatedRectContour(
        double cx, double cy, double longLen, double shortLen, double deg, int samplesPerEdge)
    {
        var hw = longLen / 2.0;
        var hh = shortLen / 2.0;
        var corners = new[]
        {
            new Point2d(cx - hw, cy - hh), new Point2d(cx + hw, cy - hh),
            new Point2d(cx + hw, cy + hh), new Point2d(cx - hw, cy + hh),
        };
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2d Rot(Point2d p) => new(
            cx + (p.X - cx) * cos - (p.Y - cy) * sin,
            cy + (p.X - cx) * sin + (p.Y - cy) * cos);

        var rotated = corners.Select(Rot).ToArray();
        var pts = new List<Point2f>();
        for (var e = 0; e < 4; e++)
        {
            var a = rotated[e];
            var b = rotated[(e + 1) % 4];
            for (var s = 0; s < samplesPerEdge; s++)
            {
                var t = (double)s / samplesPerEdge;
                pts.Add(new Point2f(
                    (float)(a.X + (b.X - a.X) * t),
                    (float)(a.Y + (b.Y - a.Y) * t)));
            }
        }
        return pts.ToArray();
    }

    private static IEnumerable<Point2f[]> CornerOutliers(double cx, double cy, double longLen, double shortLen, double deg)
    {
        var hw = longLen / 2.0;
        var hh = shortLen / 2.0;
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2f Corner(double lx, double ly)
        {
            var x = cx + lx * cos - ly * sin;
            var y = cy + lx * sin + ly * cos;
            return new Point2f((float)x, (float)y);
        }
        var corners = new[] { Corner(-hw, -hh), Corner(hw, -hh), Corner(hw, hh), Corner(-hw, hh) };
        foreach (var c in corners)
        {
            yield return
            [
                new(c.X + 3, c.Y + 2),
                new(c.X - 2, c.Y + 4),
                new(c.X + 1, c.Y - 3),
            ];
        }
    }

    private static Point2f[] BodyAndTabContour(bool tabOnPlusShort)
    {
        const int bodyW = 220;
        const int bodyH = 56;
        const int tabW = 44;
        const int tabH = 18;
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        Cv2.FillConvexPoly(mask, RectCorners(W / 2.0, H / 2.0, bodyW / 2.0, bodyH / 2.0), Scalar.All(255));
        var cy = H / 2.0 + (tabOnPlusShort ? bodyH / 2.0 + tabH / 2.0 : -(bodyH / 2.0 + tabH / 2.0));
        Cv2.FillConvexPoly(mask, RectCorners(W / 2.0, cy, tabW / 2.0, tabH / 2.0), Scalar.All(255));
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
