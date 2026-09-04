using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.Vision;

/// <summary>
/// 开源几何匹配（Chamfer）：示教 Canny 点落在当前转正窗距离场上的平均距离。
/// 只在分割目标的转正裁剪内搜索平移和 ±精修范围（含 180° 支），不整图搜。
/// </summary>
public static partial class MaskShapeMatch
{
    private const int MinTeachPoints = 24;
    private const int MaxTeachPoints = 256;
    private const double PointStridePx = 2.0;
    private const double HitDistPx = 2.5;
    private const double MaxMeanDistPx = 10.0;
    private const double MinHitRate = 0.18;
    private const double OobPenalty = 12.0;
    public const double CropMarginRatio = 0.15;
    private const int TeachBorderPx = 4;
    /// <summary>NCC 角种子：转正窗内灰度相关给出粗角，收窄 Chamfer 防插值边方向误导。</summary>
    private const double NccSeedMinScore = 0.15;
    private const double NccSeedBandDeg = 2.5;
    /// <summary>Chamfer 未过门时 NCC 置信兜底（示教 margin0 / 现场 margin0.15 导致 NCC 分偏低但角仍可靠）。</summary>
    private const double NccFallbackMinScore = 0.15;
    /// <summary>场景 |warp|≥此值时用 NCC 平移峰作 Chamfer 锚（转正窗内 NCC 残差角≈0，不能用残差角判断）。</summary>
    private const double LargeWarpNccTransAnchorDeg = 12.0;
    /// <summary>大角度 warp 插值加厚边缘：Chamfer 距离场对 Canny 做 1px 膨胀。</summary>
    private const double EdgeDilateWarpDeg = 10.0;
    /// <summary>梯度混合距离：峰值的此比例视为强边（Halcon 对比度边缘轻量近似）。</summary>
    private const double GradEdgePeakRatio = 0.15;
    /// <summary>大角度 warp：现场转正窗重采样到示教尺寸后做 Chamfer（统一规范化像素格）。</summary>
    private const double CanonChamferWarpDeg = LargeWarpNccTransAnchorDeg;
    /// <summary>定向 Chamfer：沿示教边法向搜索最近距离场（抑制平行边 2D DT 误拾取）。</summary>
    private const double DirectedSearchHalfPx = 8.0;
    private const double DirectedStepPx = 1.0;
    /// <summary>大 warp 下允许略高于均距门的裕量（转正插值边带）；超过则必须 NCC 兜底或拒识。</summary>
    private const double LargeWarpMeanSlackRatio = 1.08;
    /// <summary>平移粗搜半径上限（px），控制 Chamfer 搜索耗时。</summary>
    private const int MaxSearchSpanPx = 26;

    // ── 有向 Chamfer（HALCON shape-based 轻量版）────────────
    // 方向量化：梯度方向 atan2(dy,dx) 折叠到 [0,180)（Canny 边无极性），16 bin × 11.25°。
    private const int DirBins = 16;
    private const double DirBinWidthDeg = 180.0 / DirBins;      // 11.25
    private const int DirTolBins = 6;                            // 67.5°：正交干扰(90°)仍可分,且容忍转正插值方向漂移
    /// <summary>方向 bin 失配时附加到均距代价，抑制正交/平行 margin 干扰边。</summary>
    private const double DirMismatchMeanPx = 4.5;
    /// <summary>Sobel 幅值下限²（8-bit 图）：低于此值方向不可靠（平坦区/转正插值模糊带），标记无效豁免方向检查。</summary>
    private const double MinDirGradientSq = 36.0;                // 幅值 ~6

    public sealed record Result(double AngleDeg, Point2d Center, double Score, double MeanDistPx, double HitRate);

    public sealed record ShapeViz(
        IReadOnlyList<Point2d> Inliers,
        IReadOnlyList<Point2d> Rejected,
        ShapeSearchDebug? SearchDebug = null,
        IReadOnlyList<int>? DistHistogram = null,
        int PyramidLevels = 1)
    {
        public static readonly ShapeViz Empty = new([], [], null, null, 1);
    }

