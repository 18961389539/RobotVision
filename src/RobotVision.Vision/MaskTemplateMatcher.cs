using System.Diagnostics.CodeAnalysis;
using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Vision.Inference.Strategies;

public static partial class MaskTemplateMatcher
{
    /// <summary>
    /// 按最小外接矩形把目标转正并裁剪（warpAffine 旋转 -θ 后取矩形区域）。
    /// θ 取长边方向（与 AngleGeometry.LongAxisFromMinAreaRect 同口径），转正后长边水平。
    /// marginRatio 为四周边距（相对矩形边长），运行时匹配需要余量；示教裁剪传 0。
    /// 只旋转能盖住裁剪窗的局部补丁（对角线边长），避免对整幅相机图 WarpAffine。
    /// 映射仍按「绕原图矩形中心旋转、裁剪原点在原图像素系」计算，与整图旋转等价。
    /// </summary>
    public static UprightCropResult UprightCrop(
        Mat src, IReadOnlyList<Point2f> contour, double marginRatio, double extraWarpDeg = 0)
    {
        var rect = Cv2.MinAreaRect(contour);
        var housing = contour.Count >= 8 ? MaskHousing.Fit(contour) : default(HousingFrame?);
        // 与 WarpAffine 使用同一未归一化角：NormalizeDeg 会把 180° 折成 0°，逆变换会差 180°
        // 壳体中心/角用于转正（凸起不拖偏）；裁剪窗尺寸仍按整段轮廓，以免裁掉凸起纹理
        var warpAngleDeg = (housing is { } h
                               ? h.WarpAngleDeg
                               : (rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0))
                           + extraWarpDeg;
        var center = housing is { } hh ? hh.Center : rect.Center;

        var marginX = rect.Size.Width * marginRatio;
        var marginY = rect.Size.Height * marginRatio;
        var cropW = (int)Math.Ceiling(rect.Size.Width + 2 * marginX);
        var cropH = (int)Math.Ceiling(rect.Size.Height + 2 * marginY);
        var x = (int)Math.Floor(center.X - cropW / 2.0);
        var y = (int)Math.Floor(center.Y - cropH / 2.0);

        x = Math.Clamp(x, 0, Math.Max(0, src.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, src.Height - 1));
        cropW = Math.Min(cropW, src.Width - x);
        cropH = Math.Min(cropH, src.Height - y);
        if (cropW <= 0 || cropH <= 0)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        // 轴对齐 cropW×cropH 旋转后的外接圆直径 = 对角线；+2 吸收 floor 对齐
        var patch = (int)Math.Ceiling(Math.Sqrt((double)cropW * cropW + (double)cropH * cropH)) + 2;
        var px = Math.Clamp((int)Math.Floor(center.X - patch / 2.0), 0, Math.Max(0, src.Width - 1));
        var py = Math.Clamp((int)Math.Floor(center.Y - patch / 2.0), 0, Math.Max(0, src.Height - 1));
        var pw = Math.Min(patch, src.Width - px);
        var ph = Math.Min(patch, src.Height - py);
        if (pw <= 0 || ph <= 0)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        using var region = src[new Rect(px, py, pw, ph)];
        var localCenter = new Point2f((float)(center.X - px), (float)(center.Y - py));
        using var m = Cv2.GetRotationMatrix2D(localCenter, -warpAngleDeg, 1.0);
        using var rotated = new Mat();
        Cv2.WarpAffine(region, rotated, m, new Size(pw, ph), InterpolationFlags.Linear,
            BorderTypes.Reflect101);

        var lx = x - px;
        var ly = y - py;
        if (lx < 0)
        {
            cropW += lx;
            x -= lx;
            lx = 0;
        }
        if (ly < 0)
        {
            cropH += ly;
            y -= ly;
            ly = 0;
        }
        cropW = Math.Min(cropW, rotated.Width - lx);
        cropH = Math.Min(cropH, rotated.Height - ly);
        if (cropW <= 0 || cropH <= 0 || lx >= rotated.Width || ly >= rotated.Height)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        return new UprightCropResult(
            rotated[new Rect(lx, ly, cropW, cropH)].Clone(), warpAngleDeg, center, x, y);
    }

    /// <summary>
    /// 不转正：按轮廓轴对齐包围盒外扩裁剪。WarpAngleDeg=0，匹配须把示教模板转到现场粗角。
    /// 匹配峰用 <see cref="MapUprightToSource"/> 映回原图（此时为平移）。
    /// </summary>
    public static UprightCropResult AxisAlignedCrop(
        Mat src, IReadOnlyList<Point2f> contour, double marginRatio)
    {
        if (contour.Count < 1)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");
        var pts = contour as Point2f[] ?? [.. contour];
        var box = Cv2.BoundingRect(pts);
        var mx = (int)Math.Ceiling(box.Width * marginRatio);
        var my = (int)Math.Ceiling(box.Height * marginRatio);
        var x = box.X - mx;
        var y = box.Y - my;
        var cropW = box.Width + 2 * mx;
        var cropH = box.Height + 2 * my;

        x = Math.Clamp(x, 0, Math.Max(0, src.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, src.Height - 1));
        cropW = Math.Min(cropW, src.Width - x);
        cropH = Math.Min(cropH, src.Height - y);
        if (cropW <= 0 || cropH <= 0)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        var center = new Point2f((float)(x + cropW / 2.0), (float)(y + cropH / 2.0));
        return new UprightCropResult(
            src[new Rect(x, y, cropW, cropH)].Clone(), 0, center, x, y);
    }

