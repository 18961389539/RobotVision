using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Vision;

namespace RobotVision.Tests.HalconBench;

/// <summary>形状匹配合成夹具：与 <c>MaskShapeMatchTests</c> 同图案，供 HALCON 级矩阵对标。</summary>
internal static class ShapeMatchBenchSynth
{
    public const int W = 480;
    public const int H = 360;

    /// <summary>对标/性能测试：关闭可视化分配，与产线默认可视化解耦。</summary>
    internal static readonly ShapeMatchOptions BenchOptions = new() { EnableVisualization = false };

    public static Mat Paint(double rotateDeg, double offsetX = 0, double offsetY = 0, double scale = 1)
    {
        using var master = PaintMaster();
        return WarpMaster(master, rotateDeg, offsetX, offsetY, scale, InterpolationFlags.Cubic, new Scalar(24));
    }

    /// <summary>0° 主图：整数顶点只画一次，旋转/平移/尺度用 WarpAffine，避免每角栅格化抖动。</summary>
    private static Mat PaintMaster()
    {
        var img = new Mat(H, W, MatType.CV_8UC1, new Scalar(24));
        FillPart(img, 0, 210, 0, 0, 1);
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
        return img;
    }

    private static Mat WarpMaster(
        Mat master, double rotateDeg, double offsetX, double offsetY, double scale,
        InterpolationFlags interp, Scalar border)
    {
        var s = Math.Clamp(scale, 0.85, 1.15);
        var identity = Math.Abs(rotateDeg) < 1e-12 && Math.Abs(offsetX) < 1e-12
                       && Math.Abs(offsetY) < 1e-12 && Math.Abs(s - 1.0) < 1e-12;
        if (identity)
            return master.Clone();

        var cx = W / 2.0;
        var cy = H / 2.0;
        var pivot = new Point2f((float)(cx + offsetX), (float)(cy + offsetY));
        using var m = Cv2.GetRotationMatrix2D(pivot, -rotateDeg, 1.0 / s);
        if (Math.Abs(offsetX) > 1e-12 || Math.Abs(offsetY) > 1e-12)
        {
            m.Set(0, 2, m.At<double>(0, 2) - offsetX);
            m.Set(1, 2, m.At<double>(1, 2) - offsetY);
        }

        var img = new Mat();
        Cv2.WarpAffine(master, img, m, new Size(W, H), interp, BorderTypes.Constant, border);
        return img;
    }

    /// <summary>在转正窗 margin 内叠加与长边正交的竖条（不在分割轮廓上）。</summary>
    public static void PaintOrthogonalBarInMargin(Mat img, double sceneDeg, byte barValue = 210)
    {
        var cx = W / 2.0;
        var cy = H / 2.0;
        var barCx = cx + 88;
        var barCy = cy;
        Cv2.FillConvexPoly(img, RectCorners(barCx, barCy, 9, 58, sceneDeg + 90, cx, cy), new Scalar(barValue));
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
    }

    /// <summary>在转正窗 margin 内叠加与长边平行的横条（不在分割轮廓上）。</summary>
    public static void PaintParallelBarInMargin(Mat img, double sceneDeg, byte barValue = 210)
    {
        var cx = W / 2.0;
        var cy = H / 2.0 + 42;
        Cv2.FillConvexPoly(img, RectCorners(cx, cy, 52, 9, sceneDeg, cx, cy), new Scalar(barValue));
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
    }

    public static Point2f[] Contour(double rotateDeg, double offsetX = 0, double offsetY = 0, double scale = 1)
    {
        var master = ContourMaster();
        return TransformContour(master, rotateDeg, offsetX, offsetY, scale);
    }

    private static Point2f[]? _contourMaster;

