using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
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
    [InlineData(-37)]
    [InlineData(-20)]
    [InlineData(-8.7)]
    public void FollowsRotation_NegativeAngles(double deg)
    {
        using var teachImg = Paint(0);
        var teachContour = Contour(0);
        var model = Teach(teachImg, teachContour);
        Assert.NotNull(model);

        using var img = Paint(deg);
        var contour = Contour(deg);
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8, noFlip: true);
        Assert.True(attempt.Pose is not null,
            $"转 {deg}° 未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00}");
        var r = attempt.Pose!;
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - deg));
        var gate = deg is -37 or -20 ? 0.25 : 1.0;
        Assert.True(err < gate,
            $"转 {deg}°：得 {r.AngleDeg:0.00}，误差 {err:0.00}°(门 {gate})");
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
            $"转 {deg}° 未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00} 残差 {MaskShapeMatch.LastDebug.ResidualDeg:0.00}° 方向一致 {MaskShapeMatch.LastDebug.DirAgree:0.00} " +
            $"极性 {MaskShapeMatch.LastDebug.PolarTeach:0.0}/{MaskShapeMatch.LastDebug.Polar0:0.0}/{MaskShapeMatch.LastDebug.Polar180:0.0}");
        var r = attempt.Pose!;
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - deg));
        Assert.True(err < 1.0,
            $"转 {deg}°：得 {r.AngleDeg:0.00}，误差 {err:0.00}° 命中 {r.HitRate:0.00} 均距 {r.MeanDistPx:0.00} 方向一致 {MaskShapeMatch.LastDebug.DirAgree:0.00} " +
            $"极性 {MaskShapeMatch.LastDebug.PolarTeach:0.0}/{MaskShapeMatch.LastDebug.Polar0:0.0}/{MaskShapeMatch.LastDebug.Polar180:0.0}");
        AssertNearPart(r.Center, contour);
    }

    [Theory]
    [InlineData(8.7)]
    [InlineData(20.0)]
    public void DefaultNoFlip_DoesNotMisfireTo180(double deg)
    {
        // 极性证据门回归:中小角度旋转使 polar0≈polar180 同号(探针落插值模糊区),
        // 旧逻辑 1e-6 噪声差误走翻转支 → 输出差 ~174°(曾实测 8.7°/20° 事故)。
        // 门控后应信任主窗,不得翻转到 deg+180 附近。
        using var teachImg = Paint(0);
        var model = Teach(teachImg, Contour(0));
        Assert.NotNull(model);
        Assert.True(Math.Abs(model.PolarDelta) >= 12, $"示教极性 {model.PolarDelta:0.0} 应显著");

        using var img = Paint(deg);
        var contour = Contour(deg);
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 3);
        Assert.True(attempt.Pose is not null,
            $"转 {deg}° 未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00}");
        var r = attempt.Pose!;
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - deg));
        Assert.True(err < 45.0,
            $"转 {deg}° 误翻转到 180° 支:得 {r.AngleDeg:0.00},误差 {err:0.00}°(应 <45) " +
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

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(-8.7)]
    [InlineData(-37)]
    public void IgnoresOrthogonalBar_InUprightCropMargin(double sceneDeg)
    {
        // 正交干扰：与长边成 90° 的亮条落在转正窗 margin 内（不在分割轮廓内）。
        // 有向 Chamfer 须靠方向 bin 区分平行/正交边，不得把角拉向 90°。
        using var teachImg = Paint(0);
        var model = Teach(teachImg, Contour(0));
        Assert.NotNull(model);

        using var img = Paint(sceneDeg);
        PaintOrthogonalBarInMargin(img, sceneDeg, barValue: 210);
        var contour = Contour(sceneDeg);
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8, noFlip: true);
        Assert.True(attempt.Pose is not null,
            $"deg={sceneDeg}° 未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00} 方向一致 {MaskShapeMatch.LastDebug.DirAgree:0.00}");
        var r = attempt.Pose!;
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - sceneDeg));
        Assert.True(err < 1.0,
            $"deg={sceneDeg}° 正交条干扰：得 {r.AngleDeg:0.00}° 误差 {err:0.00}° 方向一致 {MaskShapeMatch.LastDebug.DirAgree:0.00}");
        AssertNearPart(r.Center, contour);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void IgnoresParallelBar_InUprightCropMargin(double sceneDeg)
    {
        // 平行干扰：与长边同向的亮条落在 margin 内；无向 Chamfer 易被平行边吸走。
        using var teachImg = Paint(0);
        var model = Teach(teachImg, Contour(0));
        Assert.NotNull(model);

        using var img = Paint(sceneDeg);
        PaintParallelBarInMargin(img, sceneDeg, barValue: 210);
        var contour = Contour(sceneDeg);
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8, noFlip: true);
        Assert.True(attempt.Pose is not null,
            $"deg={sceneDeg}° 未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00}");
        var r = attempt.Pose!;
        var err = Math.Abs(AngleGeometry.NormalizeSignedDeg(r.AngleDeg - sceneDeg));
        Assert.True(err < 1.0,
            $"deg={sceneDeg}° 平行条干扰：得 {r.AngleDeg:0.00}° 误差 {err:0.00}°");
        AssertNearPart(r.Center, contour);
    }

    [Fact]
    public void EmptyTeach_ReturnsNull()
    {
        using var blank = new Mat(80, 80, MatType.CV_8UC1, Scalar.All(30));
        Assert.Null(MaskShapeMatch.BuildTeach(blank));
    }

    [Fact]
    public void ShapeModel_HasNonTrivialDirectionBins()
    {
        // 有向 Chamfer 前提：示教边点方向 bin 有效（非全无效 0xFF）且覆盖 >1 个方向
        // （不对称件横边/竖边/圆点弧边方向不同）。若全同向说明方向提取失效。
        using var img = Paint(0);
        var model = Teach(img, Contour(0));
        Assert.NotNull(model);
        var bins = model.DirBins;
        Assert.NotNull(bins);
        Assert.Equal(model.PointCount, bins.Length);
        var valid = bins.Count(b => b != 0xFF);
        var distinct = bins.Where(b => b != 0xFF).Distinct().Count();
        Assert.True(valid > model.PointCount * 0.5,
            $"示教方向有效点仅 {valid}/{model.PointCount}");
        Assert.True(distinct >= 2,
            $"示教方向应覆盖多 bin（横边+竖边+弧），实际仅 {distinct} 个不同方向");
    }

    [Fact]
    public void NoFlip_SkipsFlippedWindow_AndTrustsMain()
    {
        // 语义验证：noFlip=true 跳过翻转窗——0° 主窗即正确方向，noFlip 不影响正确匹配。
        using var teachImg = Paint(0);
        var model = Teach(teachImg, Contour(0));
        Assert.NotNull(model);
        using var img = Paint(0);
        var attempt = MaskShapeMatch.TryRefine(img, Contour(0), model, refineRangeDeg: 8, noFlip: true);
        Assert.True(attempt.Pose is not null,
            $"0°(noFlip) 未过门 命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00}");
        var r = attempt.Pose!;
        Assert.InRange(r.AngleDeg, -8.0, 8.0);
        AssertNearPart(r.Center, Contour(0));
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
        var crop = MaskTemplateMatcher.UprightCrop(img, contour, MaskShapeMatch.CropMarginRatio);
        try
        {
            var model = MaskShapeMatch.BuildTeach(crop, contour);
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

    /// <summary>在转正窗 margin 内画与长边正交的竖条（不在分割轮廓上）。</summary>
    private static void PaintOrthogonalBarInMargin(Mat img, double sceneDeg, byte barValue)
    {
        var cx = W / 2.0;
        var cy = H / 2.0;
        // 竖条中心略偏右，与横条长边正交，落在 margin 环带内
        var barCx = cx + 88;
        var barCy = cy;
        var hw = 9.0;
        var hh = 58.0;
        Cv2.FillConvexPoly(img, RectCorners(barCx, barCy, hw, hh, sceneDeg + 90, cx, cy), new Scalar(barValue));
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
    }

    /// <summary>在转正窗 margin 内画与长边平行的横条（不在分割轮廓上）。</summary>
    private static void PaintParallelBarInMargin(Mat img, double sceneDeg, byte barValue)
    {
        var cx = W / 2.0;
        var cy = H / 2.0 + 42;
        Cv2.FillConvexPoly(img, RectCorners(cx, cy, 52, 9, sceneDeg, cx, cy), new Scalar(barValue));
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
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

    [Fact]
    public void AlignToTeachOrigin_WithoutModel_UsesHousingCenter()
    {
        var contour = new[]
        {
            new Point2f(100, 40),
            new Point2f(300, 40),
            new Point2f(300, 120),
            new Point2f(100, 120),
        };
        var c = MaskShapeMatch.AlignToTeachOrigin(contour, 0, model: null);
        Assert.InRange(c.X, 198, 202);
        Assert.InRange(c.Y, 78, 82);
    }

    [Fact]
    public void AlignToTeachAngle_FusesLineFitWhenNearWarp()
    {
        var contour = DenseRect(200, 80, 100, 40, deg: 0.2);
        var a = MaskShapeMatch.AlignToTeachAngle(contour, candidateDeg: 0.85);
        Assert.InRange(a, -1.5, 1.5);
    }

    private static Point2f[] DenseRect(double cx, double cy, double hw, double hh, double deg)
    {
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2f Map(double x, double y) => new(
            (float)(cx + (x - cx) * cos - (y - cy) * sin),
            (float)(cy + (x - cx) * sin + (y - cy) * cos));
        var pts = new List<Point2f>();
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20.0;
            pts.Add(Map(cx - hw + 2 * hw * t, cy - hh));
            pts.Add(Map(cx - hw + 2 * hw * t, cy + hh));
            pts.Add(Map(cx - hw, cy - hh + 2 * hh * t));
            pts.Add(Map(cx + hw, cy - hh + 2 * hh * t));
        }

        return [.. pts];
    }
}
