using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 精度基线回归（合成基准，2026-09-02 实测标定）：在已知 ground truth 场景断言
/// 两匹配器的可达精度，防角度/XY 精度回归。阈值取实测值 1.5~2× 留裕量。
/// 注意：这是合成图案基线（非规格书），真机产品图标定后按需收紧。
/// </summary>
public sealed class PrecisionBaselineTests
{
    private const double TplMinScore = 0.3;

    // ── 模板匹配（NCC）──
    // 实测（rng=5 小残差）：0°→0.000、1.3°→0.427、3.2°→0.150；XY 0.01~0.56px。
    [Theory]
    [InlineData(0.0, 0.20)]
    [InlineData(1.3, 0.60)]
    [InlineData(3.2, 0.60)]
    public void TemplateMatch_AngleBaseline(double deg, double maxErrDeg)
    {
        using var template = PaintTemplate();
        using var upright = MakeUpright(template, deg);
        var m = MaskTemplateMatcher.MatchBest(upright, template, refineRangeDeg: 5, minScore: TplMinScore);
        Assert.True(m is not null, $"TPL deg={deg:0.0} 未命中");
        var err = Math.Abs(NormalizeSigned(m!.RotationDeg - deg));
        Assert.True(err <= maxErrDeg,
            $"TPL deg={deg:0.0}:得 {m.RotationDeg:0.000} 误差 {err:0.000}°(门限 {maxErrDeg:0.00}) NCC={m.Score:0.000}");
    }

    [Theory]
    [InlineData(0.3, -0.2)]
    [InlineData(1.5, 1.0)]
    [InlineData(3.7, -2.4)]
    public void TemplateMatch_XyBaseline(double dx, double dy)
    {
        using var template = PaintTemplate();
        using var shifted = ShiftTemplate(template, dx, dy);
        var m = MaskTemplateMatcher.MatchBest(shifted, template, refineRangeDeg: 1, minScore: TplMinScore,
            orientationBranchDeg: 0);
        Assert.True(m is not null, $"TPL xy=({dx},{dy}) 未命中");
        var c = m!.CenterInUpright;
        var ex = c.X - (Canvas / 2.0 + dx);
        var ey = c.Y - (Canvas / 2.0 + dy);
        Assert.True(Math.Abs(ex) <= 0.8 && Math.Abs(ey) <= 0.8,
            $"TPL xy=({dx},{dy}):误差 ({ex:+0.00},{ey:+0.00})px(门限 ±0.8)");
    }

    // ── 形状匹配（有向 Chamfer）──
    // 实测：恒等 rng=3 → 0.30°(hit 1.00/均距 0.17px)；平移在像素网格精确恢复
    // (Pose 中心=边缘质心,与 housing 中心恒差常量,基线校正后误差≤0.75px)。
    [Fact]
    public void ShapeMatch_IdentityAngleBaseline()
    {
        using var teach = Paint(0);
        var model = Teach(teach, Contour(0));
        using var img = Paint(0);
        var attempt = MaskShapeMatch.TryRefine(img, Contour(0), model, refineRangeDeg: 3, noFlip: true);
        Assert.True(attempt.Pose is not null,
            $"恒等未过门 hit={MaskShapeMatch.LastDebug.HitRate:0.00} mean={MaskShapeMatch.LastDebug.MeanDist:0.00}");
        var err = Math.Abs(NormalizeSigned(attempt.Pose!.AngleDeg));
        Assert.True(err <= 1.0,
            $"恒等角度误差 {err:0.000}°(门限 1.0°) hit={attempt.Pose!.HitRate:0.00}");
    }