    private static Point2f[] ContourMaster()
    {
        if (_contourMaster is not null)
            return _contourMaster;
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        FillPart(mask, 0, 255, 0, 0, 1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        _contourMaster = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();
        return _contourMaster;
    }

    /// <summary>与 <see cref="Paint"/> 同一刚体：绕零件中心旋转/平移/尺度，避免再栅格化轮廓。</summary>
    private static Point2f[] TransformContour(
        Point2f[] src, double rotateDeg, double offsetX, double offsetY, double scale)
    {
        var s = Math.Clamp(scale, 0.85, 1.15);
        var identity = Math.Abs(rotateDeg) < 1e-12 && Math.Abs(offsetX) < 1e-12
                       && Math.Abs(offsetY) < 1e-12 && Math.Abs(s - 1.0) < 1e-12;
        if (identity)
            return (Point2f[])src.Clone();

        var cx = W / 2.0;
        var cy = H / 2.0;
        var px = cx + offsetX;
        var py = cy + offsetY;
        var rad = rotateDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dst = new Point2f[src.Length];
        for (var i = 0; i < src.Length; i++)
        {
            var dx = (src[i].X - cx) * s;
            var dy = (src[i].Y - cy) * s;
            dst[i] = new Point2f(
                (float)(px + dx * cos - dy * sin),
                (float)(py + dx * sin + dy * cos));
        }

        return dst;
    }

    public static MaskShapeMatch.ShapeModel? TeachFromZero(Mat teachImg, Point2f[] teachContour) =>
        Teach(teachImg, teachContour);

    /// <summary>与运行时 <see cref="MaskShapeMatch"/> 同 margin 示教。</summary>
    public static MaskShapeMatch.ShapeModel? Teach(Mat teachImg, Point2f[] teachContour)
    {
        var crop = MaskTemplateMatcher.UprightCrop(teachImg, teachContour, MaskShapeMatch.CropMarginRatio);
        try
        {
            return MaskShapeMatch.BuildTeach(crop, teachContour);
        }
        finally
        {
            crop.Upright.Dispose();
        }
    }

    /// <summary>导出 HALCON bench 用 PNG（teach_0 + live_{deg} 转正窗）。</summary>
    public static void ExportHalconFixtures(string directory)
    {
        Directory.CreateDirectory(directory);
        using var teachImg = Paint(0);
        var teachContour = Contour(0);
        var teachCrop = MaskTemplateMatcher.UprightCrop(teachImg, teachContour, MaskShapeMatch.CropMarginRatio);
        using (teachCrop.Upright)
            Cv2.ImWrite(Path.Combine(directory, "teach_0.png"), teachCrop.Upright);

        foreach (var deg in new[] { -37.0, -20.0, -8.7, 0.0, 8.7, 20.0, 37.0, 180.0 })
        {
            using var img = Paint(deg);
            var contour = Contour(deg);
            var crop = MaskTemplateMatcher.UprightCrop(img, contour, MaskShapeMatch.CropMarginRatio);
            using (crop.Upright)
            {
                var name = deg.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                Cv2.ImWrite(Path.Combine(directory, $"live_{name}.png"), crop.Upright);
            }
        }
    }

    public static (bool Ok, double AngleErrDeg, double HitRate, double MeanDistPx, double Score) RefineAngleErr(
        Mat scene, Point2f[] contour, MaskShapeMatch.ShapeModel model, double trueDeg,
        double refineRangeDeg = 12, bool noFlip = false)
    {
        var attempt = MaskShapeMatch.TryRefine(scene, contour, model, refineRangeDeg, noFlip, BenchOptions);
        if (attempt.Pose is not { } pose)
            return (false, double.NaN, MaskShapeMatch.LastDebug.HitRate, MaskShapeMatch.LastDebug.MeanDist, double.NaN);
        var err = Math.Abs(NormalizeSigned(pose.AngleDeg - trueDeg));
        return (true, err, pose.HitRate, pose.MeanDistPx, pose.Score);
    }

    /// <summary>相对分割壳体中心的欧氏距离（流水线位姿原点）。</summary>
    public static double RefineCenterErr(
        Mat scene, Point2f[] contour, MaskShapeMatch.ShapeModel model, double trueDeg, bool noFlip)
    {
        var attempt = MaskShapeMatch.TryRefine(scene, contour, model, refineRangeDeg: 8, noFlip, BenchOptions);
        if (attempt.Pose is not { } pose)
            return double.PositiveInfinity;
        var housing = MaskHousing.Fit(contour);
        var dx = pose.Center.X - housing.Center.X;
        var dy = pose.Center.Y - housing.Center.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>示教边点质心映到源图（形状模型原点）。</summary>
    public static Point2d TeachOriginSource(Mat teachImg, Point2f[] teachContour, MaskShapeMatch.ShapeModel model)
    {
        var crop = MaskTemplateMatcher.UprightCrop(teachImg, teachContour, MaskShapeMatch.CropMarginRatio);
        using (crop.Upright)
            return MaskShapeMatch.MapCropToSource(
                crop, new Point2d(model.CenterX, model.CenterY));
    }

    /// <summary>把示教原点绕合成件中心旋转到现场角。</summary>
    public static Point2d RotateAroundPaint(Point2d origin, double deg, double offsetX = 0, double offsetY = 0)
    {
        var cx = W / 2.0 + offsetX;
        var cy = H / 2.0 + offsetY;
        var rad = deg * Math.PI / 180.0;
        var dx = origin.X - cx;
        var dy = origin.Y - cy;
        return new Point2d(
            cx + dx * Math.Cos(rad) - dy * Math.Sin(rad),
            cy + dx * Math.Sin(rad) + dy * Math.Cos(rad));
    }

    /// <summary>现场壳体 + 示教原点相对壳体的旋转偏移（与输出映射同口径）。</summary>
    public static Point2d ExpectedTeachOrigin(
        Point2d teachOrigin, Point2f teachHousing, Point2f[] liveContour, double deg)
    {
        var liveH = MaskHousing.Fit(liveContour).Center;
        var dx = teachOrigin.X - teachHousing.X;
        var dy = teachOrigin.Y - teachHousing.Y;
        var rad = deg * Math.PI / 180.0;
        return new Point2d(
            liveH.X + dx * Math.Cos(rad) - dy * Math.Sin(rad),
            liveH.Y + dx * Math.Sin(rad) + dy * Math.Cos(rad));
    }

    /// <summary>相对旋转后示教原点的欧氏距离（独立于壳体拟合）。</summary>
    public static double RefineCenterErrVsTeachOrigin(
        Mat scene, Point2f[] contour, MaskShapeMatch.ShapeModel model,
        Point2d expectedOrigin, bool noFlip)
    {
        var attempt = MaskShapeMatch.TryRefine(scene, contour, model, refineRangeDeg: 8, noFlip, BenchOptions);
        if (attempt.Pose is not { } pose)
            return double.PositiveInfinity;
        var dx = pose.Center.X - expectedOrigin.X;
        var dy = pose.Center.Y - expectedOrigin.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double NormalizeSigned(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }

    /// <summary>现场图亮度/对比扰动（分割轮廓仍来自几何）。</summary>
    public static void ApplyGainBias(Mat img, double gain, double bias)
    {
        img.ConvertTo(img, MatType.CV_8UC1, gain, bias);
    }

    /// <summary>确定性高斯噪声（σ 灰度）。</summary>
    public static void AddGaussianNoise(Mat img, double sigma, int seed)
    {
        var rng = new Random(seed);
        var indexer = img.GetGenericIndexer<byte>();
        for (var y = 0; y < img.Rows; y++)
        for (var x = 0; x < img.Cols; x++)
        {
            var u1 = 1.0 - rng.NextDouble();
            var u2 = rng.NextDouble();
            var z = Math.Sqrt(-2.0 * Math.Log(Math.Max(1e-12, u1))) * Math.Cos(2 * Math.PI * u2);
            indexer[y, x] = (byte)Math.Clamp(indexer[y, x] + z * sigma, 0, 255);
        }
    }

    /// <summary>轻微剪切变形（x' = x + k(y-cy)），模拟非刚体/夹持变形。</summary>
    public static void ApplyShear(Mat img, double shearX)
    {
        var cy = img.Height / 2.0;
        using var m = new Mat(2, 3, MatType.CV_64FC1, Scalar.All(0));
        m.Set(0, 0, 1.0);
        m.Set(0, 1, shearX);
        m.Set(0, 2, -shearX * cy);
        m.Set(1, 0, 0.0);
        m.Set(1, 1, 1.0);
        m.Set(1, 2, 0.0);
        Cv2.WarpAffine(img, img, m, img.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);
    }

    public static Point2f[] ContourSheared(double rotateDeg, double shearX)
    {
        var pts = Contour(rotateDeg);
        var cy = H / 2.0;
        for (var i = 0; i < pts.Length; i++)
            pts[i] = new Point2f((float)(pts[i].X + shearX * (pts[i].Y - cy)), pts[i].Y);
        return pts;
    }

    public static void PaintOcclusion(Mat img, double sceneDeg, byte fill = 24)
    {
        var cx = W / 2.0;
        var cy = H / 2.0;
        Cv2.FillConvexPoly(img, RectCorners(cx - 36, cy, 22, 16, sceneDeg, cx, cy), new Scalar(fill));
    }

    private static void FillPart(Mat img, double rotateDeg, byte value, double offsetX = 0, double offsetY = 0, double scale = 1)
    {
        var s = Math.Clamp(scale, 0.85, 1.15);
        var cx = W / 2.0 + offsetX;
        var cy = H / 2.0 + offsetY;
        Cv2.FillConvexPoly(img, RectCorners(cx, cy, 110 * s, 28 * s, rotateDeg, cx, cy), new Scalar(value));
        var notch = RectCorners(cx - 70 * s, cy - 18 * s, 18 * s, 12 * s, rotateDeg, cx, cy);
        Cv2.FillConvexPoly(img, notch, new Scalar(24));
        var c = RotatePoint(cx + 78 * s, cy + 18 * s, rotateDeg, cx, cy);
        Cv2.Circle(img, c, Math.Max(2, (int)Math.Round(11 * s)), new Scalar(value), -1);
        var c2 = RotatePoint(cx + 40 * s, cy - 8 * s, rotateDeg, cx, cy);
        Cv2.Circle(img, c2, Math.Max(2, (int)Math.Round(6 * s)), new Scalar(40), -1);
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
