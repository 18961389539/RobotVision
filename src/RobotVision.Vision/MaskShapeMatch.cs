using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.Vision.Inference.Strategies;

/// <summary>
/// 开源几何匹配（Chamfer）：示教 Canny 点落在当前转正窗距离场上的平均距离。
/// 只在分割目标的转正裁剪内搜索平移和 ±精修范围（含 180° 支），不整图搜。
/// </summary>
public static class MaskShapeMatch
{
    private const int MinTeachPoints = 24;
    private const int MaxTeachPoints = 256;
    private const double PointStridePx = 2.0;
    private const double HitDistPx = 2.5;
    private const double MaxMeanDistPx = 10.0;
    private const double MinHitRate = 0.18;
    private const double OobPenalty = 12.0;
    private const double CropMarginRatio = 0.15;
    private const int TeachBorderPx = 4;

    // ── 有向 Chamfer（HALCON shape-based 轻量版）────────────
    // 方向量化：梯度方向 atan2(dy,dx) 折叠到 [0,180)（Canny 边无极性），16 bin × 11.25°。
    private const int DirBins = 16;
    private const double DirBinWidthDeg = 180.0 / DirBins;      // 11.25
    private const int DirTolBins = 6;                            // 67.5°：正交干扰(90°)仍可分,且容忍转正插值方向漂移
    /// <summary>Sobel 幅值下限²（8-bit 图）：低于此值方向不可靠（平坦区/转正插值模糊带），标记无效豁免方向检查。</summary>
    private const double MinDirGradientSq = 36.0;                // 幅值 ~6

    public sealed record Result(double AngleDeg, Point2d Center, double Score, double MeanDistPx, double HitRate);

    public sealed record ShapeViz(IReadOnlyList<Point2d> Inliers, IReadOnlyList<Point2d> Rejected)
    {
        public static readonly ShapeViz Empty = new([], []);
    }

    public sealed record Attempt(Result? Pose, ShapeViz Viz)
    {
        public static Attempt Miss(ShapeViz? viz = null) => new(null, viz ?? ShapeViz.Empty);
    }

    public sealed class ShapeModel
    {
        internal ShapeModel(
            Point2f[] points, double[] weights, byte[] dirBins,
            double centerX, double centerY,
            Point2f polarLeft, Point2f polarRight, double polarDelta)
        {
            Points = points;
            Weights = weights;
            DirBins = dirBins;
            CenterX = centerX;
            CenterY = centerY;
            PolarLeft = polarLeft;
            PolarRight = polarRight;
            PolarDelta = polarDelta;
        }

        internal Point2f[] Points { get; }
        internal double[] Weights { get; }
        /// <summary>每个示教边点的梯度方向 bin（[0,DirBins)，折叠 180° 无向）。与 Points 一一对应。</summary>
        internal byte[] DirBins { get; }
        internal double CenterX { get; }
        internal double CenterY { get; }
        internal Point2f PolarLeft { get; }
        internal Point2f PolarRight { get; }
        internal double PolarDelta { get; }
        public int PointCount => Points.Length;
    }

    internal readonly record struct DebugInfo(
        int TeachPts, double MeanDist, double HitRate, double ResidualDeg,
        double Polar0, double Polar180, double PolarTeach,
        double DirAgree = double.NaN); // 方向一致性命中占比（有向 Chamfer 诊断/归因）

    [ThreadStatic]
    internal static DebugInfo LastDebug;

    private static readonly RecipeTeachCache<ShapeModel> TeachCache = new(
        ShouldCache,
        RecipeTeachFingerprints.TemplateImage,
        recipe =>
        {
            using var decoded = MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
            return BuildTeach(decoded);
        },
        _ => { });

    public static Result? Refine(
        Mat image, IReadOnlyList<Point2f> contour, ShapeModel model, double refineRangeDeg = 8) =>
        TryRefine(image, contour, model, refineRangeDeg).Pose;

