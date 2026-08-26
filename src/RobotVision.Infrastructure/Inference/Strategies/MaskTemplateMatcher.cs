using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>模板匹配结果：匹配分数、相对转正模板的旋转角（度）、匹配中心在转正图坐标系的位置。</summary>
public sealed record MaskTemplateMatchResult(double Score, double RotationDeg, Point2d CenterInUpright);

/// <summary>
/// 转正裁剪结果。匹配中心在 <see cref="Upright"/> 坐标系，映射回原图须加裁剪原点后再
/// 用与 WarpAffine 相同的 <see cref="WarpAngleDeg"/> 做逆变换——不能对裁剪坐标直接 Invert。
/// </summary>
public sealed record UprightCropResult(
    Mat Upright,
    double WarpAngleDeg,
    Point2f RotationCenter,
    double CropOriginX,
    double CropOriginY);

/// <summary>
/// 分割+模板匹配（MaskTemplate 模式）的共享几何/匹配辅助：
/// 策略（运行时精修）与配方页「示教模板」（转正裁剪生成模板）共用同一套变换，
/// 保证示教坐标系与运行时坐标系完全一致。
/// 约定：所有角度遵循 AngleGeometry（y 轴向下，度，逆时针为正）。
/// </summary>
public static class MaskTemplateMatcher
{
    /// <summary>
    /// 按最小外接矩形把目标转正并裁剪（warpAffine 旋转 -θ 后取矩形区域）。
    /// θ 取长边方向（与 AngleGeometry.LongAxisFromMinAreaRect 同口径），转正后长边水平。
    /// marginRatio 为四周边距（相对矩形边长），运行时匹配需要余量；示教裁剪传 0。
    /// </summary>
    public static UprightCropResult UprightCrop(Mat src, IReadOnlyList<Point2f> contour, double marginRatio)
    {
        var rect = Cv2.MinAreaRect(contour);
        // 与 WarpAffine 使用同一未归一化角：NormalizeDeg 会把 180° 折成 0°，逆变换会差 180°
        var warpAngleDeg = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;

        var center = rect.Center;
        using var m = Cv2.GetRotationMatrix2D(center, -warpAngleDeg, 1.0);
        using var rotated = new Mat();
        Cv2.WarpAffine(src, rotated, m, src.Size(), InterpolationFlags.Linear,
            BorderTypes.Reflect101);

        var marginX = rect.Size.Width * marginRatio;
        var marginY = rect.Size.Height * marginRatio;
        var cropW = (int)Math.Ceiling(rect.Size.Width + 2 * marginX);
        var cropH = (int)Math.Ceiling(rect.Size.Height + 2 * marginY);
        var x = (int)Math.Floor(center.X - cropW / 2.0);
        var y = (int)Math.Floor(center.Y - cropH / 2.0);

        x = Math.Clamp(x, 0, Math.Max(0, rotated.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, rotated.Height - 1));
        cropW = Math.Min(cropW, rotated.Width - x);
        cropH = Math.Min(cropH, rotated.Height - y);
        if (cropW <= 0 || cropH <= 0)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        return new UprightCropResult(
            rotated[new Rect(x, y, cropW, cropH)], warpAngleDeg, center, x, y);
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
    /// 在转正图上做带旋转搜索的模板匹配：
    /// 候选角 φ ∈ ±refineRangeDeg（1° 步进）∪ 180°±refineRangeDeg——
    /// 前者精修角度，后者判头尾（180° 歧义）；两者合起来覆盖有方向的绝对角度。
    /// 相邻候选分数做抛物线插值得到亚度精度。返回 null = 全部候选低于 minScore。
    /// </summary>
    public static MaskTemplateMatchResult? MatchBest(
        Mat upright, Mat template, double refineRangeDeg, double minScore)
    {
        var step = 1.0;
        var count = (int)Math.Floor(refineRangeDeg / step);
        var rotations = new List<(double Deg, double Score, Point2d Center)>(count * 4 + 2);

        for (var i = -count; i <= count; i++)
        {
            foreach (var flip in new[] { 0.0, 180.0 })
            {
                var deg = i * step + flip;
                using var rotated = RotateTemplate(template, deg);
                // 转正图比旋转后模板小则跳过（目标不完整/靠边被裁）
                if (rotated.Width > upright.Width || rotated.Height > upright.Height)
                    continue;
                using var result = upright.MatchTemplate(rotated, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);
                var entry = (Deg: deg, Score: maxVal,
                    Center: new Point2d(maxLoc.X + rotated.Width / 2.0, maxLoc.Y + rotated.Height / 2.0));
                rotations.Add(entry);
            }
        }

        if (rotations.Count == 0)
            return null;

        var best = rotations.OrderByDescending(r => r.Score).First();
        if (best.Score < minScore)
            return null;

        // 抛物线亚度插值：两端候选必须真实存在（值类型 FirstOrDefault 缺失时 Score=0，会把峰拉歪）
        var hasPrev = TryScoreAt(rotations, best.Deg - step, out var prev);
        var hasNext = TryScoreAt(rotations, best.Deg + step, out var next);
        var delta = hasPrev && hasNext ? SubDegreeOffset(prev, best.Score, next) : 0;

        // 中心位置按插值角微调可忽略（1° 内平移 <1px），直接用最佳匹配中心
        return new MaskTemplateMatchResult(best.Score, best.Deg + delta, best.Center);
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

    /// <summary>灰度图 → 三通道 Canny 边缘图（与 Matcher 复用同管线：轻降噪 + 中高阈值）。</summary>
    public static Mat ToEdgeMap(Mat src)
    {
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);
        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 60, 160);
        var bgr = new Mat();
        Cv2.CvtColor(edges, bgr, ColorConversionCodes.GRAY2BGR);
        return bgr;
    }

