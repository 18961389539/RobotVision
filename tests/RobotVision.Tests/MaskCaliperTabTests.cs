using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// 卡尺长边 + 凸起极性：合成亮底暗条 + 单侧凸起，验证方向、短轴中心不跟肥掩码走、失败回退。
/// </summary>
public sealed class MaskCaliperTabTests(ITestOutputHelper output)
{
    private const int W = 480;
    private const int H = 360;
    private const int BodyW = 220;
    private const int BodyH = 56;
    private const int TabW = 44;
    private const int TabH = 18;

    [Fact]
    public void TabBelow_AngleNearZero_CenterOnBody()
    {
        using var img = Paint(tabOnPlusShort: true, rotateDeg: 0);
        var contour = AccurateContour(tabOnPlusShort: true, rotateDeg: 0);
        var r = MaskCaliperTab.Refine(img, contour);
        Assert.NotNull(r);
        Dump("tab-below 0°", r);
        Assert.Equal(1, r.TabSign);
        Assert.InRange(r.AngleDeg, -1.0, 1.0);
        Assert.InRange(r.Center.X, W / 2.0 - 3, W / 2.0 + 3);
        Assert.InRange(r.Center.Y, H / 2.0 - 3, H / 2.0 + 3);
        Dump("tab-below 0°", r);
    }

    [Fact]
    public void TabAbove_Adds180()
    {
        using var img = Paint(tabOnPlusShort: false, rotateDeg: 0);
        var contour = AccurateContour(tabOnPlusShort: false, rotateDeg: 0);
        var r = MaskCaliperTab.Refine(img, contour);
        Assert.NotNull(r);
        Assert.Equal(-1, r.TabSign);
        var a = Math.Abs(r.AngleDeg);
        Assert.True(a > 170, $"凸起在上应≈±180°，实际 {r.AngleDeg:0.00}");
        Dump("tab-above", r);
    }