    public static Attempt TryRefine(
        Mat image, IReadOnlyList<Point2f> contour, ShapeModel? model, double refineRangeDeg)
    {
        LastDebug = default;
        if (image.Empty() || contour.Count < 4 || model is null || model.PointCount < MinTeachPoints)
            return Attempt.Miss();

        var range = Math.Clamp(refineRangeDeg, 1, 45);
        UprightCropResult crop;
        try
        {
            crop = MaskTemplateMatcher.UprightCrop(image, contour, CropMarginRatio);
        }
        catch (InvalidOperationException)
        {
            return Attempt.Miss();
        }

        try
        {
            var hit = MatchOnUpright(crop.Upright, model, range);
            var polar0 = hit is null ? 0.0 : PolarAgree(crop.Upright, model, hit);
            var polar180 = 0.0;
            UprightCropResult? flipped = null;
            try
            {
                flipped = MaskTemplateMatcher.UprightCrop(image, contour, CropMarginRatio, extraWarpDeg: 180);
                var rematch = MatchOnUpright(flipped.Upright, model, range);
                polar180 = rematch is null ? 0.0 : PolarAgree(flipped.Upright, model, rematch);
                var pickFlip = PreferFlippedCrop(polar0, polar180, model.PolarDelta, hit, rematch);
                if (pickFlip && rematch is not null)
                {
                    crop.Upright.Dispose();
                    crop = flipped;
                    flipped = null;
                    hit = rematch;
                }
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                flipped?.Upright.Dispose();
            }

            if (hit is null)
                return Attempt.Miss();

            LastDebug = new DebugInfo(
                model.PointCount, hit.MeanDistPx, hit.HitRate, hit.RotationDeg,
                polar0, polar180, model.PolarDelta, hit.DirAgree);
            if (hit.HitRate < MinHitRate || hit.MeanDistPx > MaxMeanDistPx)
                return Attempt.Miss(hit.Viz);

            var angle = AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + hit.RotationDeg);
            var center = MaskTemplateMatcher.MapUprightToSource(crop, hit.CenterInUpright);
            var score = QualityScore(hit.MeanDistPx, hit.HitRate);
            return new Attempt(
                new Result(angle, center, score, hit.MeanDistPx, hit.HitRate),
                MapVizToSource(crop, hit.Viz));
        }
        finally
        {
            crop.Upright.Dispose();
        }
    }

    public static ShapeModel? BuildTeach(Mat template)
    {
        if (template.Empty())
            return null;
        using var edges = MaskTemplateMatcher.ToCanny8u(template);
        var sampled = SampleEdgePoints(edges);
        if (sampled.Points.Length < MinTeachPoints)
            return null;
        using var gray = ToGray(template);
        var (left, right) = PolarProbes(sampled.Points);
        var delta = SampleGray(gray, right.X + sampled.Cx, right.Y + sampled.Cy)
                    - SampleGray(gray, left.X + sampled.Cx, left.Y + sampled.Cy);
        var dirBins = TeachDirBins(gray, sampled.Points, sampled.Cx, sampled.Cy);
        return new ShapeModel(sampled.Points, RadiusWeights(sampled.Points), dirBins,
            sampled.Cx, sampled.Cy, left, right, delta);
    }

    /// <summary>对每个示教边点（相对中心坐标）插值其灰度梯度方向 bin（折叠 180° 无向）。</summary>
    private static byte[] TeachDirBins(Mat gray, Point2f[] pts, double cx, double cy)
    {
        var binMap = BuildDirMap(gray);
        try
        {
            var bins = new byte[pts.Length];
            var w = binMap.Cols;
            var h = binMap.Rows;
            for (var i = 0; i < pts.Length; i++)
            {
                // 边点在其 3×3 邻域多数方向：直接采样该点 bin（Sobel 在边缘像素处方向锐利）
                // 注意：pts 是相对中心的坐标，查方向图须加回中心偏移
                var x = (int)Math.Round(Math.Clamp(pts[i].X + cx, 0, w - 1));
                var y = (int)Math.Round(Math.Clamp(pts[i].Y + cy, 0, h - 1));
                bins[i] = binMap.At<byte>(y, x);
            }

            return bins;
        }
        finally
        {
            binMap.Dispose();
        }
    }

    /// <summary>
    /// 灰度梯度方向 bin 图（CV_8U）：Sobel 两向 → atan2 方向 → 折叠 [0,180) → 量化 DirBins。
    /// 现场搜索图与示教图各算一次，搜索循环全程复用（开销 = 1 次 Sobel，可忽略）。
    /// </summary>
    private static Mat BuildDirMap(Mat gray)
    {
        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, ksize: 3);
        Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, ksize: 3);
        var binMap = new Mat(gray.Rows, gray.Cols, MatType.CV_8UC1, Scalar.All(0));
        var bx = gx.GetGenericIndexer<float>();
        var by = gy.GetGenericIndexer<float>();
        var bm = binMap.GetGenericIndexer<byte>();
        for (var y = 0; y < gray.Rows; y++)
        for (var x = 0; x < gray.Cols; x++)
        {
            var dx = bx[y, x];
            var dy = by[y, x];
            // 弱梯度（平坦区/插值模糊带）方向无意义 → 0xFF 无效标记（运行时豁免方向检查）
            if (dx * dx + dy * dy < MinDirGradientSq)
            {
                bm[y, x] = 0xFF;
                continue;
            }

            var deg = Math.Atan2(dy, dx) * 180.0 / Math.PI; // [-180,180]
            if (deg < 0)
                deg += 180.0;                                // 折叠：θ 与 θ+180 同边
            var bin = (int)(deg / DirBinWidthDeg);
            bm[y, x] = (byte)Math.Clamp(bin, 0, DirBins - 1);
        }

        return binMap;
    }

    public static void Warm(RecipeConfig recipe) => TeachCache.Warm(recipe);

    public static void Remove(string recipeName) => TeachCache.Remove(recipeName);

    /// <summary>归还 <see cref="GetOrCreate"/> 租约。与旋转/SIFT 缓存语义一致。</summary>
    public static void Release(ShapeModel? model) => TeachCache.Release(model);

    public static ShapeModel? GetOrCreate(RecipeConfig recipe) => TeachCache.GetOrCreate(recipe);

    internal static double QualityScore(double meanDist, double hitRate)
    {
        var dist = Math.Clamp(1.0 - meanDist / 8.0, 0, 1);
        return Math.Clamp(0.55 * hitRate + 0.45 * dist, 0.15, 1);
    }

    internal static string FormatQualityNote(double score, double matchThreshold, DebugInfo debug) =>
        $"命中 {debug.HitRate:P0} · 均距 {debug.MeanDist:0.1f}px · 方向一致 {debug.DirAgree:P0} · 分 {score:0.00} (门 {matchThreshold:0.00})";

    private static bool ShouldCache(RecipeConfig recipe) =>
        recipe.AngleMode == AngleMode.MaskTemplate
        && recipe.Template.RefineMethod == SegmentRefineMethod.ShapeMatch
        && !string.IsNullOrEmpty(recipe.Template.TemplateImageBase64);

    private sealed record MatchHit(
        double RotationDeg, Point2d CenterInUpright, double MeanDistPx, double HitRate, ShapeViz Viz,
        double DirAgree = double.NaN);

    private static MatchHit? MatchOnUpright(
        Mat upright, ShapeModel model, double rangeDeg)
    {
        using var gray = ToGray(upright);
        using var edges = MaskTemplateMatcher.ToCanny8u(upright);
        using var inv = new Mat();
        Cv2.BitwiseNot(edges, inv);
        using var dt = new Mat();
        Cv2.DistanceTransform(inv, dt, DistanceTypes.L2, DistanceTransformMasks.Mask3);
        if (dt.Empty() || dt.Type() != MatType.CV_32FC1)
            return null;
        using var dirMap = BuildDirMap(gray);
        var cx0 = upright.Width / 2.0;
        var cy0 = upright.Height / 2.0;
        var span = Math.Max(10, Math.Min(40, Math.Max(upright.Width, upright.Height) * 0.10));
        var coarse = Search(dt, dirMap, model, rangeDeg, 0, cx0, cy0, span, transStep: 3, rotStep: 1.0);
        if (coarse is not { } c)
            return null;
        var fine = Search(dt, dirMap, model, 2.5, c.Deg, c.Mx, c.My, 12, transStep: 1, rotStep: 0.5);
        var seed = fine ?? c;
        var micro = RefineSubpixel(dt, dirMap, model, seed);
        var viz = BuildViz(dt, model.Points, micro.Deg, micro.Mx, micro.My);
        return new MatchHit(micro.Deg, new Point2d(micro.Mx, micro.My), micro.Mean, micro.Hit, viz,
            micro.DirAgree);
    }

    private readonly record struct PoseCand(double Deg, double Mx, double My, double Mean, double Hit, double Cost, double DirAgree);

    private static PoseCand? Search(
        Mat dt, Mat dirMap, ShapeModel model, double rangeDeg, double bandDeg,
        double cx, double cy, double span, int transStep, double rotStep)
    {
        PoseCand? best = null;
        var indexer = dt.GetGenericIndexer<float>();
        var dirIdx = dirMap.GetGenericIndexer<byte>();
        var w = dt.Cols;
        var h = dt.Rows;
        var lo = bandDeg - rangeDeg;
        var hi = bandDeg + rangeDeg;
        for (var deg = lo; deg <= hi + 1e-6; deg += rotStep)
        {
            var rad = deg * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            for (var dy = -span; dy <= span + 1e-6; dy += transStep)
            for (var dx = -span; dx <= span + 1e-6; dx += transStep)
            {
                var mx = cx + dx;
                var my = cy + dy;
                var scored = Score(indexer, dirIdx, w, h, model, cos, sin, mx, my);
                if (best is null || scored.Cost < best.Value.Cost - 1e-9)
                    best = new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
            }
        }

        return best;
    }

    private static PoseCand RefineSubpixel(Mat dt, Mat dirMap, ShapeModel model, PoseCand seed)
    {
        var best = seed;
        var indexer = dt.GetGenericIndexer<float>();
        var dirIdx = dirMap.GetGenericIndexer<byte>();
        var w = dt.Cols;
        var h = dt.Rows;
        foreach (var dDeg in new[] { -0.25, 0, 0.25 })
        {
            var deg = seed.Deg + dDeg;
            var rad = deg * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            for (var dy = -0.5; dy <= 0.5 + 1e-9; dy += 0.5)
            for (var dx = -0.5; dx <= 0.5 + 1e-9; dx += 0.5)
            {
                var mx = seed.Mx + dx;
                var my = seed.My + dy;
                var scored = Score(indexer, dirIdx, w, h, model, cos, sin, mx, my);
                if (scored.Cost < best.Cost - 1e-9)
                    best = new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
            }
        }

        return best with { Deg = AngleGeometry.NormalizeSignedDeg(best.Deg) };
    }

    private readonly record struct Scored(double Mean, double Hit, double Cost, double DirAgree);

    private static Scored Score(
        MatIndexer<float> dt, MatIndexer<byte> dirMap,
        int w, int h, ShapeModel model, double cos, double sin, double mx, double my)
    {
        var pts = model.Points;
        var weights = model.Weights;
        var dirBins = model.DirBins;
        var sum = 0.0;
        var wsum = 0.0;
        var hit = 0;
        var n = pts.Length;
        var dirOkCount = 0;
        var dirChecked = 0;
        for (var i = 0; i < n; i++)
        {
            var x = mx + pts[i].X * cos - pts[i].Y * sin;
            var y = my + pts[i].X * sin + pts[i].Y * cos;
            var wt = weights[i];
            wsum += wt;
            if (x < 1 || y < 1 || x >= w - 2 || y >= h - 2)
            {
                sum += OobPenalty * wt;
                continue;
            }

            var d = SampleBilinear(dt, x, y);
            // 有向 Chamfer：模型点方向 bin + 旋转偏移 = 期望方向，与现场方向图比（折叠 180° 无向）。
            // 方向失配只影响 hit（不放大距离代价）：正确位姿距离近 → mean 低仍占优；
            // 方向项用于压制"距离近但方向错"的平行干扰边（其 hit 少 → cost 升高）。
            var dirOk = true;
            var sceneBin = dirMap[(int)Math.Round(y), (int)Math.Round(x)];
            var modelBin = dirBins[i];
            if (modelBin < DirBins && sceneBin < DirBins)
            {
                dirChecked++;
                var degBins = (int)Math.Round(Atan2SignedDeg(cos, sin) / DirBinWidthDeg) % DirBins;
                var expect = (modelBin + degBins + DirBins * 4) % DirBins;
                var rawDiff = (sceneBin - expect + DirBins * 4) % DirBins;
                var diff = Math.Min(rawDiff, DirBins - rawDiff); // 环回最小差
                if (diff <= DirTolBins)
                    dirOkCount++;
                else
                    dirOk = false;
            }

            sum += d * wt;
            if (dirOk && d <= HitDistPx)
                hit++;
        }

        var mean = sum / Math.Max(1e-6, wsum);
        var hitRate = hit / (double)n;
        var dirAgree = dirChecked == 0 ? double.NaN : dirOkCount / (double)dirChecked;
        return new Scored(mean, hitRate, mean + 6.0 * (1.0 - hitRate), dirAgree);
    }

    /// <summary>从旋转矩阵余弦/正弦恢复有符号旋转角（度）。</summary>
    private static double Atan2SignedDeg(double cos, double sin) =>
        Math.Atan2(sin, cos) * 180.0 / Math.PI;

    private static bool PreferFlippedCrop(
        double polar0, double polar180, double polarTeach, MatchHit? a, MatchHit? b)
    {
        if (b is null)
            return false;
        if (a is null)
            return true;
        if (Math.Abs(polarTeach) < 12)
            return b.MeanDistPx + 0.2 < a.MeanDistPx;
        if (polar180 > polar0 + 1e-6)
            return true;
        if (polar0 > polar180 + 1e-6)
            return false;
        return b.MeanDistPx + 0.2 < a.MeanDistPx;
    }

    private static double PolarAgree(Mat upright, ShapeModel model, MatchHit hit)
    {
        using var gray = ToGray(upright);
        var rad = hit.RotationDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2d Map(Point2f p) => new(
            hit.CenterInUpright.X + p.X * cos - p.Y * sin,
            hit.CenterInUpright.Y + p.X * sin + p.Y * cos);
        var l = Map(model.PolarLeft);
        var r = Map(model.PolarRight);
        var delta = SampleGray(gray, r.X, r.Y) - SampleGray(gray, l.X, l.Y);
        return Math.Sign(model.PolarDelta) * delta;
    }

    private static (Point2f Left, Point2f Right) PolarProbes(Point2f[] pts)
    {
        var minX = pts[0].X;
        var maxX = pts[0].X;
        var minY = pts[0].Y;
        var maxY = pts[0].Y;
        foreach (var p in pts)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        var sx = Math.Max(8, maxX - minX);
        var sy = Math.Max(8, maxY - minY);
        return (
            new Point2f(minX + 0.22f * sx, minY + 0.32f * sy),
            new Point2f(maxX - 0.22f * sx, maxY - 0.32f * sy));
    }

    private static Mat ToGray(Mat src)
    {
        var gray = new Mat();
        if (src.Channels() == 1)
            src.CopyTo(gray);
        else
            Cv2.CvtColor(src, gray, src.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static double SampleGray(Mat gray, double x, double y)
    {
        var x0 = (int)Math.Round(Math.Clamp(x, 1, gray.Cols - 2));
        var y0 = (int)Math.Round(Math.Clamp(y, 1, gray.Rows - 2));
        var indexer = gray.GetGenericIndexer<byte>();
        var sum = 0;
        var n = 0;
        for (var dy = -3; dy <= 3; dy++)
        for (var dx = -3; dx <= 3; dx++)
        {
            var xx = x0 + dx;
            var yy = y0 + dy;
            if ((uint)xx >= (uint)gray.Cols || (uint)yy >= (uint)gray.Rows)
                continue;
            sum += indexer[yy, xx];
            n++;
        }

        return n == 0 ? 0 : sum / (double)n;
    }

    private static float SampleBilinear(MatIndexer<float> dt, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var dx = (float)(x - x0);
        var dy = (float)(y - y0);
        var v00 = dt[y0, x0];
        var v10 = dt[y0, x0 + 1];
        var v01 = dt[y0 + 1, x0];
        var v11 = dt[y0 + 1, x0 + 1];
        return (1 - dx) * (1 - dy) * v00 + dx * (1 - dy) * v10 + (1 - dx) * dy * v01 + dx * dy * v11;
    }

    private readonly record struct SampledEdges(Point2f[] Points, double Cx, double Cy);

    private static SampledEdges SampleEdgePoints(Mat edges)
    {
        var raw = new List<Point2f>();
        var indexer = edges.GetGenericIndexer<byte>();
        var border = Math.Min(TeachBorderPx, Math.Min(edges.Rows, edges.Cols) / 4);
        for (var y = border; y < edges.Rows - border; y++)
        for (var x = border; x < edges.Cols - border; x++)
        {
            if (indexer[y, x] == 0)
                continue;
            raw.Add(new Point2f(x, y));
        }

        if (raw.Count == 0)
            return new SampledEdges([], 0, 0);

        var cx = 0.0;
        var cy = 0.0;
        foreach (var p in raw)
        {
            cx += p.X;
            cy += p.Y;
        }

        cx /= raw.Count;
        cy /= raw.Count;

        var step = 1;
        if (raw.Count > MaxTeachPoints)
            step = Math.Max(1, (int)Math.Ceiling(raw.Count / (double)MaxTeachPoints));
        var minSq = PointStridePx * PointStridePx;
        var kept = new List<Point2f>(Math.Min(MaxTeachPoints, raw.Count));
        for (var i = 0; i < raw.Count && kept.Count < MaxTeachPoints; i += step)
        {
            var p = raw[i];
            var rel = new Point2f((float)(p.X - cx), (float)(p.Y - cy));
            var ok = true;
            for (var k = 0; k < kept.Count; k++)
            {
                var dx = rel.X - kept[k].X;
                var dy = rel.Y - kept[k].Y;
                if (dx * dx + dy * dy < minSq)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
                kept.Add(rel);
        }

        return new SampledEdges(kept.ToArray(), cx, cy);
    }

    private static double[] RadiusWeights(Point2f[] pts)
    {
        var maxR = 1.0;
        foreach (var p in pts)
        {
            var r = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            if (r > maxR)
                maxR = r;
        }

        var w = new double[pts.Length];
        for (var i = 0; i < pts.Length; i++)
        {
            var r = Math.Sqrt(pts[i].X * pts[i].X + pts[i].Y * pts[i].Y) / maxR;
            w[i] = 0.35 + r * r;
        }

        return w;
    }

    private static ShapeViz BuildViz(Mat dt, Point2f[] pts, double deg, double mx, double my)
    {
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var inn = new List<Point2d>();
        var rej = new List<Point2d>();
        var indexer = dt.GetGenericIndexer<float>();
        var w = dt.Cols;
        var h = dt.Rows;
        foreach (var p in pts)
        {
            var x = mx + p.X * cos - p.Y * sin;
            var y = my + p.X * sin + p.Y * cos;
            var at = new Point2d(x, y);
            if (x < 1 || y < 1 || x >= w - 2 || y >= h - 2)
            {
                rej.Add(at);
                continue;
            }

            if (SampleBilinear(indexer, x, y) <= HitDistPx)
                inn.Add(at);
            else
                rej.Add(at);
        }

        return new ShapeViz(inn, rej);
    }

    private static ShapeViz MapVizToSource(UprightCropResult crop, ShapeViz viz)
    {
        Point2d Map(Point2d p) => MaskTemplateMatcher.MapUprightToSource(crop, p);
        return new ShapeViz(viz.Inliers.Select(Map).ToArray(), viz.Rejected.Select(Map).ToArray());
    }
}
