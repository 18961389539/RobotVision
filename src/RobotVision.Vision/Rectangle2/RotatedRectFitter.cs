using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Vision;

/// <summary>鲁棒核：对标 HALCON <c>fit_rectangle2_contour_xld</c> 的 algorithm 参数。</summary>
public enum RectFitAlgorithm
{
    /// <summary>Huber 权（OpenCV FitLine 初值 + 迭代重权）。</summary>
    Huber = 0,

    /// <summary>Tukey 双平方权（默认，对离群点更激进）。</summary>
    Tukey = 1,
}

/// <summary>
/// 旋转矩形鲁棒拟合结果。角度为长轴方向（度，与 <see cref="AngleGeometry"/> 同口径）。
/// </summary>
public readonly record struct RotatedRectFitResult(
    Point2d Center,
    double AngleDeg,
    double LongLen,
    double ShortLen,
    int Inliers,
    double RmsPx,
    bool Ok);

/// <summary>
/// 轮廓点 → 旋转矩形（rectangle2）：四边分组 + 迭代 Tukey/Huber 直线拟合 + 平行线对求中心/尺寸。
/// 对标 HALCON <c>fit_rectangle2_contour_xld</c>（regression + 鲁棒权 + 端点裁剪）。
/// 轴近角用最近边分类；|cos4θ|≈1 时跳过几何 clip。半长分位精修后：一维角搜索 → 归属边 Tukey 重心 → 再角搜索（尺寸锁定）。
/// </summary>
internal static class RotatedRectFitter
{
    private const int MinPoints = 12;
    private const int MinPointsPerEdge = 4;
    private const double ConvergeAngleDeg = 0.02;
    private const double ConvergeCenterPx = 0.05;
    private const double ConvergeSizePx = 0.1;
    private const double ShortEdgeHalfLongPercentile = 0.5;
    private const double LongEdgeHalfShortPercentile = 0.42;
    private const double JitterBlendP42 = 0.55;
    private const double JitterBlendP38 = 0.42;
    private const double JitterHeavySpread = 0.35;
    private const double AxisNearShortEdgeHalfLongPercentileLo = 0.38;
    private const double AxisNearShortEdgeHalfLongPercentileMid = 0.42;
    private const int DefaultMaxIter = 12;
    private const double InlierSigma = 2.5;

    /// <param name="clipEndPoints">每边丢弃的端点个数（对标 HALCON clip_end_points）。</param>
    public static RotatedRectFitResult Fit(
        IReadOnlyList<Point2f> points,
        double? seedAngleDeg = null,
        RectFitAlgorithm algorithm = RectFitAlgorithm.Tukey,
        int clipEndPoints = 0,
        int maxIterations = DefaultMaxIter)
    {
        if (points.Count < MinPoints)
            return Fail();

        var rect = Cv2.MinAreaRect(points is Point2f[] arr ? arr : points.ToArray());
        var warp = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        var halfLong = Math.Max(rect.Size.Width, rect.Size.Height) * 0.5;
        var halfShort = Math.Min(rect.Size.Width, rect.Size.Height) * 0.5;
        if (halfLong < 4 || halfShort < 2)
            return Fail();

        double cx = rect.Center.X;
        double cy = rect.Center.Y;
        var obbHalfLong = halfLong;
        var obbHalfShort = halfShort;
        var work = ToPoint2d(points);
        ReadOnlySpan<(double Angle, double HalfLong, double HalfShort)> branches =
        [
            (warp, halfLong, halfShort),
            (warp + 90.0, halfShort, halfLong),
        ];

        RotatedRectFitResult? best = null;
        var bestScore = double.PositiveInfinity;
        foreach (var branch in branches)
        {
            var fit = FitOnce(
                work, cx, cy, branch.Angle, branch.HalfLong, branch.HalfShort,
                algorithm, clipEndPoints, maxIterations);
            if (!fit.Ok)
                continue;
            var dx = fit.Center.X - cx;
            var dy = fit.Center.Y - cy;
            var centerShift = Math.Sqrt(dx * dx + dy * dy);
            var shiftGate = Math.Max(10.0, 0.12 * 2 * obbHalfLong);
            if (centerShift > shiftGate)
            {
                var reanchored = ReanchorFitCenter(fit, cx, cy, work);
                if (!reanchored.Ok)
                    continue;
                fit = reanchored;
            }
            if (seedAngleDeg is { } seed &&
                AngleGeometry.UndirectedDeltaDeg(fit.AngleDeg, seed) > 12.0)
                continue;
            var score = BranchScore(fit, cx, cy, obbHalfLong, obbHalfShort, seedAngleDeg);
            if (score < bestScore)
            {
                bestScore = score;
                best = fit;
            }
        }

        var result = best ?? ObbFallback(rect, work);
        return result.Ok ? ApplyHalfExtentRefine(result, work, clipEndPoints) : result;
    }