    /// <summary>搜索诊断：粗/细网格评估次数、最优代价、金字塔层数（<see cref="ShapeMatchOptions.EmitSearchDebug"/>）。</summary>
    public sealed record ShapeSearchDebug(
        int CoarseEvaluations, int FineEvaluations, double BestCost, int PyramidLevels = 1);

    public sealed record Attempt(Result? Pose, ShapeViz Viz)
    {
        public static Attempt Miss(ShapeViz? viz = null) => new(null, viz ?? ShapeViz.Empty);
    }

    public sealed class ShapeModel : IDisposable
    {
        internal ShapeModel(
            Point2f[] points, double[] weights, byte[] dirBins,
            double centerX, double centerY,
            Point2f polarLeft, Point2f polarRight, double polarDelta,
            Mat nccGray, double partHalfW, double partHalfH,
            double housingOffsetX, double housingOffsetY)
        {
            Points = points;
            Weights = weights;
            DirBins = dirBins;
            CenterX = centerX;
            CenterY = centerY;
            PolarLeft = polarLeft;
            PolarRight = polarRight;
            PolarDelta = polarDelta;
            NccGray = nccGray;
            PartHalfW = partHalfW;
            PartHalfH = partHalfH;
            HousingOffsetX = housingOffsetX;
            HousingOffsetY = housingOffsetY;
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
        /// <summary>示教转正灰度图（与配方 PNG 同尺寸），供现场 NCC 角种子。</summary>
        internal Mat NccGray { get; }
        /// <summary>示教窗内零件半宽/半高（扣除 margin 后的几何尺度，用于规范化 Chamfer）。</summary>
        internal double PartHalfW { get; }
        internal double PartHalfH { get; }
        /// <summary>示教转正窗：边点质心相对壳体中心的偏移（px）。</summary>
        internal double HousingOffsetX { get; }
        internal double HousingOffsetY { get; }
        /// <summary>源图像素：示教原点相对分割轮廓多边形质心（刚体搬运用）。</summary>
        internal double SourceOx { get; private set; }
        internal double SourceOy { get; private set; }
        internal bool HasSourceOrigin { get; private set; }
        public int PointCount => Points.Length;

        internal void BindSourceOrigin(Point2d origin, IReadOnlyList<Point2f> contour)
        {
            var c = PolygonCentroid(contour);
            SourceOx = origin.X - c.X;
            SourceOy = origin.Y - c.Y;
            HasSourceOrigin = true;
        }

        public void Dispose() => NccGray.Dispose();
    }

    /// <summary>示教 PNG 上 Chamfer 原点（边点质心），供 JL <c>SetShapeModelOrigin</c> 对齐。</summary>
    public static bool TryTeachOrigin(Mat template, out double cx, out double cy)
    {
        using var model = BuildTeach(template);
        if (model is null)
        {
            cx = 0;
            cy = 0;
            return false;
        }

        cx = model.CenterX;
        cy = model.CenterY;
        return true;
    }

    /// <summary>
    /// 与 Chamfer 几何原点同口径：有源图绑定时多边形质心 + R(θ)×示教偏移；
    /// 否则壳体中心 + R(θ)×HousingOffset。
    /// </summary>
    public static Point2d AlignToTeachOrigin(
        IReadOnlyList<Point2f> contour, double angleDeg, ShapeModel? model)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        if (model is { HasSourceOrigin: true })
        {
            var c = PolygonCentroid(contour);
            return new Point2d(
                c.X + model.SourceOx * cos - model.SourceOy * sin,
                c.Y + model.SourceOx * sin + model.SourceOy * cos);
        }

        var housing = MaskHousing.Fit(contour);
        var ox = model?.HousingOffsetX ?? 0;
        var oy = model?.HousingOffsetY ?? 0;
        return new Point2d(
            housing.Center.X + ox * cos - oy * sin,
            housing.Center.Y + ox * sin + oy * cos);
    }

