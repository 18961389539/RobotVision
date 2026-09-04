using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Vision;

/// <summary>
/// 旋转矩形亚像素精修：原图四边布卡尺（对标 HALCON <c>measure_pairs</c> / <c>gen_measure_rectangle2</c>），
/// 长边对定角与中心、短边对定尺寸。质量不过门返回 null，调用方回退轮廓级结果。
/// </summary>
internal static class RotatedRectSubpixel
{
    private const int MinPointsPerEdge = 6;
    private const double MaxParallelDeg = 3.5;
    private const double WidthRatioLo = 0.55;
    private const double WidthRatioHi = 1.65;
    private const double SearchMinPx = 6.0;
    private const double SearchMaxPx = 24.0;
    private const double SearchShortRatio = 0.28;
    private const double InlierTolPx = 1.5;

    public readonly record struct Result(
        Point2d Center,
        double AngleDeg,
        double LongLen,
        double ShortLen,
        double MaxParallelDeg,
        int Inliers,
        double RmsPx);

    public static Result? Refine(
        Mat gray,
        Point2d center,
        double longLen,
        double shortLen,
        double seedAngleDeg,
        RectEdgePolarity polarity = RectEdgePolarity.Any) =>
        Refine(gray, center, longLen, shortLen, seedAngleDeg,
            RectFitOptions.Default with { EdgePolarity = polarity });