    /// <summary>
    /// 混合判决：边缘图定角度（轮廓是角度信息的稳定来源，实测抖动 σ 从 ~1.6° 降到 ~0.3°），
    /// 灰度图二选一定头尾（Canny 会抹掉彩色/灰度不对称特征，纯边缘判向会大面积 180° 翻转；
    /// 灰度在 ±180 两分支各匹配一次取高分，恢复头尾可靠性）。
    /// 返回 Score 为边缘匹配分（角度有效性的判据）；角度与中心取胜出分支。
    /// </summary>
    public static MaskTemplateMatchResult? MatchBestHybrid(
        Mat uprightGray, Mat templateGray, double refineRangeDeg, double minScore)
    {
        // 1) 边缘图角度搜索（含 0/180 两分支取峰，亚度插值）
        using var uprightEdges = ToEdgeMap(uprightGray);
        using var templateEdges = ToEdgeMap(templateGray);
        var edgeMatch = MatchBest(uprightEdges, templateEdges, refineRangeDeg, minScore);
        if (edgeMatch is null)
            return null;

        // 2) 灰度图头尾复核：边缘峰角 φ 与其对角 φ±180 各匹配一次，分数定头尾
        var degA = NormalizeSigned(edgeMatch.RotationDeg);
        var degB = degA >= 0 ? degA - 180.0 : degA + 180.0;
        var a = SingleMatch(uprightGray, templateGray, degA);
        var b = SingleMatch(uprightGray, templateGray, degB);
        return SelectHybridOrientation(edgeMatch, a, b);
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

    /// <summary>单角度灰度匹配：模板旋转到指定角后在转正图上滑窗一次。尺寸不够返回 null。</summary>
    private static MaskTemplateMatchResult? SingleMatch(Mat upright, Mat template, double deg)
    {
        using var rotated = RotateTemplate(template, NormalizeSigned(deg));
        if (rotated.Width > upright.Width || rotated.Height > upright.Height)
            return null;
        using var result = upright.MatchTemplate(rotated, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);
        return new MaskTemplateMatchResult(maxVal, deg,
            new Point2d(maxLoc.X + rotated.Width / 2.0, maxLoc.Y + rotated.Height / 2.0));
    }

    /// <summary>归一到 (-180,180]。</summary>
    private static double NormalizeSigned(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }

    /// <summary>
    /// 直线拟合精修（弱纹理矩形适用）：掩码轮廓两条长边各做鲁棒直线拟合（Huber），
    /// 取两线角均值修正粗角度——抗离群点显著优于 minAreaRect（单噪声点即可拉动矩形）。
    /// 中心 = 轮廓点均值（亚像素）。角度无方向语义，归一到 [0,180)。
    /// 带宽不足（非矩形/点太少）返回粗角度兜底。
    /// </summary>
    public static (double AngleDeg, Point2d Center) RefineByLineFit(
        IReadOnlyList<Point2f> contour, double coarseAngleDeg)
    {
        // 轮廓均值中心（边界密集采样时近似质心）
        var cx = contour.Average(p => p.X);
        var cy = contour.Average(p => p.Y);

        // 转正坐标系（绕中心旋转 -coarseAngle）：长边变为近似水平，便于按 y 分带
        var rad = -coarseAngleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var pts = new Point2f[contour.Count];
        for (var i = 0; i < contour.Count; i++)
        {
            var dx = contour[i].X - cx;
            var dy = contour[i].Y - cy;
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
            return (coarseAngleDeg, new(cx, cy));

        // 两条长边带：上/下 35% 高度带；x 取内侧 70%（排除圆角/毛刺端点）
        var yCut = yMin + 0.35 * h;
        var xLo = xMin + 0.15 * w;
        var xHi = xMax - 0.15 * w;
        var top = pts.Where(p => p.Y <= yCut && p.X >= xLo && p.X <= xHi).ToArray();
        var bottom = pts.Where(p => p.Y >= yMax - 0.35 * h && p.X >= xLo && p.X <= xHi).ToArray();
        if (top.Length < 8 || bottom.Length < 8)
            return (coarseAngleDeg, new(cx, cy)); // 非矩形轮廓：兜底粗角度

        var aTop = FitLineAngleDeg(top);
        var aBottom = FitLineAngleDeg(bottom);
        if (double.IsNaN(aTop) || double.IsNaN(aBottom))
            return (coarseAngleDeg, new(cx, cy));

        // 转正系中两线角应 ≈0；偏差均值即粗角度修正量
        var delta = (aTop + aBottom) / 2.0;
        return (AngleGeometry.NormalizeDeg(coarseAngleDeg + delta), new Point2d(cx, cy));
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

    /// <summary>质心-内孔连线结果：连线角（指向孔，(-180,180]）、掩码质心（输出位置）。</summary>
    public sealed record CentroidHoleResult(double AngleDeg, Point2d Centroid);

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

        // 位掩码 → CV_8UC1（255=前景）。像素索引 = y*width + x，LSB-first
        var buf = new byte[width * height];
        for (var i = 0; i < buf.Length; i++)
            buf[i] = (byte)(((bitPackedMask[i >> 3] >> (i & 7)) & 1) * 255);
        using var mask = Mat.FromPixelData(height, width, MatType.CV_8UC1, buf);

        // 掩码质心（全填充像素的一阶矩）
        var m = Cv2.Moments(mask, true);
        if (Math.Abs(m.M00) < 1e-9)
            return null;
        var centroid = new Point2d(m.M10 / m.M00, m.M01 / m.M00);

        // 两层轮廓：RETR_CCOMP 下 Parent ≥ 0 的是孔（内轮廓）
        Cv2.FindContours(mask, out var contours, out var hierarchy, RetrievalModes.CComp,
            ContourApproximationModes.ApproxSimple);
        double bestArea = 0;
        Point[]? bestHole = null;
        for (var i = 0; i < contours.Length; i++)
        {
            if (hierarchy[i].Parent < 0)
                continue; // 外轮廓
            var area = Cv2.ContourArea(contours[i]);
            if (area < MinHoleAreaPx || area <= bestArea)
                continue;
            bestArea = area;
            bestHole = contours[i];
        }
        if (bestHole is null || bestHole.Length < 5)
            return null; // FitEllipse 至少 5 点

        var ellipse = Cv2.FitEllipse(InputArray.Create(bestHole));
        var holeCenter = new Point2d(ellipse.Center.X, ellipse.Center.Y);

        // 基线检查：孔几乎在质心上 → 连线方向随机（槽的头尾判决同样依赖此偏置）
        var dx = holeCenter.X - centroid.X;
        var dy = holeCenter.Y - centroid.Y;
        if (dx * dx + dy * dy < MinBaselinePx * MinBaselinePx)
            return null;

        // 孔/槽自适应：轴比 ≥ 阈值走"长轴定角 + 偏置侧定头尾"
        var major = Math.Max(ellipse.Size.Width, ellipse.Size.Height);
        var minor = Math.Min(ellipse.Size.Width, ellipse.Size.Height);
        double angleDeg;
        if (major >= SlotAspectThreshold * minor)
        {
            // 长轴角（OpenCV angle ∈ (0,90] 且 Width 未必是长边，与 LongAxisFromMinAreaRect 同口径换算）
            var axisDeg = AngleGeometry.NormalizeDeg(
                ellipse.Size.Width >= ellipse.Size.Height ? ellipse.Angle : ellipse.Angle + 90.0);
            var rad = axisDeg * Math.PI / 180.0;
            var ax = Math.Cos(rad);
            var ay = Math.Sin(rad);

            // 偏置 v 在轴向/法向的分量：零件刚体连接下两分量都随 180° 旋转反号；
            // 取绝对值较大者定符号——径向槽用轴向分量，切向槽用法向分量，判决余量最大化
            var along = dx * ax + dy * ay;
            var across = -dx * ay + dy * ax;
            var bit = Math.Abs(along) >= Math.Abs(across) ? Math.Sign(along) : Math.Sign(across);
            angleDeg = bit >= 0 ? axisDeg : AngleGeometry.NormalizeSignedDeg(axisDeg + 180.0);
        }
        else
        {
            // 圆孔：质心→孔心连线（指向孔）
            var (_, ang) = AngleGeometry.FromTwoPoints(centroid.X, centroid.Y, holeCenter.X, holeCenter.Y);
            angleDeg = ang;
        }
        return new CentroidHoleResult(angleDeg, centroid);
    }

    /// <summary>绕模板中心旋转（边缘色填充外扩角），小幅旋转用线性插值。
    /// 填充色取模板四边像素均值（与运行时转正图边缘背景一致）——
    /// 黑色填充会让四角面积随角度对称变化，把匹配峰钉死在 0°，精修失效。</summary>
    private static Mat RotateTemplate(Mat template, double deg)
    {
        if (Math.Abs(deg) < 1e-9)
            return template.Clone();

        var center = new Point2f(template.Width / 2f, template.Height / 2f);
        using var m = Cv2.GetRotationMatrix2D(center, deg, 1.0);
        // 旋转后外接尺寸，避免四角裁掉
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Abs(Math.Cos(rad));
        var sin = Math.Abs(Math.Sin(rad));
        var w = (int)Math.Ceiling(template.Width * cos + template.Height * sin);
        var h = (int)Math.Ceiling(template.Width * sin + template.Height * cos);
        // 平移补偿：保持旋转中心在新图中心
        m.Set(0, 2, m.At<double>(0, 2) + (w - template.Width) / 2.0);
        m.Set(1, 2, m.At<double>(1, 2) + (h - template.Height) / 2.0);

        // 边缘均值填充：与转正图背景同色，避免填充色差随角度引入伪峰
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
        List<(double Deg, double Score, Point2d Center)> rotations, double deg, out double score)
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

    /// <summary>三点抛物线顶点偏移（亚度插值）：s(p)、s(0)、s(n) 的对称抛物线顶点，限制在 ±0.5 步长内。</summary>
    private static double SubDegreeOffset(double prev, double best, double next)
    {
        var denom = prev - 2 * best + next;
        if (Math.Abs(denom) < 1e-9)
            return 0;
        var offset = 0.5 * (prev - next) / denom;
        return Math.Clamp(offset, -0.5, 0.5);
    }
}