    /// <summary>
    /// 与 Chamfer 强命中出角同口径：线拟合与转正粗角相差 &lt;1.5° 时用线拟合方向，头尾仍跟 candidate。
    /// 粗角落在 ±90° 之外时先 CanonWarp，避免 MinAreaRect 的 ±180° 支把长边拟合锁在补角。
    /// </summary>
    public static double AlignToTeachAngle(IReadOnlyList<Point2f> contour, double candidateDeg)
    {
        var housing = MaskHousing.Fit(contour);
        var warp = housing.WarpAngleDeg;
        var seed = Math.Abs(AngleGeometry.NormalizeSignedDeg(warp)) > 90.0
            ? AngleGeometry.CanonWarpDeg(warp)
            : warp;
        var (lfDeg, _, lfOk) = MaskTemplateMatcher.RefineByLineFit(contour, seed);
        if (!lfOk)
            return candidateDeg;

        var dSeed = Math.Abs(AngleGeometry.NormalizeSignedDeg(lfDeg - seed));
        if (dSeed >= 1.5)
            return candidateDeg;

        var fused = AngleGeometry.FuseDirected(lfDeg, candidateDeg);
        var a = AngleGeometry.NormalizeSignedDeg(fused);
        if (Math.Abs(a) > 90.0)
            a = AngleGeometry.NormalizeSignedDeg(a + 180.0);
        return a;
    }