    private static RotatedRectFitResult ApplyHalfExtentRefine(
        RotatedRectFitResult fit, Point2d[] work, int clipEndPoints = 2)
    {
        var classifyHalfLong = fit.LongLen * 0.5;
        var classifyHalfShort = fit.ShortLen * 0.5;
        var halfLong = classifyHalfLong;
        var halfShort = classifyHalfShort;
        // 轴近 / |cos4θ|≈1 时 FitOnce 已跳过几何 clip，半长精修须按 clip=0 做分位收缩。
        clipEndPoints = EffectiveClipEndPoints(fit.AngleDeg, clipEndPoints);
        for (var pass = 0; pass < 2; pass++)
        {
            RefineHalfExtentsFromClassifiedEdgeExtents(
                work, fit.Center.X, fit.Center.Y, fit.AngleDeg,
                classifyHalfLong, classifyHalfShort, clipEndPoints,
                ref halfLong, ref halfShort);
        }

        if (TryCollectShortEdgeCenterAbsU(
                work, fit.Center.X, fit.Center.Y, fit.AngleDeg, halfLong, halfShort, out var absU))
        {
            if (ShouldUseSoftCornerWeights(fit.AngleDeg) &&
                TryPercentile(absU, ShortEdgeHalfLongPercentile, out var estHalfLong))
            {
                var delta = estHalfLong - halfLong;
                if (delta > 0.12 && delta < 0.35)
                    halfLong = estHalfLong;

                var maxU = MaxAbsULongClassified(
                    work, fit.Center.X, fit.Center.Y, fit.AngleDeg,
                    classifyHalfLong, classifyHalfShort);
                if (maxU >= 2 && halfLong > maxU && halfLong - maxU < 0.05 && estHalfLong - halfLong < 0.05)
                    halfLong = maxU;

                // 高 jitter：p50 被噪声抬高且当前半长贴在 p50 附近时，向低分位回混。
                if (TryPercentile(absU, AxisNearShortEdgeHalfLongPercentileMid, out var p42Soft))
                {
                    var spread = estHalfLong - p42Soft;
                    if (spread > JitterHeavySpread &&
                        TryPercentile(absU, AxisNearShortEdgeHalfLongPercentileLo, out var p38Soft) &&
                        halfLong >= p38Soft && halfLong >= estHalfLong - 0.08)
                        halfLong = JitterBlendP38 * p38Soft + (1.0 - JitterBlendP38) * estHalfLong;
                    else if (spread > 0.18 && halfLong >= p42Soft && halfLong >= estHalfLong - 0.08)
                        halfLong = JitterBlendP42 * p42Soft + (1.0 - JitterBlendP42) * estHalfLong;
                    // 缺边：半长夹在略高的 p42 与 p50 之间（spread 不大，尚未贴上 p50）。
                    else if (halfLong < estHalfLong &&
                             estHalfLong - halfLong < 0.055 &&
                             halfLong > p42Soft + 0.04 &&
                             halfLong - p42Soft < 0.12 &&
                             spread < 0.16)
                        halfLong = p42Soft;
                }
            }
            else if (IsAxisNearDeg(fit.AngleDeg) &&
                     TryPercentile(absU, ShortEdgeHalfLongPercentile, out var p50) &&
                     TryPercentile(absU, AxisNearShortEdgeHalfLongPercentileMid, out var p42) &&
                     TryPercentile(absU, AxisNearShortEdgeHalfLongPercentileLo, out var p38))
            {
                var axisEst = halfLong > p50 + 0.055 ? p38
                    : halfLong > p50 + 0.02 ? p42
                    : p50;
                var delta = axisEst - halfLong;
                if (delta < -0.04 && delta > -0.40)
                    halfLong = axisEst;
                if (halfLong < p50 - 0.06)
                    halfLong = 0.5 * (halfLong + p50);
            }
            else if (IsCos4NearOne(fit.AngleDeg) &&
                     TryPercentile(absU, AxisNearShortEdgeHalfLongPercentileMid, out var cos4P42) &&
                     halfLong > cos4P42 + 0.02 && halfLong - cos4P42 < 0.12)
                halfLong = cos4P42;
        }

        if (IsAxisNearDeg(fit.AngleDeg) &&
            TryCollectLongEdgeCenterAbsV(
                work, fit.Center.X, fit.Center.Y, fit.AngleDeg, halfLong, halfShort, out var absV) &&
            TryPercentile(absV, ShortEdgeHalfLongPercentile, out var p50Short) &&
            halfShort > p50Short + 0.02 && halfShort - p50Short < 0.08)
            halfShort = p50Short;

        if (ShouldUseSoftCornerWeights(fit.AngleDeg) &&
            TryCollectLongEdgeCenterAbsV(
                work, fit.Center.X, fit.Center.Y, fit.AngleDeg, halfLong, halfShort, out var absVSoft) &&
            TryPercentile(absVSoft, LongEdgeHalfShortPercentile, out var p42Short))
        {
            if (halfShort > p42Short + 0.02 && halfShort - p42Short < 0.12)
                halfShort = p42Short;
            else if (halfShort < p42Short - 0.02 && p42Short - halfShort < 0.15)
                halfShort = p42Short;
        }

        var cx = fit.Center.X;
        var cy = fit.Center.Y;
        var angle = RefineAngleBySignedResidual(
            work, cx, cy, fit.AngleDeg, halfLong, halfShort);
        RefineCenterByAssignedResidual(work, ref cx, ref cy, angle, halfLong, halfShort);
        angle = RefineAngleBySignedResidual(work, cx, cy, angle, halfLong, halfShort);
        var sizeUnchanged =
            Math.Abs(halfLong - fit.LongLen * 0.5) < 1e-6 &&
            Math.Abs(halfShort - fit.ShortLen * 0.5) < 1e-6;
        var angleUnchanged = Math.Abs(AngleGeometry.SignedDeltaHalfDeg(angle, fit.AngleDeg)) < 1e-4;
        var centerUnchanged = Math.Abs(cx - fit.Center.X) < 1e-6 && Math.Abs(cy - fit.Center.Y) < 1e-6;
        if (sizeUnchanged && angleUnchanged && centerUnchanged)
            return fit;
        var (rms, inliers) = MeasureResiduals(work, cx, cy, angle, halfLong, halfShort);
        if (double.IsNaN(rms) || inliers < MinPoints)
            return fit;
        return fit with
        {
            Center = new Point2d(cx, cy),
            AngleDeg = AngleGeometry.NormalizeDeg(angle),
            LongLen = 2 * halfLong,
            ShortLen = 2 * halfShort,
            RmsPx = rms,
            Inliers = inliers,
        };
    }