    public static Result? Refine(
        Mat gray,
        Point2d center,
        double longLen,
        double shortLen,
        double seedAngleDeg,
        RectFitOptions options)
    {
        if (gray is null || gray.Empty() || gray.Channels() != 1)
            return null;
        if (longLen < 24 || shortLen < 8)
            return null;

        var polarity = options.EdgePolarity;
        var measureMode = options.EdgeMeasureMode;
        var lockAngle = options.Constraints.FixedAngleDeg.HasValue;
        var lockSize = options.Constraints.FixedLongLenPx.HasValue &&
                       options.Constraints.FixedShortLenPx.HasValue;
        if (lockSize)
        {
            longLen = options.Constraints.FixedLongLenPx!.Value;
            shortLen = options.Constraints.FixedShortLenPx!.Value;
        }

        var theta = lockAngle
            ? DirectedTrigAngle(options.Constraints.FixedAngleDeg!.Value)
            : DirectedTrigAngle(seedAngleDeg);
        var rad = theta * Math.PI / 180.0;
        var dirX = Math.Cos(rad);
        var dirY = Math.Sin(rad);
        var nrmX = -dirY;
        var nrmY = dirX;

        var halfLong = longLen / 2.0;
        var halfShort = shortLen / 2.0;
        var search = Math.Clamp(SearchShortRatio * shortLen, SearchMinPx, SearchMaxPx);
        var searchI = (int)Math.Ceiling(search);

        var longNeg = new List<Point2d>();
        var longPos = new List<Point2d>();
        var shortNeg = new List<Point2d>();
        var shortPos = new List<Point2d>();

        var longProbes = MaskHousing.ProbeCount(longLen);
        var longInset = MaskHousing.ProbeInsetRatio(longLen, shortLen, probeAlongLong: true);
        var longSpan = halfLong * (1.0 - 2.0 * longInset);
        if (longSpan < 8)
            return null;

        for (var i = 0; i < longProbes; i++)
        {
            var t = longProbes == 1 ? 0.0 : -longSpan + 2.0 * longSpan * i / (longProbes - 1);
            var bx = center.X + dirX * t;
            var by = center.Y + dirY * t;
            if (RectEdgeSampler.TryMeasurePair(gray, bx, by, dirX, dirY, nrmX, nrmY, halfShort, searchI, polarity, measureMode, out var offNeg, out var offPos))
            {
                longNeg.Add(new Point2d(bx + nrmX * offNeg, by + nrmY * offNeg));
                longPos.Add(new Point2d(bx + nrmX * offPos, by + nrmY * offPos));
            }
            else
            {
                if (RectEdgeSampler.TryMeasure(gray, bx, by, dirX, dirY, nrmX, nrmY, +halfShort, searchI, polarity, measureMode, out var offP))
                    longPos.Add(new Point2d(bx + nrmX * (+halfShort + offP), by + nrmY * (+halfShort + offP)));
                if (RectEdgeSampler.TryMeasure(gray, bx, by, dirX, dirY, nrmX, nrmY, -halfShort, searchI, FlipPolarity(polarity), measureMode, out var offM))
                    longNeg.Add(new Point2d(bx + nrmX * (-halfShort + offM), by + nrmY * (-halfShort + offM)));
            }
        }

        // 沿法向内缩布点、切向量测长边；探针数跟长边尺度（shortLen 仅 ~4 根，达不到 MinPointsPerEdge）
        var shortProbes = MaskHousing.ProbeCount(longLen);
        var shortInset = MaskHousing.ProbeInsetRatio(shortLen, longLen, probeAlongLong: false);
        var shortSpan = halfShort * (1.0 - 2.0 * shortInset);
        if (shortSpan < 6)
            return null;

        for (var i = 0; i < shortProbes; i++)
        {
            var t = shortProbes == 1 ? 0.0 : -shortSpan + 2.0 * shortSpan * i / (shortProbes - 1);
            var bx = center.X + nrmX * t;
            var by = center.Y + nrmY * t;
            if (RectEdgeSampler.TryMeasure(gray, bx, by, nrmX, nrmY, dirX, dirY, +halfLong, searchI, polarity, measureMode, out var offP))
                shortPos.Add(new Point2d(bx + dirX * (+halfLong + offP), by + dirY * (+halfLong + offP)));
            if (RectEdgeSampler.TryMeasure(gray, bx, by, nrmX, nrmY, dirX, dirY, -halfLong, searchI, FlipPolarity(polarity), measureMode, out var offM))
                shortNeg.Add(new Point2d(bx + dirX * (-halfLong + offM), by + dirY * (-halfLong + offM)));
        }

        if (longNeg.Count < MinPointsPerEdge || longPos.Count < MinPointsPerEdge)
            return null;

        double refinedHalf;
        double parallel;
        if (lockAngle)
        {
            refinedHalf = SnapUndirectedToSeed(AngleGeometry.NormalizeDeg(theta), theta);
            parallel = FitLineAngle(longNeg, out var aNeg) && FitLineAngle(longPos, out var aPos)
                ? AngleGeometry.UndirectedDeltaDeg(aNeg, aPos)
                : 0;
        }
        else
        {
            if (!FitLineAngle(longNeg, out var aNeg) || !FitLineAngle(longPos, out var aPos))
                return null;

            refinedHalf = AngleGeometry.NormalizeDeg(
                theta + 0.5 * (AngleGeometry.SignedDeltaHalfDeg(aNeg, theta) + AngleGeometry.SignedDeltaHalfDeg(aPos, theta)));
            refinedHalf = SnapUndirectedToSeed(refinedHalf, theta);
            parallel = AngleGeometry.UndirectedDeltaDeg(aNeg, aPos);
        }

        if (parallel > MaxParallelDeg)
            return null;

        rad = refinedHalf * Math.PI / 180.0;
        dirX = Math.Cos(rad);
        dirY = Math.Sin(rad);
        nrmX = -dirY;
        nrmY = dirX;

        var dPos = MeanNormalOffset(longPos, center, nrmX, nrmY, out var inPos);
        var dNeg = MeanNormalOffset(longNeg, center, nrmX, nrmY, out var inNeg);
        var measuredShort = Math.Abs(dPos - dNeg);
        if (!lockSize)
        {
            var shortRatio = measuredShort / shortLen;
            if (shortRatio < WidthRatioLo || shortRatio > WidthRatioHi)
                return null;
        }
        else
            measuredShort = shortLen;

        var shiftShort = 0.5 * (dNeg + dPos);
        var cx = center.X + nrmX * shiftShort;
        var cy = center.Y + nrmY * shiftShort;

        double measuredLong = longLen;
        var inShort = 0;
        if (shortNeg.Count >= MinPointsPerEdge && shortPos.Count >= MinPointsPerEdge)
        {
            var uPos = MeanTangentOffset(shortPos, cx, cy, dirX, dirY, out var inSp);
            var uNeg = MeanTangentOffset(shortNeg, cx, cy, dirX, dirY, out var inSn);
            var candidateLong = Math.Abs(uPos - uNeg);
            var shiftLong = 0.5 * (uNeg + uPos);
            if (lockSize)
            {
                measuredLong = longLen;
                cx += dirX * shiftLong;
                cy += dirY * shiftLong;
                inShort = inSn + inSp;
            }
            else
            {
                var longRatio = candidateLong / longLen;
                if (longRatio >= WidthRatioLo && longRatio <= WidthRatioHi)
                {
                    measuredLong = candidateLong;
                    cx += dirX * shiftLong;
                    cy += dirY * shiftLong;
                    inShort = inSn + inSp;
                }
            }
        }

        var inliers = inPos + inNeg + inShort;
        var rms = MeasureRms(longNeg, longPos, shortNeg, shortPos, cx, cy, dirX, dirY, nrmX, nrmY,
            measuredLong, measuredShort);

        return options.Constraints.Apply(new Result(
            new Point2d(cx, cy),
            SnapUndirectedToSeed(AngleGeometry.NormalizeDeg(refinedHalf), theta),
            measuredLong,
            measuredShort,
            parallel,
            inliers,
            rms));
    }