    /// <summary>
    /// 把转正裁剪图上的点映回源图坐标：先加裁剪原点（得到旋转整图坐标），再 Invert
    /// 与 UprightCrop 相同的绕矩形中心旋转 -θ。
    /// </summary>
    public static Point2d MapUprightToSource(UprightCropResult crop, Point2d centerInUpright)
    {
        var rotatedX = centerInUpright.X + crop.CropOriginX;
        var rotatedY = centerInUpright.Y + crop.CropOriginY;
        using var m = Cv2.GetRotationMatrix2D(crop.RotationCenter, -crop.WarpAngleDeg, 1.0);
        using var mInv = new Mat();
        Cv2.InvertAffineTransform(m, mInv);
        var px = mInv.At<double>(0, 0) * rotatedX + mInv.At<double>(0, 1) * rotatedY + mInv.At<double>(0, 2);
        var py = mInv.At<double>(1, 0) * rotatedX + mInv.At<double>(1, 1) * rotatedY + mInv.At<double>(1, 2);
        return new Point2d(px, py);
    }

    /// <summary>
    /// 源图坐标 → 转正裁剪图坐标：与 <see cref="MapUprightToSource"/> 互逆
    ///（同一旋转矩阵，不加 Invert）。
    /// </summary>
    public static Point2d MapSourceToUpright(UprightCropResult crop, Point2d source)
    {
        using var m = Cv2.GetRotationMatrix2D(crop.RotationCenter, -crop.WarpAngleDeg, 1.0);
        var rx = m.At<double>(0, 0) * source.X + m.At<double>(0, 1) * source.Y + m.At<double>(0, 2);
        var ry = m.At<double>(1, 0) * source.X + m.At<double>(1, 1) * source.Y + m.At<double>(1, 2);
        return new Point2d(rx - crop.CropOriginX, ry - crop.CropOriginY);
    }

    /// <summary>匹配角落在 180° 支（头尾与示教相反），转正窗相对示教翻了面。</summary>
    public static bool IsOrientationFlip(double rotationDeg) =>
        Math.Abs(NormalizeSigned(rotationDeg)) >= 90;

    /// <summary>
    /// 转正窗需要再转 180° 才能与示教同向：匹配已走 180° 支，或 180° NCC 明显高于 0°
    ///（极性偶发同号时仍会用错 0° 峰，Y 跳一档）。
    /// </summary>
    public static bool NeedsUprightAlign(MaskTemplateMatchResult match) =>
        IsOrientationFlip(match.RotationDeg)
        || LastDebug.Score180 > LastDebug.Score0 + 0.08;

    /// <summary>
    /// 把源图上的轴对齐特征框映到转正图，取四角 AABB 后裁出模板（独立拷贝）。
    /// 示教用：特征框在相机图上画出，模板必须与运行时同一套转正坐标系。
    /// </summary>
    public static Mat CropUprightBySourceRect(
        UprightCropResult crop, double x, double y, double width, double height)
    {
        var corners = new[]
        {
            new Point2d(x, y),
            new Point2d(x + width, y),
            new Point2d(x + width, y + height),
            new Point2d(x, y + height),
        };
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var corner in corners)
        {
            var u = MapSourceToUpright(crop, corner);
            minX = Math.Min(minX, u.X);
            minY = Math.Min(minY, u.Y);
            maxX = Math.Max(maxX, u.X);
            maxY = Math.Max(maxY, u.Y);
        }