    /// <summary>
    /// 固定中心与半长，在种子角附近最小化到四边的有符号残差平方和（对标 HALCON 整轮廓 Tukey 矩形，一维角搜索）。
    /// </summary>
    private static double RefineAngleBySignedResidual(
        Point2d[] work, double cx, double cy, double angleDeg, double halfLong, double halfShort)
    {
        var best = angleDeg;
        var bestCost = SignedRectCost(work, cx, cy, angleDeg, halfLong, halfShort);
        const double coarse = 0.05;
        const double span = 0.40;
        for (var d = -span; d <= span + 1e-9; d += coarse)
        {
            if (Math.Abs(d) < 1e-9)
                continue;
            var a = angleDeg + d;
            var cost = SignedRectCost(work, cx, cy, a, halfLong, halfShort);
            if (cost + 1e-12 < bestCost)
            {
                bestCost = cost;
                best = a;
            }
        }

        const double fine = 0.01;
        var fineBest = best;
        var fineCost = bestCost;
        for (var d = -coarse; d <= coarse + 1e-9; d += fine)
        {
            var a = best + d;
            var cost = SignedRectCost(work, cx, cy, a, halfLong, halfShort);
            if (cost + 1e-12 < fineCost)
            {
                fineCost = cost;
                fineBest = a;
            }
        }
        return fineBest;
    }