    internal readonly record struct DebugInfo(
        int TeachPts, double MeanDist, double HitRate, double ResidualDeg,
        double Polar0, double Polar180, double PolarTeach,
        double DirAgree = double.NaN,
        double DisplayAngleDeg = double.NaN,
        double DisplayCx = double.NaN,
        double DisplayCy = double.NaN);

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
        m => m.Dispose());

    public static Result? Refine(
        Mat image, IReadOnlyList<Point2f> contour, ShapeModel model, double refineRangeDeg = 8) =>
        TryRefine(image, contour, model, refineRangeDeg).Pose;

    public static Attempt TryRefine(
        Mat image, IReadOnlyList<Point2f> contour, ShapeModel? model, double refineRangeDeg,
        bool noFlip = false) =>
        TryRefine(image, contour, model, refineRangeDeg, noFlip, null);

    public static Attempt TryRefine(
        Mat image, IReadOnlyList<Point2f> contour, ShapeModel? model, double refineRangeDeg,
        bool noFlip, ShapeMatchOptions? options)
    {
        _activeOptions = options ?? ShapeMatchOptions.Default;
        try
        {
            return TryRefineCore(image, contour, model, refineRangeDeg, noFlip);
        }
        finally
        {
            _activeOptions = ShapeMatchOptions.Default;
        }
    }

    [ThreadStatic] private static ShapeMatchOptions _activeOptions = ShapeMatchOptions.Default;

    private static ShapeMatchOptions ActiveOptions => _activeOptions ?? ShapeMatchOptions.Default;

    private static double ActiveMinHitRate => ActiveOptions.MinHitRate;
    private static double ActiveMaxMeanDist => ActiveOptions.MaxMeanDistPx;

    private static Attempt TryRefineCore(
        Mat image, IReadOnlyList<Point2f> contour, ShapeModel? model, double refineRangeDeg,
        bool noFlip)
    {
        LastDebug = default;
        if (image.Empty() || contour.Count < 4 || model is null || model.PointCount < MinTeachPoints)
            return Attempt.Miss();

        var range = Math.Clamp(
            ActiveOptions.AngleExtentDeg > 0
                ? Math.Min(refineRangeDeg, ActiveOptions.AngleExtentDeg * 0.5)
                : refineRangeDeg,
            1, 45);
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
            var ncc = ProbeNccSeed(crop.Upright, model, range, orientationBranchDeg: 0);
            var contourUpright = ContourInUpright(crop, contour);
            ChamferField? field = null;
            MatchHit? hit = null;
            try
            {
                field = ChamferField.Create(crop.Upright, model, crop.WarpAngleDeg, contourUpright);
                hit = MatchOnField(field, model, range, ncc, crop.WarpAngleDeg);
                var polar0 = hit is null || noFlip ? 0.0 : PolarAgree(crop.Upright, model, hit);
                var polar180 = 0.0;
                UprightCropResult? flipped = null;
                if (!noFlip)
                {
                    try
                    {
                        flipped = MaskTemplateMatcher.UprightCrop(image, contour, CropMarginRatio, extraWarpDeg: 180);
                        var flipContour = ContourInUpright(flipped, contour);
                        using var flipField = ChamferField.Create(flipped.Upright, model, flipped.WarpAngleDeg, flipContour);
                        var rematch = MatchOnField(flipField, model, range, default, flipped.WarpAngleDeg);
                        polar180 = rematch is null ? 0.0 : PolarAgree(flipped.Upright, model, rematch);
                        var pickFlip = PreferFlippedCrop(polar0, polar180, model.PolarDelta, hit, rematch);
                        if (pickFlip && rematch is not null)
                        {
                            crop.Upright.Dispose();
                            crop = flipped;
                            flipped = null;
                            hit = rematch;
                            field.Dispose();
                            field = ChamferField.Create(crop.Upright, model, crop.WarpAngleDeg, ContourInUpright(crop, contour));
                            ncc = ProbeNccSeed(crop.Upright, model, range, orientationBranchDeg: 0);
                            if (ncc.Found)
                            {
                                var refined = MatchOnField(field, model, range, ncc, crop.WarpAngleDeg);
                                if (refined is not null)
                                    hit = refined;
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    finally
                    {
                        flipped?.Upright.Dispose();
                    }
                }

                if (hit is null)
                {
                    if (NccAngleTrustworthy(ncc) && ncc.Score >= NccFallbackMinScore)
                        hit = NccTrustedHit(field, model, ncc, crop.WarpAngleDeg);
                    if (hit is null)
                        return Attempt.Miss();
                }

                if ((hit.HitRate < ActiveMinHitRate || hit.MeanDistPx > ActiveMaxMeanDist)
                    && NccAngleTrustworthy(ncc) && ncc.Score >= NccFallbackMinScore)
                {
                    var borderlineMean = hit.MeanDistPx <= ActiveMaxMeanDist * LargeWarpMeanSlackRatio;
                    var waiveMean = Math.Abs(crop.WarpAngleDeg) >= LargeWarpNccTransAnchorDeg
                                    && hit.HitRate >= ActiveMinHitRate
                                    && hit.MeanDistPx > ActiveMaxMeanDist
                                    && borderlineMean;
                    if (!waiveMean)
                        hit = NccTrustedHit(field, model, ncc, crop.WarpAngleDeg) ?? hit;
                }

                if (NccAngleTrustworthy(ncc) && ncc.Score >= NccFallbackMinScore
                    && Math.Abs(AngleGeometry.NormalizeSignedDeg(hit.RotationDeg - ncc.RotationDeg)) > 0.32)
                {
                    var nccHit = NccTrustedHit(field, model, ncc, crop.WarpAngleDeg);
                    if (nccHit is not null
                        && (nccHit.MeanDistPx <= hit.MeanDistPx * 1.08 || nccHit.HitRate + 0.04 >= hit.HitRate))
                        hit = nccHit;
                }

                LastDebug = new DebugInfo(
                    model.PointCount, hit.MeanDistPx, hit.HitRate, hit.RotationDeg,
                    polar0, polar180, model.PolarDelta, hit.DirAgree);
                var angle = ResolveOutputAngleDeg(contour, crop.WarpAngleDeg, hit.RotationDeg, ncc, hit.HitRate, hit.MeanDistPx);
                var center = ResolveOutputCenter(contour, crop, model, hit, ncc, angle);
                LastDebug = LastDebug with
                {
                    DisplayAngleDeg = angle,
                    DisplayCx = center.X,
                    DisplayCy = center.Y,
                };
                var largeWarp = Math.Abs(crop.WarpAngleDeg) >= LargeWarpNccTransAnchorDeg;
                var nccOk = NccAngleTrustworthy(ncc) && ncc.Score >= NccFallbackMinScore;
                var nccValidates = largeWarp && nccOk
                                    && hit.MeanDistPx <= ActiveMaxMeanDist * LargeWarpMeanSlackRatio;
                // 轻微尺度/大 warp 插值使 hit 跌到门下：NCC 角可信则放宽命中，避免把正确峰当拒识。
                if (hit.HitRate < ActiveMinHitRate && !(nccOk && hit.HitRate >= ActiveMinHitRate * 0.55))
                    return Attempt.Miss(hit.Viz);
                if (hit.MeanDistPx > ActiveMaxMeanDist && !nccValidates)
                    return Attempt.Miss(hit.Viz);

                var score = QualityScore(hit.MeanDistPx, hit.HitRate, ncc.Found ? ncc.Score : double.NaN, crop.WarpAngleDeg);
                return new Attempt(
                    new Result(angle, center, score, hit.MeanDistPx, hit.HitRate),
                    MapVizToSource(crop, hit.Viz));
            }
            finally
            {
                field?.Dispose();
            }
        }
        finally
        {
            crop.Upright.Dispose();
        }
    }


    public static void Warm(RecipeConfig recipe) => TeachCache.Warm(recipe);

    public static void Remove(string recipeName) => TeachCache.Remove(recipeName);

    /// <summary>归还 <see cref="GetOrCreate"/> 租约。与旋转/SIFT 缓存语义一致。</summary>
    public static void Release(ShapeModel? model) => TeachCache.Release(model);

    public static ShapeModel? GetOrCreate(RecipeConfig recipe) => TeachCache.GetOrCreate(recipe);

    internal static double QualityScore(double meanDist, double hitRate, double nccScore = double.NaN, double sceneWarpDeg = 0)
    {
        var dist = Math.Clamp(1.0 - meanDist / 8.0, 0, 1);
        var geo = Math.Clamp(0.55 * hitRate + 0.45 * dist, 0.15, 1);
        if (double.IsFinite(nccScore) && Math.Abs(sceneWarpDeg) >= LargeWarpNccTransAnchorDeg)
            return Math.Clamp(0.4 * geo + 0.6 * nccScore, 0.15, 1);
        return geo;
    }

    internal static string FormatQualityNote(double score, double matchThreshold, DebugInfo debug) =>
        $"命中 {debug.HitRate:P0} · 均距 {debug.MeanDist:0.1f}px · 残差 {debug.ResidualDeg:0.00}° · 方向一致 {debug.DirAgree:P0} · 分 {score:0.00} (门 {matchThreshold:0.00})";

    internal static Point2d DebugNccToModelCenter(ShapeModel model, double rotDegInUpright, Point2d templateCenter) =>
        NccCenterToModelCenter(model, rotDegInUpright, templateCenter);

    internal static (double Mean, double Hit) DebugChamferAt(
        Mat upright, ShapeModel model, double deg, Point2d center, double sceneWarpDeg,
        IReadOnlyList<Point2f>? contourInUpright = null)
    {
        using var field = ChamferField.Create(upright, model, sceneWarpDeg, contourInUpright);
        var pose = ScorePose(field, model, deg, center.X, center.Y, coarseFast: true);
        return (pose.Mean, pose.Hit);
    }

    private static bool ShouldCache(RecipeConfig recipe) =>
        recipe.AngleMode == AngleMode.MaskTemplate
        && recipe.Template.RefineMethod == SegmentRefineMethod.ShapeMatch
        && !string.IsNullOrEmpty(recipe.Template.TemplateImageBase64);

}