    [Theory]
    [InlineData(1.2, -0.8)]
    [InlineData(3.5, 2.0)]
    [InlineData(8.0, -5.0)]
    public void ShapeMatch_TranslationBaseline(double dx, double dy)
    {
        using var teach = Paint(0);
        var model = Teach(teach, Contour(0));
        // 基线:0 偏移下 Pose 中心相对 housing 中心的常量差(质心 vs 矩形中心定义差)
        using var img0 = Paint(0);
        var baseAttempt = MaskShapeMatch.TryRefine(img0, Contour(0), model, refineRangeDeg: 3, noFlip: true);
        Assert.True(baseAttempt.Pose is not null, "0 偏移基线未过门");
        var baseHousing = MaskHousing.Fit(Contour(0)).Center;
        var baseDelta = new Point2d(baseAttempt.Pose!.Center.X - baseHousing.X, baseAttempt.Pose.Center.Y - baseHousing.Y);

        using var img = Paint(0, dx, dy);
        var contour = Contour(0, dx, dy);
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 3, noFlip: true);
        Assert.True(attempt.Pose is not null, $"xy=({dx},{dy}) 未过门");
        var hc = MaskHousing.Fit(contour).Center;
        var errX = Math.Abs((attempt.Pose!.Center.X - hc.X) - baseDelta.X);
        var errY = Math.Abs((attempt.Pose.Center.Y - hc.Y) - baseDelta.Y);
        Assert.True(errX <= 0.75 && errY <= 0.75,
            $"xy=({dx},{dy}) 平移误差 ({errX:+0.00},{errY:+0.00})px(门限 ±0.75,基线校正后)");
    }

    // ── helper（与 MaskShapeMatchTests / MaskTemplateMatchTests 同法）──
    private const int W = 480, H = 360;

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

    private static Mat Paint(double rotateDeg) => Paint(rotateDeg, 0, 0);

    private static Mat Paint(double rotateDeg, double offsetX, double offsetY)
    {
        var img = new Mat(H, W, MatType.CV_8UC1, new Scalar(24));
        FillPart(img, rotateDeg, 210, offsetX, offsetY);
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
        return img;
    }

    private static Point2f[] Contour(double rotateDeg) => Contour(rotateDeg, 0, 0);

    private static Point2f[] Contour(double rotateDeg, double offsetX, double offsetY)
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        FillPart(mask, rotateDeg, 255, offsetX, offsetY);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        return contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();
    }

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

    // ── 模板图案 helper ──
    private const int TplW = 160, TplH = 64;
    private const int Canvas = 400;

    private static Mat PaintTemplate()
    {
        var mat = new Mat(TplH, TplW, MatType.CV_8UC3, new Scalar(55, 55, 55));
        for (var x = 10; x < TplW - 10; x += 12)
            Cv2.Line(mat, new Point(x, 8), new Point(x, TplH - 8), new Scalar(150, 150, 150), 2);
        Cv2.Circle(mat, new Point(TplW - 22, TplH / 2), 10, new Scalar(40, 90, 220), -1);
        Cv2.Rectangle(mat, new Point(6, 6), new Point(28, TplH - 6), new Scalar(30, 30, 30), -1);
        return mat;
    }

    private static Mat MakeUpright(Mat template, double objectDeg)
    {
        using var full = new Mat(Canvas, Canvas, MatType.CV_8UC3, new Scalar(55, 55, 55));
        var px = (Canvas - template.Width) / 2;
        var py = (Canvas - template.Height) / 2;
        template.CopyTo(full[new Rect(px, py, template.Width, template.Height)]);
        using var rotated = new Mat();
        if (Math.Abs(objectDeg) < 1e-9) full.CopyTo(rotated);
        else
        {
            var center = new Point2f(Canvas / 2f, Canvas / 2f);
            using var m = Cv2.GetRotationMatrix2D(center, objectDeg, 1.0);
            Cv2.WarpAffine(full, rotated, m, new Size(Canvas, Canvas), InterpolationFlags.Linear,
                BorderTypes.Constant, new Scalar(55, 55, 55));
        }
        var cropW = (int)Math.Ceiling(template.Width * 1.3);
        var cropH = (int)Math.Ceiling(template.Height * 1.3);
        var x = (Canvas - cropW) / 2;
        var y = (Canvas - cropH) / 2;
        return rotated[new Rect(x, y, cropW, cropH)].Clone();
    }

    private static Mat ShiftTemplate(Mat template, double dx, double dy)
    {
        using var full = new Mat(Canvas, Canvas, MatType.CV_8UC3, new Scalar(55, 55, 55));
        var px = (Canvas - template.Width) / 2;
        var py = (Canvas - template.Height) / 2;
        template.CopyTo(full[new Rect(px, py, template.Width, template.Height)]);
        using var m = new Mat(2, 3, MatType.CV_64FC1);
        m.SetArray(new double[] { 1, 0, dx, 0, 1, dy });
        var shifted = new Mat();
        Cv2.WarpAffine(full, shifted, m, new Size(Canvas, Canvas), InterpolationFlags.Linear,
            BorderTypes.Constant, new Scalar(55, 55, 55));
        return shifted;
    }

    private static double NormalizeSigned(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }
}