        var ix = (int)Math.Floor(minX);
        var iy = (int)Math.Floor(minY);
        var iw = (int)Math.Ceiling(maxX) - ix;
        var ih = (int)Math.Ceiling(maxY) - iy;
        var clipped = new Rect(ix, iy, Math.Max(0, iw), Math.Max(0, ih))
            & new Rect(0, 0, crop.Upright.Width, crop.Upright.Height);
        if (clipped.Width < 8 || clipped.Height < 8)
            throw new InvalidOperationException(
                "特征 ROI 转正后过小或落在目标外（请把特征框画在分割目标上）");
        return crop.Upright[clipped].Clone();
    }

    internal const double SearchCoarseStep = 2.0;
    internal const double SearchFineStep = 1.0;
    internal const double SearchFineHalfWidth = 1.0;

    /// <summary>
    /// 缓存应覆盖的整数度：0°±range 与（非 noFlip 时）180°±range，步进 <see cref="SearchFineStep"/>。
    /// <paramref name="noFlip"/>：目标永不翻转 180°（分向限定），只生成 0 支缓存，省一半。
    /// </summary>
    public static IReadOnlyList<double> SearchCacheDegrees(double refineRangeDeg, bool noFlip = false)
    {
        var count = (int)Math.Floor(refineRangeDeg / SearchFineStep);
        var degrees = new List<double>(4 * count + 4);
        for (var i = -count; i <= count; i++)
            degrees.Add(i * SearchFineStep);
        if (!noFlip)
            for (var i = -count; i <= count; i++)
                degrees.Add(180.0 + i * SearchFineStep);
        return degrees;
    }

    /// <summary>
    /// 按当前精修范围预旋转模板（配方加载时调用）。内部各图由返回库持有。
    /// <paramref name="noFlip"/>：只预旋 0 支（见 <see cref="SearchCacheDegrees"/>）。
    /// </summary>
    public static RotatedTemplateBank CreateRotationBank(Mat template, double refineRangeDeg, bool noFlip = false)
    {
        var source = template.Clone();
        var items = new List<(double Deg, Mat Image)>();
        foreach (var deg in SearchCacheDegrees(refineRangeDeg, noFlip))
            items.Add((deg, RotateTemplate(source, deg)));
        return new RotatedTemplateBank(source, refineRangeDeg, items);
    }

    /// <summary>
    /// 在搜索图上做带旋转搜索的模板匹配：
    /// 先在 origin±range 与 origin+180°±range 上按 2° 粗搜，再在两支各自峰附近 ±1° 按 1° 精搜，
    /// 用匹配窗（模板大小）的暗区极性消 180° 歧义，抛物线插值得到亚度。
    /// 转正窗时 origin=0；不转正时 origin=壳体粗角（旋转模板而非画面）。
    /// 不得用整幅转正图做极性：分割裁剪是整机+边距，暗区质心几乎在中心，极性常为 null，
    /// NCC 在近对称件（OSDP 凸起）上两支分数接近，会随机翻面。
    /// origin 非 0 时关闭极性（匹配窗未转正，上下半区与示教对不上）。
    /// <paramref name="rotated"/> 为配方加载时生成的缓存；缺角仍现场 WarpAffine。
    /// <paramref name="orientationBranchDeg"/> 非空时只搜该头尾支（相对 origin，测试/调用方强制）。
    /// <paramref name="useMatchedPolarity"/> 为 false 时按 NCC 最高分选支（边缘图极性不可靠）。
    /// 返回 null = 全部候选低于 minScore。
    /// </summary>
    public static MaskTemplateMatchResult? MatchBest(
        Mat upright, Mat template, double refineRangeDeg, double minScore,
        RotatedTemplateBank? rotated = null, double? orientationBranchDeg = null,
        bool useMatchedPolarity = true, double searchOriginDeg = 0)
    {
        LastDebug = default;
        if (Math.Abs(NormalizeSigned(searchOriginDeg)) >= 1.0)
            useMatchedPolarity = false;
        var searchZero = orientationBranchDeg is null
            || Math.Abs(NormalizeSigned(orientationBranchDeg.Value - searchOriginDeg)) < 90;
        var search180 = orientationBranchDeg is null
            || Math.Abs(NormalizeSigned(orientationBranchDeg.Value - searchOriginDeg)) >= 90;

        var rotations = new List<AngleSample>(32);
        if (searchZero)
            SampleAngleBand(upright, template, rotated, rotations, centerDeg: searchOriginDeg,
                rangeDeg: refineRangeDeg, step: SearchCoarseStep);
        if (search180)
            SampleAngleBand(upright, template, rotated, rotations, centerDeg: searchOriginDeg + 180.0,
                rangeDeg: refineRangeDeg, step: SearchCoarseStep);
        if (rotations.Count == 0)
            return null;

        if (searchZero)
        {
            var coarse0 = BestInBand(rotations, zeroBand: true, searchOriginDeg);
            if (coarse0 is not null)
                SampleAngleBand(upright, template, rotated, rotations, coarse0.Value.Deg,
                    SearchFineHalfWidth, SearchFineStep);
        }
        if (search180)
        {
            var coarse180 = BestInBand(rotations, zeroBand: false, searchOriginDeg);
            if (coarse180 is not null)
                SampleAngleBand(upright, template, rotated, rotations, coarse180.Value.Deg,
                    SearchFineHalfWidth, SearchFineStep);
        }

        var best0 = BestInBand(rotations, zeroBand: true, searchOriginDeg);
        var best180 = BestInBand(rotations, zeroBand: false, searchOriginDeg);
        var best = PickOrientation(upright, template, best0, best180, useMatchedPolarity);
        LastDebug = LastDebug with { PeakSharpness = MeasurePeakSharpness(rotations, best, searchOriginDeg) };
        if (best.Score < minScore)
            return null;

        // 抛物线亚度插值：两端候选必须真实存在（值类型 FirstOrDefault 缺失时 Score=0，会把峰拉歪）
        var hasPrev = TryScoreAt(rotations, best.Deg - SearchFineStep, out var prev);
        var hasNext = TryScoreAt(rotations, best.Deg + SearchFineStep, out var next);
        var delta = hasPrev && hasNext ? SubDegreeOffset(prev, best.Score, next) : 0;

        // 中心位置按插值角微调可忽略（1° 内平移 <1px），直接用最佳匹配中心
        return new MaskTemplateMatchResult(best.Score, best.Deg + delta, best.Center);
    }

    private readonly record struct AngleSample(double Deg, double Score, Point2d Center, int TplW, int TplH);

    /// <summary>
    /// 在 center±range 上按 step 采样并匹配；已有角度跳过。
    /// 优先用预旋转缓存，未命中再 WarpAffine（只 Dispose 现场图，不得 Dispose 缓存图）。
    /// </summary>
    private static void SampleAngleBand(
        Mat upright,
        Mat template,
        RotatedTemplateBank? bank,
        List<AngleSample> rotations,
        double centerDeg,
        double rangeDeg,
        double step)
    {
        var count = (int)Math.Floor(rangeDeg / step);
        for (var i = -count; i <= count; i++)
        {
            var deg = centerDeg + i * step;
            if (TryScoreAt(rotations, deg, out _))
                continue;

            Mat? owned = null;
            if (bank is null || !bank.TryGet(deg, out var rotated))
                owned = rotated = RotateTemplate(template, deg);
            try
            {
                // 转正图比旋转后模板小则跳过（目标不完整/靠边被裁）
                if (rotated.Width > upright.Width || rotated.Height > upright.Height)
                    continue;
                using var result = upright.MatchTemplate(rotated, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);
                rotations.Add(new AngleSample(deg, maxVal,
                    new Point2d(maxLoc.X + rotated.Width / 2.0, maxLoc.Y + rotated.Height / 2.0),
                    rotated.Width, rotated.Height));
            }
            finally
            {
                owned?.Dispose();
            }
        }
    }

    /// <summary>模板 PNG base64 编码（内嵌配方文件存储）。</summary>
    public static string EncodeTemplatePng(Mat template)
    {
        Cv2.ImEncode(".png", template, out var bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>模板 PNG base64 解码（字节流解码，兼容任意路径/内嵌场景）。</summary>
    public static Mat DecodeTemplatePng(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException("模板图解码失败（数据损坏）");
        return mat;
    }

    /// <summary>单通道 Canny（形状匹配 / 边缘 NCC 共用）。</summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Returned Mat ownership transfers to caller.")]
    public static Mat ToCanny8u(Mat src)
    {
        using var gray = new Mat();
        if (src.Channels() == 1)
            src.CopyTo(gray);
        else
            Cv2.CvtColor(src, gray, src.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY);
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);
        var edges = new Mat();
        var (low, high) = AdaptiveCannyThresholds(blurred);
        Cv2.Canny(blurred, edges, low, high);
        TightenOrLoosenEdges(blurred, edges, low, high);
        return edges;
    }

    /// <summary>灰度图 → 三通道 Canny 边缘图（与 Matcher 复用同管线：轻降噪 + 中高阈值）。</summary>
    public static Mat ToEdgeMap(Mat src)
    {
        using var edges = ToCanny8u(src);
        var bgr = new Mat();
        Cv2.CvtColor(edges, bgr, ColorConversionCodes.GRAY2BGR);
        return bgr;
    }

    /// <summary>按灰度均值/σ 与 Otsu 估 Canny 双阈值；弱对比度降低门槛，避免固定 60/160 出空边图。</summary>
    public static (double Low, double High) AdaptiveCannyThresholds(Mat gray8)
    {
        if (gray8.Empty())
            return (60, 160);
        Cv2.MeanStdDev(gray8, out var meanS, out var stdS);
        using var tmp = new Mat();
        var otsu = Cv2.Threshold(gray8, tmp, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        var sigma = stdS.Val0;
        var t = otsu;
        if (sigma < 15)
            t = Math.Max(8, Math.Min(otsu, meanS.Val0) * 0.65);
        var low = Math.Clamp(0.5 * t, 8, 100);
        var high = Math.Clamp(Math.Max(t, low + 20), 24, 220);
        return (low, high);
    }

    private static void TightenOrLoosenEdges(Mat blurred, Mat edges, double low, double high)
    {
        var n = blurred.Rows * blurred.Cols;
        if (n <= 0)
            return;
        var density = Cv2.CountNonZero(edges) / (double)n;
        if (density < 0.003)
        {
            Cv2.Canny(blurred, edges, 2, Math.Max(16, high * 0.25));
            return;
        }

        if (density > 0.22)
            Cv2.Canny(blurred, edges, Math.Min(120, low * 1.35), Math.Min(240, high * 1.35));
    }

    /// <summary>
    /// 用暗区质心相对图像中心的上下位置消 180° 歧义（OSDP 类「横条+单侧凸起」）。
    /// 返回 0 或 180：只搜该头尾支；null = 极性不可靠，两支都搜。
    /// 仅当两图都是模板尺度（示教图或匹配窗）时可信；整机转正裁剪请用匹配窗极性。
    /// </summary>
    public static double? PreferOrientationBranch(Mat template, Mat upright)
    {
        if (!TryDarkMassVerticalSign(template, out var templateSign, out var templateOffset)
            || !TryDarkMassVerticalSign(upright, out var uprightSign, out var uprightOffset))
            return null;
        if (templateOffset < MinPolarityOffsetPx || uprightOffset < MinPolarityOffsetPx)
            return null;
        return templateSign == uprightSign ? 0.0 : 180.0;
    }

    private const double MinPolarityOffsetPx = 2.0;
    /// <summary>上下半区灰度差（0–255）低于此则极性不可靠。</summary>
    private const double MinSplitMeanDiff = 6.0;

    /// <summary>
    /// 头尾极性：优先用匹配窗中部上下半区的亮度差（亮底暗凸起，不依赖 Otsu），
    /// Otsu 暗区质心作回退。工业环光下 Otsu 会把整窗打成近对称，极性时有时无。
    /// </summary>
    private static bool TryDarkMassVerticalSign(Mat bgr, out int sign, out double offsetPx)
    {
        sign = 0;
        offsetPx = 0;
        if (bgr.Empty() || bgr.Rows < 8 || bgr.Cols < 8)
            return false;

        using var gray = new Mat();
        if (bgr.Channels() == 1)
            bgr.CopyTo(gray);
        else
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        if (TrySplitMeanVerticalSign(gray, out sign, out offsetPx))
            return true;

        using var dark = new Mat();
        Cv2.Threshold(gray, dark, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        var moments = Cv2.Moments(dark, true);
        if (moments.M00 < 16)
            return false;

        var cy = moments.M01 / moments.M00;
        var mid = (gray.Rows - 1) / 2.0;
        offsetPx = Math.Abs(cy - mid);
        sign = cy > mid ? 1 : -1;
        return true;
    }

    /// <summary>
    /// 只看横向中部 50%：下半更暗为 +1（与示教「凸起在下」同号），上半更暗为 -1。
    /// 比 Otsu 质心抗环光/曝光抖动。
    /// </summary>
    private static bool TrySplitMeanVerticalSign(Mat gray, out int sign, out double offsetPx)
    {
        sign = 0;
        offsetPx = 0;
        var h = gray.Rows;
        var w = gray.Cols;
        var x0 = w / 4;
        var x1 = w - x0;
        var yMid = h / 2;
        if (x1 - x0 < 4 || yMid < 4 || h - yMid < 4)
            return false;

        using var top = gray[new Rect(x0, 0, x1 - x0, yMid)];
        using var bot = gray[new Rect(x0, yMid, x1 - x0, h - yMid)];
        var topMean = Cv2.Mean(top).Val0;
        var botMean = Cv2.Mean(bot).Val0;
        var diff = topMean - botMean; // 正 = 下面更暗
        if (Math.Abs(diff) < MinSplitMeanDiff)
            return false;

        sign = diff > 0 ? 1 : -1;
        offsetPx = Math.Abs(diff);
        return true;
    }

    private static AngleSample? BestInBand(List<AngleSample> samples, bool zeroBand, double originDeg = 0)
    {
        AngleSample? best = null;
        foreach (var s in samples)
        {
            var isZero = Math.Abs(NormalizeSigned(s.Deg - originDeg)) < 90;
            if (isZero != zeroBand)
                continue;
            if (best is null || s.Score > best.Value.Score)
                best = s;
        }
        return best;
    }

    /// <summary>同头尾支上，距主峰 ≥2.5° 的次高峰缺口 (best−second)/best。钝峰趋近 0。</summary>
    private static double MeasurePeakSharpness(List<AngleSample> samples, AngleSample best, double originDeg = 0)
    {
        if (best.Score <= 1e-9)
            return 0;
        var bestZero = Math.Abs(NormalizeSigned(best.Deg - originDeg)) < 90;
        var second = 0.0;
        var found = false;
        foreach (var s in samples)
        {
            var sZero = Math.Abs(NormalizeSigned(s.Deg - originDeg)) < 90;
            if (sZero != bestZero)
                continue;
            if (Math.Abs(NormalizeSigned(s.Deg - best.Deg)) < 2.5)
                continue;
            if (!found || s.Score > second)
            {
                second = s.Score;
                found = true;
            }
        }

        if (!found)
            return 1;
        return Math.Clamp((best.Score - second) / best.Score, 0, 1);
    }

    /// <summary>
    /// 两支都有峰时：用匹配窗暗区极性对齐示教模板（同号→0° 支，异号→180° 支）；
    /// 极性不可靠或关闭时退回 NCC 高峰。
    /// </summary>
    private static AngleSample PickOrientation(
        Mat upright, Mat template, AngleSample? best0, AngleSample? best180, bool useMatchedPolarity)
    {
        if (best0 is null)
            return best180!.Value;
        if (best180 is null)
            return best0.Value;

        if (useMatchedPolarity
            && TryDarkMassVerticalSign(template, out var templateSign, out var templateOffset)
            && templateOffset >= MinPolarityOffsetPx)
        {
            var sceneSign = SceneDarkSign(upright, best0.Value, best180.Value);
            LastDebug = new MatchOrientationDebug(
                best0.Value.Score, best180.Value.Score, templateSign, sceneSign,
                Math.Sqrt(
                    Math.Pow(best0.Value.Center.X - best180.Value.Center.X, 2)
                    + Math.Pow(best0.Value.Center.Y - best180.Value.Center.Y, 2)));
            if (sceneSign is not null)
            {
                var picked = sceneSign == templateSign ? best0.Value : best180.Value;
                var other = sceneSign == templateSign ? best180.Value : best0.Value;
                // 极性与 NCC 严重打架时（分差 >0.12）不信极性，避免选到空窗
                if (picked.Score + 0.12 >= other.Score)
                    return picked;
            }
        }
        else
        {
            LastDebug = new MatchOrientationDebug(
                best0.Value.Score, best180.Value.Score, null, null,
                Math.Sqrt(
                    Math.Pow(best0.Value.Center.X - best180.Value.Center.X, 2)
                    + Math.Pow(best0.Value.Center.Y - best180.Value.Center.Y, 2)));
        }

        return best0.Value.Score >= best180.Value.Score ? best0.Value : best180.Value;
    }

    /// <summary>两处匹配窗：符号一致则采用；不一致时取对比更强的一窗。
    /// 0° 窗在翻面目标上可能裁掉凸起，不能单独否决 180° 窗。</summary>
    private static int? SceneDarkSign(Mat upright, AngleSample a, AngleSample b)
    {
        using var pa = ExtractMatchedPatch(upright, a);
        using var pb = ExtractMatchedPatch(upright, b);
        var sA = 0;
        var sB = 0;
        var oA = 0.0;
        var oB = 0.0;
        var okA = pa is not null && TryDarkMassVerticalSign(pa, out sA, out oA);
        var okB = pb is not null && TryDarkMassVerticalSign(pb, out sB, out oB);
        if (okA && okB)
        {
            if (sA == sB)
                return oA >= MinPolarityOffsetPx || oB >= MinPolarityOffsetPx ? sA : null;
            return oA >= oB
                ? (oA >= MinPolarityOffsetPx ? sA : null)
                : (oB >= MinPolarityOffsetPx ? sB : null);
        }
        if (okA && oA >= MinPolarityOffsetPx)
            return sA;
        if (okB && oB >= MinPolarityOffsetPx)
            return sB;
        return null;
    }

    private static Mat? ExtractMatchedPatch(Mat upright, AngleSample s)
    {
        var x = (int)Math.Round(s.Center.X - s.TplW / 2.0);
        var y = (int)Math.Round(s.Center.Y - s.TplH / 2.0);
        var r = new Rect(x, y, s.TplW, s.TplH) & new Rect(0, 0, upright.Width, upright.Height);
        if (r.Width < 8 || r.Height < 8)
            return null;
        return upright[r].Clone();
    }

    /// <summary>
    /// 混合判决：边缘图定角度（轮廓是角度信息的稳定来源，实测抖动 σ 从 ~1.6° 降到 ~0.3°），
    /// 灰度匹配窗极性定头尾（Canny 会抹掉灰度不对称；近对称件上灰度 NCC 两支也接近）。
    /// 极性不可靠时退回灰度 NCC 二选一。
    /// 返回 Score 为边缘匹配分（角度有效性的判据）；角度与中心取胜出分支。
    /// </summary>
    public static MaskTemplateMatchResult? MatchBestHybrid(
        Mat uprightGray, Mat templateGray, double refineRangeDeg, double minScore,
        RotatedTemplateBank? grayRotated = null, RotatedTemplateBank? edgeRotated = null,
        double? orientationBranchDeg = null, double searchOriginDeg = 0)
    {
        using var uprightEdges = ToEdgeMap(uprightGray);
        using var templateEdges = edgeRotated is null ? ToEdgeMap(templateGray) : null;
        var edgeTemplate = edgeRotated?.Source ?? templateEdges!;
        // 边缘图不做暗区极性（Otsu 不可靠）；头尾交给灰度匹配窗
        var edgeMatch = MatchBest(uprightEdges, edgeTemplate, refineRangeDeg, minScore,
            edgeRotated, orientationBranchDeg, useMatchedPolarity: false, searchOriginDeg);
        if (edgeMatch is null)
            return null;

        if (orientationBranchDeg is not null)
        {
            var gray = SingleMatch(uprightGray, templateGray, edgeMatch.RotationDeg, grayRotated);
            return gray is null
                ? edgeMatch
                : new MaskTemplateMatchResult(edgeMatch.Score, gray.RotationDeg, gray.CenterInUpright);
        }

        var degA = NormalizeSigned(edgeMatch.RotationDeg);
        var degB = degA >= 0 ? degA - 180.0 : degA + 180.0;
        var a = SingleMatch(uprightGray, templateGray, degA, grayRotated);
        var b = SingleMatch(uprightGray, templateGray, degB, grayRotated);

        var picked = Math.Abs(NormalizeSigned(searchOriginDeg)) < 1.0
            ? PickHybridByGrayPolarity(uprightGray, templateGray, a, b)
              ?? SelectHybridOrientation(edgeMatch, a, b)
            : SelectHybridOrientation(edgeMatch, a, b);
        return new MaskTemplateMatchResult(edgeMatch.Score, picked.RotationDeg, picked.CenterInUpright);
    }

    /// <summary>
    /// 灰度头尾二选一：任一侧失败用另一侧；双侧失败退回边缘结果。
    /// 不能写 <c>a?.Score &gt;= b?.Score</c>——一侧为 null 时比较为 false，会解引用另一侧的 null。
    /// </summary>
    public static MaskTemplateMatchResult SelectHybridOrientation(
        MaskTemplateMatchResult edgeMatch,
        MaskTemplateMatchResult? grayA,
        MaskTemplateMatchResult? grayB)
    {
        if (grayA is null && grayB is null)
            return edgeMatch;
        if (grayB is null)
            return new MaskTemplateMatchResult(edgeMatch.Score, grayA!.RotationDeg, grayA.CenterInUpright);
        if (grayA is null)
            return new MaskTemplateMatchResult(edgeMatch.Score, grayB.RotationDeg, grayB.CenterInUpright);
        var win = grayA.Score >= grayB.Score ? grayA : grayB;
        return new MaskTemplateMatchResult(edgeMatch.Score, win.RotationDeg, win.CenterInUpright);
    }

    private static MaskTemplateMatchResult? PickHybridByGrayPolarity(
        Mat uprightGray, Mat templateGray,
        MaskTemplateMatchResult? grayA, MaskTemplateMatchResult? grayB)
    {
        if (grayA is null || grayB is null)
            return null;
        if (!TryDarkMassVerticalSign(templateGray, out var templateSign, out var templateOffset)
            || templateOffset < MinPolarityOffsetPx)
            return null;

        var aZero = Math.Abs(NormalizeSigned(grayA.RotationDeg)) < 90;
        var zero = aZero ? grayA : grayB;
        var oneEighty = aZero ? grayB : grayA;
        var s0 = new AngleSample(zero.RotationDeg, zero.Score, zero.CenterInUpright,
            templateGray.Width, templateGray.Height);
        var s180 = new AngleSample(oneEighty.RotationDeg, oneEighty.Score, oneEighty.CenterInUpright,
            templateGray.Width, templateGray.Height);
        var sceneSign = SceneDarkSign(uprightGray, s0, s180);
        if (sceneSign is null)
            return null;
        var win = sceneSign == templateSign ? zero : oneEighty;
        return win;
    }

    /// <summary>单角度灰度匹配：模板旋转到指定角后在转正图上滑窗一次。尺寸不够返回 null。</summary>
    private static MaskTemplateMatchResult? SingleMatch(
        Mat upright, Mat template, double deg, RotatedTemplateBank? bank = null)
    {
        var signed = NormalizeSigned(deg);
        Mat? owned = null;
        Mat rotated;
        if (bank is not null && (bank.TryGet(signed, out rotated!) || bank.TryGet(deg, out rotated!)))
        {
            // 整数度命中缓存（亚度插值角仍现场旋转）
        }
        else
        {
            owned = rotated = RotateTemplate(template, signed);
        }

        try
        {
            if (rotated.Width > upright.Width || rotated.Height > upright.Height)
                return null;
            using var result = upright.MatchTemplate(rotated, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);
            return new MaskTemplateMatchResult(maxVal, deg,
                new Point2d(maxLoc.X + rotated.Width / 2.0, maxLoc.Y + rotated.Height / 2.0));
        }
        finally
        {
            owned?.Dispose();
        }
    }

    /// <summary>归一到 (-180,180]。</summary>
    private static double NormalizeSigned(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }

    /// <summary>
    /// 直线拟合精修（弱纹理矩形适用）：先剔凸起再对两条长边 Huber 拟合。
    /// Fitted=false 时 Angle/Center 仍是粗结果，供画面对照；策略默认不得输出给机器人。
    /// </summary>
    public static (double AngleDeg, Point2d Center, bool Fitted) RefineByLineFit(
        IReadOnlyList<Point2f> contour, double coarseAngleDeg)
    {
        var horizontal = RefineByLineFitBands(contour, coarseAngleDeg, horizontalBands: true);
        if (horizontal.Fitted)
            return horizontal;
        return RefineByLineFitBands(contour, coarseAngleDeg, horizontalBands: false);
    }

    /// <summary>直线拟合：horizontalBands=true 取上下长边；false 取左右长边（齿列/缺口在上下时更稳）。</summary>
    internal static (double AngleDeg, Point2d Center, bool Fitted) RefineByLineFitBands(
        IReadOnlyList<Point2f> contour, double coarseAngleDeg, bool horizontalBands)
    {
        var ptsSrc = MaskHousing.CorePoints(contour);
        var cx = ptsSrc.Average(p => p.X);
        var cy = ptsSrc.Average(p => p.Y);

        var rad = -coarseAngleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var pts = new Point2f[ptsSrc.Length];
        for (var i = 0; i < ptsSrc.Length; i++)
        {
            var dx = ptsSrc[i].X - cx;
            var dy = ptsSrc[i].Y - cy;
            pts[i] = new Point2f(
                (float)(cx + dx * cos - dy * sin),
                (float)(cy + dx * sin + dy * cos));
        }

        double yMin = float.MaxValue, yMax = float.MinValue, xMin = float.MaxValue, xMax = float.MinValue;
        foreach (var p in pts)
        {
            if (p.Y < yMin) yMin = p.Y;
            if (p.Y > yMax) yMax = p.Y;
            if (p.X < xMin) xMin = p.X;
            if (p.X > xMax) xMax = p.X;
        }
        var h = yMax - yMin;
        var w = xMax - xMin;
        if (h <= 0 || w <= 0)
            return (coarseAngleDeg, new(cx, cy), false);

        var yCut = yMin + 0.35 * h;
        var xCut = xMin + 0.35 * w;
        var xLo = xMin + 0.15 * w;
        var xHi = xMax - 0.15 * w;
        var yLo = yMin + 0.15 * h;
        var yHi = yMax - 0.15 * h;
        Point2f[] bandA;
        Point2f[] bandB;
        if (horizontalBands)
        {
            bandA = pts.Where(p => p.Y <= yCut && p.X >= xLo && p.X <= xHi).ToArray();
            bandB = pts.Where(p => p.Y >= yMax - 0.35 * h && p.X >= xLo && p.X <= xHi).ToArray();
        }
        else
        {
            bandA = pts.Where(p => p.X <= xCut && p.Y >= yLo && p.Y <= yHi).ToArray();
            bandB = pts.Where(p => p.X >= xMax - 0.35 * w && p.Y >= yLo && p.Y <= yHi).ToArray();
        }

        if (bandA.Length < 8 || bandB.Length < 8)
            return (coarseAngleDeg, new(cx, cy), false);

        var aTop = FitLineAngleDeg(bandA);
        var aBottom = FitLineAngleDeg(bandB);
        if (double.IsNaN(aTop) || double.IsNaN(aBottom))
            return (coarseAngleDeg, new(cx, cy), false);

        var delta = (aTop + aBottom) / 2.0;
        return (AngleGeometry.NormalizeDeg(coarseAngleDeg + delta), new Point2d(cx, cy), true);
    }

    /// <summary>Huber 鲁棒直线拟合，返回直线角（归一到 (-90,90]，与方向无关）；点太少返回 NaN。</summary>
    private static double FitLineAngleDeg(Point2f[] points)
    {
        if (points.Length < 8)
            return double.NaN;
        var line = Cv2.FitLine(points, DistanceTypes.Huber, 0, 0.01, 0.01);
        var deg = Math.Atan2(line.Vy, line.Vx) * 180.0 / Math.PI;
        // 方向向量无方向语义：归一到 (-90,90]
        var d = ((deg + 90.0) % 180.0 + 180.0) % 180.0 - 90.0;
        return d == -90.0 ? 90.0 : d;
    }

    /// <summary>内孔判定的最小面积（px²）：更小的孔对掩码噪声敏感，不参与连线。</summary>
    private const double MinHoleAreaPx = 30;

    /// <summary>质心-内孔连线的最小基线（px）：孔太靠近质心则角度噪声放大，放弃精修。</summary>
    private const double MinBaselinePx = 5;

    /// <summary>槽/孔分界：拟合椭圆长短轴比 ≥ 此值视为细长槽（长轴定角），否则按圆孔（中心连线）。</summary>
    private const double SlotAspectThreshold = 2.5;

    /// <summary>
    /// 质心-内标连线精修（CentroidHoleLine）：解码分割位掩码（BoundingBox 尺寸、LSB-first
    /// 位打包，与 YoloDotNet GetContourPoints 同源格式），FindContours(RETR_CCOMP) 提取
    /// 内孔轮廓，取最大孔 FitEllipse。按椭圆长短轴比分两种内标：
    /// 圆孔（轴比 &lt; 阈值）——角度 = 质心→孔心连线（指向孔），角度精度受偏置距离限制；
    /// 细长槽（轴比 ≥ 阈值）——角度 = 槽长轴（基线=槽长，精度高），
    /// 头尾 = 偏置向量在（长轴/法线）中较大分量上的符号（零件转 180° 两分量同时反号，
    /// 判决只需 ±1 bit，偏置很弱也稳定）。位置均为掩码质心（亚像素）。
    /// 无有效孔/基线不足返回 null（策略兜底粗角度）。输出为 BoundingBox 局部坐标。
    /// </summary>
    public static CentroidHoleResult? RefineByCentroidHoleLine(byte[] bitPackedMask, int width, int height)
    {
        if (bitPackedMask.Length == 0 || width <= 0 || height <= 0)
            return null;

        var buf = new byte[width * height];
        for (var i = 0; i < buf.Length; i++)
            buf[i] = (byte)(((bitPackedMask[i >> 3] >> (i & 7)) & 1) * 255);
        using var mask = Mat.FromPixelData(height, width, MatType.CV_8UC1, buf);

        var m = Cv2.Moments(mask, true);
        if (Math.Abs(m.M00) < 1e-9)
            return null;
        var centroid = new Point2d(m.M10 / m.M00, m.M01 / m.M00);

        Cv2.FindContours(mask, out var contours, out var hierarchy, RetrievalModes.CComp,
            ContourApproximationModes.ApproxSimple);
        double bestArea = 0;
        Point[]? bestHole = null;
        for (var i = 0; i < contours.Length; i++)
        {
            if (hierarchy[i].Parent < 0)
                continue;
            var area = Cv2.ContourArea(contours[i]);
            if (area < MinHoleAreaPx || area <= bestArea)
                continue;
            bestArea = area;
            bestHole = contours[i];
        }
        if (bestHole is null || bestHole.Length < 5)
            return null;

        using var holeArray = InputArray.Create(bestHole);
        var ellipse = Cv2.FitEllipse(holeArray);
        var holeCenter = new Point2d(ellipse.Center.X, ellipse.Center.Y);

        var dx = holeCenter.X - centroid.X;
        var dy = holeCenter.Y - centroid.Y;
        if (dx * dx + dy * dy < MinBaselinePx * MinBaselinePx)
            return null;

        var major = Math.Max(ellipse.Size.Width, ellipse.Size.Height);
        var minor = Math.Min(ellipse.Size.Width, ellipse.Size.Height);
        double angleDeg;
        if (major >= SlotAspectThreshold * minor)
        {
            var axisDeg = AngleGeometry.NormalizeDeg(
                ellipse.Size.Width >= ellipse.Size.Height ? ellipse.Angle : ellipse.Angle + 90.0);
            var rad = axisDeg * Math.PI / 180.0;
            var ax = Math.Cos(rad);
            var ay = Math.Sin(rad);
            var along = dx * ax + dy * ay;
            var across = -dx * ay + dy * ax;
            var bit = Math.Abs(along) >= Math.Abs(across) ? Math.Sign(along) : Math.Sign(across);
            angleDeg = bit >= 0 ? axisDeg : AngleGeometry.NormalizeSignedDeg(axisDeg + 180.0);
        }
        else
        {
            var (_, ang) = AngleGeometry.FromTwoPoints(centroid.X, centroid.Y, holeCenter.X, holeCenter.Y);
            angleDeg = ang;
        }

        var offset = Math.Sqrt(dx * dx + dy * dy);
        var quality = Math.Clamp(
            0.45 + 0.30 * Math.Clamp(bestArea / 200.0, 0, 1) + 0.25 * Math.Clamp(offset / 16.0, 0, 1),
            0.45, 0.98);
        return new CentroidHoleResult(angleDeg, centroid, quality);
    }

    /// <summary>绕模板中心旋转（边缘色填充外扩角），小幅旋转用线性插值。</summary>
    private static Mat RotateTemplate(Mat template, double deg)
    {
        if (Math.Abs(deg) < 1e-9)
            return template.Clone();

        var center = new Point2f(template.Width / 2f, template.Height / 2f);
        using var m = Cv2.GetRotationMatrix2D(center, deg, 1.0);
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Abs(Math.Cos(rad));
        var sin = Math.Abs(Math.Sin(rad));
        var w = (int)Math.Ceiling(template.Width * cos + template.Height * sin);
        var h = (int)Math.Ceiling(template.Width * sin + template.Height * cos);
        m.Set(0, 2, m.At<double>(0, 2) + (w - template.Width) / 2.0);
        m.Set(1, 2, m.At<double>(1, 2) + (h - template.Height) / 2.0);

        using var top = template.Row(0);
        using var bottom = template.Row(template.Rows - 1);
        using var left = template.Col(0);
        using var right = template.Col(template.Cols - 1);
        var mt = top.Mean(); var mb = bottom.Mean();
        var ml = left.Mean(); var mr = right.Mean();
        var fill = new Scalar(
            (mt.Val0 + mb.Val0 + ml.Val0 + mr.Val0) / 4,
            (mt.Val1 + mb.Val1 + ml.Val1 + mr.Val1) / 4,
            (mt.Val2 + mb.Val2 + ml.Val2 + mr.Val2) / 4);

        var dst = new Mat();
        Cv2.WarpAffine(template, dst, m, new Size(w, h), InterpolationFlags.Linear,
            BorderTypes.Constant, fill);
        return dst;
    }

    private static bool TryScoreAt(
        List<AngleSample> rotations, double deg, out double score)
    {
        foreach (var r in rotations)
        {
            if (Math.Abs(r.Deg - deg) < 1e-6)
            {
                score = r.Score;
                return true;
            }
        }
        score = 0;
        return false;
    }

    private static double SubDegreeOffset(double prev, double best, double next)
    {
        var denom = prev - 2 * best + next;
        if (Math.Abs(denom) < 1e-9)
            return 0;
        var offset = 0.5 * (prev - next) / denom;
        return Math.Clamp(offset, -0.5, 0.5);
    }
}