    [Theory]
    [InlineData(12.0)]
    [InlineData(-18.0)]
    [InlineData(37.0)]
    public void RotatedTabBelow_FollowsAngle(double deg)
    {
        using var img = Paint(tabOnPlusShort: true, rotateDeg: deg);
        var contour = AccurateContour(tabOnPlusShort: true, rotateDeg: deg);
        var r = MaskCaliperTab.Refine(img, contour);
        Assert.NotNull(r);
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - deg));
        Assert.True(err < 0.8, $"角度误差 {err:0.00}°（得 {r.AngleDeg:0.00}，目标 {deg}）");
        Assert.InRange(r.Center.X, W / 2.0 - 4, W / 2.0 + 4);
        Assert.InRange(r.Center.Y, H / 2.0 - 4, H / 2.0 + 4);
        Dump($"rot {deg}°", r);
    }

    [Fact]
    public void FatMaskTowardTab_CenterStaysOnBodyNotMask()
    {
        using var img = Paint(tabOnPlusShort: true, rotateDeg: 0);
        var fat = FatContourTowardPlusShort(expandPx: 22, rotateDeg: 0);
        var coarse = Cv2.MinAreaRect(fat);
        var r = MaskCaliperTab.Refine(img, fat);
        Assert.NotNull(r);
        // 肥掩码把凸起侧扩出去，外接矩形中心沿 +Y 偏；卡尺短轴应仍停在壳体中缝
        Assert.True(coarse.Center.Y > H / 2.0 + 6,
            $"肥掩码中心应明显下移，实际 Y={coarse.Center.Y:0.1}");
        Assert.InRange(r.Center.Y, H / 2.0 - 3, H / 2.0 + 3);
        Assert.True(Math.Abs(r.Center.Y - coarse.Center.Y) > 5,
            "卡尺中心不应等于分割外接矩形中心");
        Dump($"fat mask minAreaY={coarse.Center.Y:0.1}", r);
    }

    [Fact]
    public void CoarseAngleOff_StillRecovers()
    {
        const double trueDeg = 8.0;
        using var img = Paint(tabOnPlusShort: true, rotateDeg: trueDeg);
        var contour = AccurateContour(tabOnPlusShort: true, rotateDeg: trueDeg + 4.0);
        var r = MaskCaliperTab.Refine(img, contour);
        Assert.NotNull(r);
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - trueDeg));
        Assert.True(err < 1.2, $"粗角偏 4° 后误差 {err:0.00}°（得 {r.AngleDeg:0.00}）");
        Dump("coarse +4°", r);
    }

    [Fact]
    public void InnerHole_KeepsOuterWidth()
    {
        using var img = Paint(tabOnPlusShort: true, rotateDeg: 0);
        // 壳体内挖亮孔：若取最强梯度会锁内壁，宽度会明显小于 BodyH
        Cv2.Circle(img, new Point(W / 2, H / 2), 12, new Scalar(230), -1);
        var r = MaskCaliperTab.Refine(img, AccurateContour(tabOnPlusShort: true, rotateDeg: 0));
        Assert.NotNull(r);
        Assert.InRange(MaskCaliperTab.LastDebug.WidthPx, BodyH - 8, BodyH + 8);
        Dump("inner hole", r);
    }

    [Fact]
    public void NoEdges_ReturnsNull()
    {
        using var blank = new Mat(H, W, MatType.CV_8UC1, new Scalar(240));
        var contour = AccurateContour(tabOnPlusShort: true, rotateDeg: 0);
        Assert.Null(MaskCaliperTab.Refine(blank, contour));
    }

    [Fact]
    public void TabBelowAndAbove_SameBodyCenter()
    {
        using var below = Paint(tabOnPlusShort: true, rotateDeg: 0);
        using var above = Paint(tabOnPlusShort: false, rotateDeg: 0);
        var r0 = MaskCaliperTab.Refine(below, AccurateContour(true, 0));
        var r1 = MaskCaliperTab.Refine(above, AccurateContour(false, 0));
        Assert.NotNull(r0);
        Assert.NotNull(r1);
        Assert.InRange(Math.Abs(r0.Center.X - r1.Center.X), 0, 2.5);
        Assert.InRange(Math.Abs(r0.Center.Y - r1.Center.Y), 0, 2.5);
        Assert.True(Math.Abs(AngleGeometry.NormalizeSignedDeg(r0.AngleDeg - r1.AngleDeg)) > 170);
    }

    private void Dump(string tag, MaskCaliperTab.Result r)
    {
        var d = MaskCaliperTab.LastDebug;
        output.WriteLine(
            $"{tag}: {r.Center.X:0.2},{r.Center.Y:0.2}  {r.AngleDeg:0.300}°  tab={r.TabSign}  " +
            $"probes={d.ValidProbes} par={d.ParallelDeg:0.03}° w={d.WidthPx:0.1} diff={d.TabGrayDiff:0.1}");
    }

    private static Mat Paint(bool tabOnPlusShort, double rotateDeg)
    {
        var img = new Mat(H, W, MatType.CV_8UC1, new Scalar(240));
        FillBodyAndTab(img, tabOnPlusShort, rotateDeg);
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.6);
        return img;
    }

    private static void FillBodyAndTab(Mat img, bool tabOnPlusShort, double rotateDeg)
    {
        Cv2.FillConvexPoly(img, BodyCorners(rotateDeg), new Scalar(75));
        Cv2.FillConvexPoly(img, TabCorners(tabOnPlusShort, rotateDeg), new Scalar(28));
    }

    private static Point[] BodyCorners(double deg) =>
        RectCorners(W / 2.0, H / 2.0, BodyW / 2.0, BodyH / 2.0, deg);

    private static Point[] TabCorners(bool onPlusShort, double deg)
    {
        var cy = H / 2.0 + (onPlusShort ? BodyH / 2.0 + TabH / 2.0 : -(BodyH / 2.0 + TabH / 2.0));
        return RectCorners(W / 2.0, cy, TabW / 2.0, TabH / 2.0, deg);
    }

    private static Point2f[] AccurateContour(bool tabOnPlusShort, double rotateDeg)
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        FillBodyAndTab(mask, tabOnPlusShort, rotateDeg);
        return LargestContour(mask);
    }

    /// <summary>只把 +短轴一侧（凸起侧）外扩，模拟分割把凸起画肥、外接矩形中心被拖偏。</summary>
    private static Point2f[] FatContourTowardPlusShort(double expandPx, double rotateDeg)
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        var hw = BodyW / 2.0;
        var hh = BodyH / 2.0 + expandPx / 2.0;
        var cy = H / 2.0 + expandPx / 2.0;
        Cv2.FillConvexPoly(mask, RectCorners(W / 2.0, cy, hw, hh, rotateDeg), Scalar.All(255));
        return LargestContour(mask);
    }

    private static Point2f[] LargestContour(Mat mask)
    {
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        Assert.True(contours.Length > 0);
        var best = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        return best.Select(p => new Point2f(p.X, p.Y)).ToArray();
    }

    private static Point[] RectCorners(double cx, double cy, double hw, double hh, double deg)
    {
        var ox = W / 2.0;
        var oy = H / 2.0;
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point Rot(double x, double y)
        {
            var dx = x - ox;
            var dy = y - oy;
            return new Point(
                (int)Math.Round(ox + dx * cos - dy * sin),
                (int)Math.Round(oy + dx * sin + dy * cos));
        }

        return
        [
            Rot(cx - hw, cy - hh),
            Rot(cx + hw, cy - hh),
            Rot(cx + hw, cy + hh),
            Rot(cx - hw, cy + hh),
        ];
    }
}