    /// <summary>
    /// 尺寸与角锁定：对归属边内点做 Tukey 加权有符号残差，平移中心使对边残差均值为 0。
    /// </summary>
    private static void RefineCenterByAssignedResidual(
        Point2d[] work, ref double cx, ref double cy, double angleDeg,
        double halfLong, double halfShort)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var uMargin = 0.82 * halfLong;
        var vMargin = 0.82 * halfShort;
        var rU = new List<double>();
        var rV = new List<double>();
        foreach (var p in work)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            var du = Math.Abs(u) - halfLong;
            var dv = Math.Abs(v) - halfShort;
            if (du > dv)
            {
                if (Math.Abs(v) > vMargin)
                    continue;
                var su = u >= 0 ? 1.0 : -1.0;
                rU.Add(su * du);
            }
            else
            {
                if (Math.Abs(u) > uMargin)
                    continue;
                var sv = v >= 0 ? 1.0 : -1.0;
                rV.Add(sv * dv);
            }
        }

        var duShift = WeightedSignedMean(rU);
        var dvShift = WeightedSignedMean(rV);
        if (double.IsNaN(duShift) && double.IsNaN(dvShift))
            return;
        duShift = double.IsNaN(duShift) ? 0.0 : Math.Clamp(duShift, -0.6, 0.6);
        dvShift = double.IsNaN(dvShift) ? 0.0 : Math.Clamp(dvShift, -0.6, 0.6);
        cx += duShift * cos - dvShift * sin;
        cy += duShift * sin + dvShift * cos;
    }

    private static double WeightedSignedMean(List<double> residuals)
    {
        if (residuals.Count < MinPointsPerEdge * 2)
            return double.NaN;
        var abs = residuals.Select(Math.Abs).OrderBy(x => x).ToArray();
        var scale = Math.Max(1.4826 * abs[abs.Length / 2], 0.25);
        var num = 0.0;
        var den = 0.0;
        foreach (var r in residuals)
        {
            var w = TukeyWeight(r / (2.0 * scale));
            num += w * r;
            den += w;
        }
        return den < 1e-9 ? double.NaN : num / den;
    }

    private static double SignedRectCost(
        Point2d[] pts, double cx, double cy, double angleDeg, double halfLong, double halfShort)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var sum = 0.0;
        foreach (var p in pts)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            var du = Math.Abs(u) - halfLong;
            var dv = Math.Abs(v) - halfShort;
            var r = du > dv ? du : dv;
            sum += r * r;
        }
        return sum;
    }

    /// <summary>迭代中心漂移过大时，保留角度/尺寸，回锚 OBB 中心并重算 RMS。</summary>
    private static RotatedRectFitResult ReanchorFitCenter(
        RotatedRectFitResult fit, double cx, double cy, Point2d[] work)
    {
        var halfLong = fit.LongLen * 0.5;
        var halfShort = fit.ShortLen * 0.5;
        var (rms, inliers) = MeasureResiduals(work, cx, cy, fit.AngleDeg, halfLong, halfShort);
        if (double.IsNaN(rms) || inliers < MinPoints)
            return Fail();
        return fit with
        {
            Center = new Point2d(cx, cy),
            RmsPx = rms,
            Inliers = inliers,
        };
    }

    private static RotatedRectFitResult ObbFallback(RotatedRect rect, Point2d[] work)
    {
        var halfLong = Math.Max(rect.Size.Width, rect.Size.Height) * 0.5;
        var halfShort = Math.Min(rect.Size.Width, rect.Size.Height) * 0.5;
        var warp = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        var (rms, inliers) = MeasureResiduals(work, rect.Center.X, rect.Center.Y, warp, halfLong, halfShort);
        if (double.IsNaN(rms) || inliers < MinPoints)
            return Fail();
        return new RotatedRectFitResult(
            new Point2d(rect.Center.X, rect.Center.Y),
            AngleGeometry.NormalizeDeg(warp),
            2 * halfLong,
            2 * halfShort,
            inliers,
            rms,
            true);
    }

    private static RotatedRectFitResult FitOnce(
        Point2d[] work,
        double cx,
        double cy,
        double angle,
        double halfLong,
        double halfShort,
        RectFitAlgorithm algorithm,
        int clipEndPoints,
        int maxIterations)
    {
        for (var iter = 0; iter < maxIterations; iter++)
        {
            var rad = angle * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);

            ClassifyEdges(work, cx, cy, cos, sin, halfLong, halfShort, angle,
                out var negLong, out var posLong, out var negShort, out var posShort);

            var effectiveClip = EffectiveClipEndPoints(angle, clipEndPoints);
            ClipEnds(negLong, effectiveClip, cos, sin, alongLong: true);
            ClipEnds(posLong, effectiveClip, cos, sin, alongLong: true);
            ClipEnds(negShort, effectiveClip, cos, sin, alongLong: false);
            ClipEnds(posShort, effectiveClip, cos, sin, alongLong: false);

            if (negLong.Count < MinPointsPerEdge || posLong.Count < MinPointsPerEdge ||
                negShort.Count < MinPointsPerEdge || posShort.Count < MinPointsPerEdge)
                break;

            var softCorner = effectiveClip <= 0 && ShouldUseSoftCornerWeights(angle);
            if (!FitLineRobust(negLong, algorithm, null, out var ln0, out var la0) ||
                !FitLineRobust(posLong, algorithm, null, out var ln1, out var la1) ||
                !FitLineRobust(negShort, algorithm,
                    ShortEdgeCornerContext(softCorner, cx, cy, cos, sin, halfLong, halfShort),
                    out var sn0, out var sa0) ||
                !FitLineRobust(posShort, algorithm,
                    ShortEdgeCornerContext(softCorner, cx, cy, cos, sin, halfLong, halfShort),
                    out var sn1, out var sa1))
                break;

            var laMid = angle + 0.5 * (
                AngleGeometry.SignedDeltaHalfDeg(la0, angle) +
                AngleGeometry.SignedDeltaHalfDeg(la1, angle));
            var deltaAngle = AngleGeometry.SignedDeltaHalfDeg(laMid, angle);
            angle = laMid;

            rad = angle * Math.PI / 180.0;
            cos = Math.Cos(rad);
            sin = Math.Sin(rad);
            var nShortX = -sin;
            var nShortY = cos;
            var nLongX = cos;
            var nLongY = sin;

            var dNegLong = UvOffset(ln0, cx, cy, cos, sin).V;
            var dPosLong = UvOffset(ln1, cx, cy, cos, sin).V;
            var dNegShort = UvOffset(sn0, cx, cy, cos, sin).U;
            var dPosShort = UvOffset(sn1, cx, cy, cos, sin).U;

            var newHalfLong = 0.5 * Math.Abs(dPosShort - dNegShort);
            var newHalfShort = 0.5 * Math.Abs(dPosLong - dNegLong);
            if (newHalfLong < 4 || newHalfShort < 2)
                break;

            var shiftShort = 0.5 * (dNegLong + dPosLong);
            var shiftLong = 0.5 * (dNegShort + dPosShort);
            var newCx = cx + nShortX * shiftShort + nLongX * shiftLong;
            var newCy = cy + nShortY * shiftShort + nLongY * shiftLong;

            if (Math.Abs(deltaAngle) < ConvergeAngleDeg &&
                Math.Abs(newCx - cx) < ConvergeCenterPx &&
                Math.Abs(newCy - cy) < ConvergeCenterPx &&
                Math.Abs(newHalfLong - halfLong) < ConvergeSizePx &&
                Math.Abs(newHalfShort - halfShort) < ConvergeSizePx)
            {
                cx = newCx;
                cy = newCy;
                halfLong = newHalfLong;
                halfShort = newHalfShort;
                break;
            }

            cx = newCx;
            cy = newCy;
            halfLong = newHalfLong;
            halfShort = newHalfShort;
        }

        RefineHalfExtentsFromClassifiedEdgeExtents(
            work, cx, cy, angle, halfLong, halfShort, clipEndPoints, ref halfLong, ref halfShort);

        var (rms, inliers) = MeasureResiduals(work, cx, cy, angle, halfLong, halfShort);
        if (double.IsNaN(rms) || inliers < MinPoints)
            return Fail();

        return new RotatedRectFitResult(
            new Point2d(cx, cy),
            AngleGeometry.NormalizeDeg(angle),
            2 * halfLong,
            2 * halfShort,
            inliers,
            rms,
            true);
    }

    private static double SeedAnglePenalty(double angleDeg, double? seedDeg) =>
        seedDeg is { } s ? 0.08 * AngleGeometry.UndirectedDeltaDeg(angleDeg, s) : 0.0;

    private static double BranchScore(
        RotatedRectFitResult fit,
        double obbCx,
        double obbCy,
        double obbHalfLong,
        double obbHalfShort,
        double? seedDeg)
    {
        var score = fit.RmsPx;
        var dx = fit.Center.X - obbCx;
        var dy = fit.Center.Y - obbCy;
        var centerShift = Math.Sqrt(dx * dx + dy * dy);
        score += 0.35 * centerShift;
        score += SeedAnglePenalty(fit.AngleDeg, seedDeg);
        var aspect = fit.LongLen / Math.Max(1.0, fit.ShortLen);
        var obbAspect = (2 * obbHalfLong) / Math.Max(1.0, 2 * obbHalfShort);
        if (aspect < 1.0)
            score += 50;
        if (Math.Abs(Math.Log(aspect / obbAspect)) > 0.35)
            score += 5;
        return score;
    }

    /// <summary>带全链路选项的轮廓拟合。</summary>
    public static RotatedRectFitResult Fit(
        IReadOnlyList<Point2f> points,
        double? seedAngleDeg,
        RectFitOptions options) =>
        ApplyConstraints(
            Fit(points, seedAngleDeg, options.ContourAlgorithm, options.ClipEndPoints),
            options.Constraints);

    private static RotatedRectFitResult ApplyConstraints(RotatedRectFitResult fit, RectFitConstraints? constraints) =>
        constraints is null ? fit : constraints.Apply(fit);

    private static bool IsAxisNearDeg(double angleDeg) =>
        Math.Abs(Math.Sin(2.0 * angleDeg * Math.PI / 180.0)) < 0.15;

    /// <summary>clip 在部分角（如 135°）会误删短边角点导致长边膨胀；轴近角与 |cos4θ|≈1 时跳过。</summary>
    private static int EffectiveClipEndPoints(double angleDeg, int clipEndPoints)
    {
        if (clipEndPoints <= 0)
            return 0;
        if (IsAxisNearDeg(angleDeg))
            return 0;
        var cos4 = Math.Cos(4.0 * angleDeg * Math.PI / 180.0);
        return Math.Abs(cos4) > 0.85 ? 0 : clipEndPoints;
    }

    /// <summary>轴近角用最近边分类；|cos4θ|≈1 时跳过 clip（如 135°）。</summary>
    private static bool PreferClosestEdgeClassification(double angleDeg)
    {
        if (IsAxisNearDeg(angleDeg))
            return true;
        // 仅轴近角用最近边；离轴带（如 ±18°）实测最近边无收益。
        return false;
    }

    /// <summary>离轴角 clip=0 时对沿边坐标做软角点降权（对标 Tukey 抑角点，非几何 clip）。</summary>
    private static bool ShouldUseSoftCornerWeights(double angleDeg) =>
        !IsAxisNearDeg(angleDeg) && Math.Abs(Math.Cos(4.0 * angleDeg * Math.PI / 180.0)) <= 0.85;

    private readonly record struct LineFitCornerContext(
        double Cx, double Cy, double Cos, double Sin,
        double HalfLong, double HalfShort, bool AlongLongEdge,
        double SoftStart = 0.82, double SoftEnd = 0.96);

    /// <summary>沿边坐标接近角区时平滑降权（与 ClipEnds 排序轴一致：长边 |u|、短边 |v|）。</summary>
    private static double SoftCornerAlongEdgeWeight(double along, double halfExtent, double start, double end)
    {
        if (halfExtent < 1e-6)
            return 1.0;
        var t = along / halfExtent;
        if (t <= start)
            return 1.0;
        if (t >= end)
            return 0.0;
        var x = (t - start) / (end - start);
        return 1.0 - x * x * (3.0 - 2.0 * x);
    }

    private static double CornerWeight(Point2d p, LineFitCornerContext ctx)
    {
        var (u, v) = UvOffset(p, ctx.Cx, ctx.Cy, ctx.Cos, ctx.Sin);
        if (ctx.AlongLongEdge)
            return SoftCornerAlongEdgeWeight(Math.Abs(u), ctx.HalfLong, ctx.SoftStart, ctx.SoftEnd);
        // 短边 |u|≈halfLong：法向近似均匀降权，等效强化 Tukey（clip=0 离轴角实证最稳）
        return SoftCornerAlongEdgeWeight(Math.Abs(u), ctx.HalfLong, ctx.SoftStart, ctx.SoftEnd);
    }

    private static LineFitCornerContext? ShortEdgeCornerContext(
        bool enabled, double cx, double cy, double cos, double sin,
        double halfLong, double halfShort) =>
        enabled
            ? new LineFitCornerContext(
                cx, cy, cos, sin, halfLong, halfShort, AlongLongEdge: false,
                SoftStart: 0.82, SoftEnd: 0.96)
            : null;

    private static double MaxAbsULongClassified(
        Point2d[] work, double cx, double cy, double angleDeg, double halfLong, double halfShort)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var max = 0.0;
        foreach (var p in work)
        {
            var (u, v) = UvOffset(p, cx, cy, cos, sin);
            var du = Math.Abs(u) - halfLong;
            var dv = Math.Abs(v) - halfShort;
            if (du <= dv)
                max = Math.Max(max, Math.Abs(u));
        }
        return max;
    }

    private static bool IsCos4NearOne(double angleDeg) =>
        Math.Abs(Math.Cos(4.0 * angleDeg * Math.PI / 180.0)) > 0.85;

    private static bool TryCollectLongEdgeCenterAbsV(
        Point2d[] work, double cx, double cy, double angleDeg,
        double halfLong, double halfShort, out List<double> absV)
    {
        absV = [];
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var margin = 0.82 * halfLong;
        foreach (var p in work)
        {
            var (u, v) = UvOffset(p, cx, cy, cos, sin);
            var du = Math.Abs(u) - halfLong;
            var dv = Math.Abs(v) - halfShort;
            if (du > dv || Math.Abs(u) > margin)
                continue;
            absV.Add(Math.Abs(v));
        }
        absV.Sort();
        return absV.Count >= MinPointsPerEdge * 2 && absV[^1] >= 2;
    }

    private static bool TryCollectShortEdgeCenterAbsU(
        Point2d[] work, double cx, double cy, double angleDeg,
        double halfLong, double halfShort, out List<double> absU)
    {
        absU = [];
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var margin = 0.82 * halfShort;
        foreach (var p in work)
        {
            var (u, v) = UvOffset(p, cx, cy, cos, sin);
            var du = Math.Abs(u) - halfLong;
            var dv = Math.Abs(v) - halfShort;
            if (du <= dv || Math.Abs(v) > margin)
                continue;
            absU.Add(Math.Abs(u));
        }
        absU.Sort();
        return absU.Count >= MinPointsPerEdge * 2 && absU[^1] >= 4;
    }

    private static bool TryPercentile(List<double> sorted, double percentile, out double value)
    {
        value = 0;
        if (sorted.Count == 0)
            return false;
        var i = (int)Math.Clamp(Math.Round(percentile * (sorted.Count - 1)), 0, sorted.Count - 1);
        value = sorted[i];
        return value >= 4;
    }

    private static void ClassifyEdges(
        IReadOnlyList<Point2d> pts, double cx, double cy, double cos, double sin,
        double halfLong, double halfShort, double angleDeg,
        out List<Point2d> negLong, out List<Point2d> posLong,
        out List<Point2d> negShort, out List<Point2d> posShort)
    {
        if (PreferClosestEdgeClassification(angleDeg))
            ClassifyEdgesByClosest(pts, cx, cy, cos, sin, halfLong, halfShort,
                out negLong, out posLong, out negShort, out posShort);
        else
            ClassifyEdgesByDuDv(pts, cx, cy, cos, sin, halfLong, halfShort,
                out negLong, out posLong, out negShort, out posShort);
    }

    private static void ClassifyEdgesByDuDv(
        IReadOnlyList<Point2d> pts, double cx, double cy, double cos, double sin,
        double halfLong, double halfShort,
        out List<Point2d> negLong, out List<Point2d> posLong,
        out List<Point2d> negShort, out List<Point2d> posShort)
    {
        negLong = [];
        posLong = [];
        negShort = [];
        posShort = [];
        foreach (var p in pts)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            var du = Math.Abs(u) - halfLong;
            var dv = Math.Abs(v) - halfShort;
            if (du > dv)
                (u < 0 ? negShort : posShort).Add(p);
            else
                (v < 0 ? negLong : posLong).Add(p);
        }
    }

    private static void ClassifyEdgesByClosest(
        IReadOnlyList<Point2d> pts, double cx, double cy, double cos, double sin,
        double halfLong, double halfShort,
        out List<Point2d> negLong, out List<Point2d> posLong,
        out List<Point2d> negShort, out List<Point2d> posShort)
    {
        negLong = [];
        posLong = [];
        negShort = [];
        posShort = [];
        const double tieEps = 1e-6;
        foreach (var p in pts)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            var dNegLong = Math.Abs(v + halfShort);
            var dPosLong = Math.Abs(v - halfShort);
            var dNegShort = Math.Abs(u + halfLong);
            var dPosShort = Math.Abs(u - halfLong);

            var best = 0;
            var bestD = dNegLong;
            void Consider(double d, int id)
            {
                if (d < bestD - tieEps || (Math.Abs(d - bestD) <= tieEps && id < best))
                {
                    bestD = d;
                    best = id;
                }
            }
            Consider(dPosLong, 1);
            Consider(dNegShort, 2);
            Consider(dPosShort, 3);

            switch (best)
            {
                case 0: negLong.Add(p); break;
                case 1: posLong.Add(p); break;
                case 2: negShort.Add(p); break;
                default: posShort.Add(p); break;
            }
        }
    }

    /// <summary>
    /// 短线线距 half 因角点抖动膨胀时：长边 max|u| 收紧 halfLong；长边 max|v| 收紧 halfShort。
    /// </summary>
    private static void RefineHalfExtentsFromClassifiedEdgeExtents(
        Point2d[] work, double cx, double cy, double angleDeg,
        double halfLong, double halfShort, int clipEndPoints,
        ref double halfLongOut, ref double halfShortOut)
    {
        if (IsAxisNearDeg(angleDeg))
            return;
        var cos4 = Math.Cos(4.0 * angleDeg * Math.PI / 180.0);
        if (Math.Abs(cos4) > 0.85)
            return;

        var longSlack = clipEndPoints <= 0 ? 0.08 : 0.2;
        var shortSlack = 0.05;

        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var maxAbsULong = 0.0;
        var maxAbsVLong = 0.0;
        var maxAbsVShort = 0.0;
        foreach (var p in work)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            var du = Math.Abs(u) - halfLong;
            var dv = Math.Abs(v) - halfShort;
            if (du <= dv)
            {
                maxAbsULong = Math.Max(maxAbsULong, Math.Abs(u));
                maxAbsVLong = Math.Max(maxAbsVLong, Math.Abs(v));
            }
            else
                maxAbsVShort = Math.Max(maxAbsVShort, Math.Abs(v));
        }

        if (maxAbsULong >= 2 && halfLongOut > maxAbsULong + longSlack)
            halfLongOut = maxAbsULong;
        var shortCap = maxAbsVLong >= 2 ? maxAbsVLong : maxAbsVShort;
        if (shortCap >= 2 && halfShortOut > shortCap + shortSlack)
            halfShortOut = shortCap;
    }

    /// <summary>沿边方向排序后裁掉两端各 <paramref name="clip"/> 个点。</summary>
    private static void ClipEnds(List<Point2d> edge, int clip, double cos, double sin, bool alongLong)
    {
        if (clip <= 0 || edge.Count <= 2 * clip + MinPointsPerEdge)
            return;
        edge.Sort((a, b) =>
        {
            var ta = alongLong ? a.X * cos + a.Y * sin : -a.X * sin + a.Y * cos;
            var tb = alongLong ? b.X * cos + b.Y * sin : -b.X * sin + b.Y * cos;
            return ta.CompareTo(tb);
        });
        edge.RemoveRange(edge.Count - clip, clip);
        edge.RemoveRange(0, clip);
    }

    private static bool FitLineRobust(
        List<Point2d> pts, RectFitAlgorithm algorithm, LineFitCornerContext? cornerCtx,
        out Point2d point, out double angleDeg)
    {
        point = default;
        angleDeg = 0;
        if (pts.Count < MinPointsPerEdge)
            return false;

        var cornerMul = new double[pts.Count];
        if (cornerCtx is { } ctx)
        {
            for (var i = 0; i < pts.Count; i++)
                cornerMul[i] = CornerWeight(pts[i], ctx);
        }
        else
            Array.Fill(cornerMul, 1.0);

        var arr = pts.ConvertAll(p => new Point2f((float)p.X, (float)p.Y)).ToArray();
        var line = Cv2.FitLine(arr, DistanceTypes.Huber, 0, 0.01, 0.01);
        var vx = line.Vx;
        var vy = line.Vy;
        angleDeg = Math.Atan2(vy, vx) * 180.0 / Math.PI;
        point = new Point2d(line.X1, line.Y1);

        var weights = new double[pts.Count];
        Array.Fill(weights, 1.0);
        var robustIters = cornerCtx is null ? 6 : 8;
        for (var iter = 0; iter < robustIters; iter++)
        {
            var nx = -Math.Sin(angleDeg * Math.PI / 180.0);
            var ny = Math.Cos(angleDeg * Math.PI / 180.0);
            var dists = new double[pts.Count];
            for (var i = 0; i < pts.Count; i++)
            {
                var d = (pts[i].X - point.X) * nx + (pts[i].Y - point.Y) * ny;
                dists[i] = d;
            }

            var scale = RobustScale(dists);
            if (scale < 1e-6)
                break;

            var wSum = 0.0;
            var cx = 0.0;
            var cy = 0.0;
            for (var i = 0; i < pts.Count; i++)
            {
                var w = algorithm == RectFitAlgorithm.Tukey
                    ? TukeyWeight(dists[i] / scale)
                    : HuberWeight(dists[i] / scale);
                w *= cornerMul[i];
                weights[i] = w;
                wSum += w;
                cx += w * pts[i].X;
                cy += w * pts[i].Y;
            }
            if (wSum < 1e-9)
                break;
            cx /= wSum;
            cy /= wSum;

            var cxx = 0.0;
            var cxy = 0.0;
            var cyy = 0.0;
            for (var i = 0; i < pts.Count; i++)
            {
                var w = weights[i];
                var dx = pts[i].X - cx;
                var dy = pts[i].Y - cy;
                cxx += w * dx * dx;
                cxy += w * dx * dy;
                cyy += w * dy * dy;
            }

            var trace = cxx + cyy;
            var det = cxx * cyy - cxy * cxy;
            var disc = Math.Sqrt(Math.Max(0, trace * trace * 0.25 - det));
            var lambda1 = trace * 0.5 + disc;
            var evx = Math.Abs(cxy) > 1e-12 ? lambda1 - cyy : 1.0;
            var evy = Math.Abs(cxy) > 1e-12 ? cxy : 0.0;
            var len = Math.Sqrt(evx * evx + evy * evy);
            if (len < 1e-9)
                break;
            vx = evx / len;
            vy = evy / len;
            angleDeg = Math.Atan2(vy, vx) * 180.0 / Math.PI;
            point = new Point2d(cx, cy);
        }

        return true;
    }

    private static (double U, double V) UvOffset(Point2d p, double cx, double cy, double cos, double sin)
    {
        var dx = p.X - cx;
        var dy = p.Y - cy;
        return (dx * cos + dy * sin, -dx * sin + dy * cos);
    }

    private static (double Rms, int Inliers) MeasureResiduals(
        Point2d[] pts, double cx, double cy, double angleDeg, double halfLong, double halfShort)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var sumSq = 0.0;
        var inliers = 0;
        foreach (var p in pts)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            var du = Math.Max(0, Math.Abs(u) - halfLong);
            var dv = Math.Max(0, Math.Abs(v) - halfShort);
            var d = Math.Sqrt(du * du + dv * dv);
            sumSq += d * d;
            if (d <= InlierSigma)
                inliers++;
        }
        return (Math.Sqrt(sumSq / pts.Length), inliers);
    }

    private static double RobustScale(double[] dists)
    {
        var abs = dists.Select(Math.Abs).OrderBy(x => x).ToArray();
        var med = abs[abs.Length / 2];
        return Math.Max(1.4826 * med, 0.5);
    }

    private static double TukeyWeight(double r) =>
        Math.Abs(r) >= 1.0 ? 0.0 : Math.Pow(1.0 - r * r, 2);

    private static double HuberWeight(double r) =>
        Math.Abs(r) <= 1.0 ? 1.0 : 1.0 / Math.Abs(r);

    private static Point2d[] ToPoint2d(IReadOnlyList<Point2f> points)
    {
        var arr = new Point2d[points.Count];
        for (var i = 0; i < points.Count; i++)
            arr[i] = new Point2d(points[i].X, points[i].Y);
        return arr;
    }

    private static RotatedRectFitResult Fail() =>
        new(default, 0, 0, 0, 0, double.NaN, false);
}