    public static RotatedRectFitResult RefineFromContour(
        Mat gray,
        IReadOnlyList<Point2f> contour,
        double? seedAngleDeg = null,
        RectFitOptions? options = null)
    {
        options ??= RectFitOptions.Default;
        var core = options.StripTabProtrusion ? MaskHousing.CorePoints(contour) : contour;
        var contourFit = RotatedRectFitter.Fit(core, seedAngleDeg, options);
        if (!contourFit.Ok)
            return contourFit;

        var sub = Refine(
            gray,
            contourFit.Center,
            contourFit.LongLen,
            contourFit.ShortLen,
            DirectedTrigAngle(contourFit.AngleDeg, core),
            options);
        if (sub is null)
            return contourFit;

        var shiftPx = Math.Sqrt(
            Math.Pow(sub.Value.Center.X - contourFit.Center.X, 2) +
            Math.Pow(sub.Value.Center.Y - contourFit.Center.Y, 2));
        if (shiftPx > Math.Max(8.0, 0.35 * contourFit.ShortLen))
            return contourFit;

        return options.Constraints.Apply(new RotatedRectFitResult(
            sub.Value.Center,
            sub.Value.AngleDeg,
            sub.Value.LongLen,
            sub.Value.ShortLen,
            sub.Value.Inliers,
            sub.Value.RmsPx,
            true));
    }

    public static RotatedRectFitResult RefineFromContour(
        Mat gray,
        IReadOnlyList<Point2f> contour,
        double? seedAngleDeg,
        RectEdgePolarity polarity) =>
        RefineFromContour(gray, contour, seedAngleDeg, RectFitOptions.Default with { EdgePolarity = polarity });

    public static double QualityScore(Result r)
    {
        var angleTerm = Math.Clamp(1.0 - r.MaxParallelDeg / MaxParallelDeg, 0, 1);
        var rmsTerm = Math.Clamp(1.0 - r.RmsPx / 2.0, 0, 1);
        var inlierTerm = Math.Clamp(r.Inliers / 48.0, 0, 1);
        var nrms = r.ShortLen > 1e-3 ? r.RmsPx / r.ShortLen : double.NaN;
        var nrmsTerm = double.IsFinite(nrms) ? Math.Clamp(1.0 - nrms / 0.08, 0, 1) : rmsTerm;
        return 0.35 * angleTerm + 0.30 * rmsTerm + 0.20 * inlierTerm + 0.15 * nrmsTerm;
    }

    private static double MeanNormalOffset(List<Point2d> pts, Point2d center, double nx, double ny, out int inliers)
    {
        inliers = 0;
        if (pts.Count == 0)
            return 0;
        var mean = 0.0;
        foreach (var p in pts)
            mean += (p.X - center.X) * nx + (p.Y - center.Y) * ny;
        mean /= pts.Count;
        foreach (var p in pts)
        {
            var v = (p.X - center.X) * nx + (p.Y - center.Y) * ny;
            if (Math.Abs(v - mean) <= InlierTolPx)
                inliers++;
        }
        return mean;
    }

    private static double MeanTangentOffset(List<Point2d> pts, double cx, double cy, double tx, double ty, out int inliers)
    {
        inliers = 0;
        if (pts.Count == 0)
            return 0;
        var mean = 0.0;
        foreach (var p in pts)
            mean += (p.X - cx) * tx + (p.Y - cy) * ty;
        mean /= pts.Count;
        foreach (var p in pts)
        {
            var v = (p.X - cx) * tx + (p.Y - cy) * ty;
            if (Math.Abs(v - mean) <= InlierTolPx)
                inliers++;
        }
        return mean;
    }

    private static double MeasureRms(
        List<Point2d> longNeg, List<Point2d> longPos,
        List<Point2d> shortNeg, List<Point2d> shortPos,
        double cx, double cy, double dirX, double dirY, double nrmX, double nrmY,
        double longLen, double shortLen)
    {
        var halfLong = longLen / 2.0;
        var halfShort = shortLen / 2.0;
        var sum = 0.0;
        var n = 0;
        foreach (var batch in new[] { longNeg, longPos, shortNeg, shortPos })
        {
            foreach (var p in batch)
            {
                var u = (p.X - cx) * dirX + (p.Y - cy) * dirY;
                var v = (p.X - cx) * nrmX + (p.Y - cy) * nrmY;
                var du = Math.Max(0, Math.Abs(u) - halfLong);
                var dv = Math.Max(0, Math.Abs(v) - halfShort);
                sum += du * du + dv * dv;
                n++;
            }
        }
        return n == 0 ? double.NaN : Math.Sqrt(sum / n);
    }

    private static bool FitLineAngle(List<Point2d> pts, out double angleDeg)
    {
        angleDeg = 0;
        if (pts.Count < MinPointsPerEdge)
            return false;
        var arr = pts.ConvertAll(p => new Point2f((float)p.X, (float)p.Y)).ToArray();
        var line = Cv2.FitLine(arr, DistanceTypes.Huber, 0, 0.01, 0.01);
        angleDeg = AngleGeometry.NormalizeDeg(Math.Atan2(line.Vy, line.Vx) * 180.0 / Math.PI);
        return true;
    }

    private static RectEdgePolarity FlipPolarity(RectEdgePolarity p) => p switch
    {
        RectEdgePolarity.DarkToBright => RectEdgePolarity.BrightToDark,
        RectEdgePolarity.BrightToDark => RectEdgePolarity.DarkToBright,
        _ => RectEdgePolarity.Any,
    };

    private static double SnapUndirectedToSeed(double angleDeg, double seedDeg)
    {
        var baseAngle = AngleGeometry.NormalizeDeg(angleDeg);
        var alt = AngleGeometry.NormalizeDeg(angleDeg + 180.0);
        var d0 = Math.Abs(AngleGeometry.SignedDeltaHalfDeg(baseAngle, seedDeg));
        var d1 = Math.Abs(AngleGeometry.SignedDeltaHalfDeg(alt, seedDeg));
        return d1 < d0 ? alt : baseAngle;
    }

    private static double DirectedTrigAngle(double angleDeg, IReadOnlyList<Point2f>? contour = null)
    {
        if (contour is { Count: >= 4 })
        {
            var obb = Cv2.MinAreaRect(contour is Point2f[] a ? a : contour.ToArray());
            var warp = obb.Size.Width >= obb.Size.Height ? obb.Angle : obb.Angle + 90.0;
            if (AngleGeometry.UndirectedDeltaDeg(angleDeg, warp) < 0.5)
                return warp;
        }
        var n = AngleGeometry.NormalizeDeg(angleDeg);
        return n > 90.0 ? n - 180.0 : n;
    }
}
