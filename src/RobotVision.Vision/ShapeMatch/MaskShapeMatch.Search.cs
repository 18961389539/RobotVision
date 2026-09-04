using OpenCvSharp;
using RobotVision.Core.Geometry;
using System.Runtime.CompilerServices;

namespace RobotVision.Vision;

public static partial class MaskShapeMatch
{
    private sealed record MatchHit(
        double RotationDeg, Point2d CenterInUpright, double MeanDistPx, double HitRate, ShapeViz Viz,
        double DirAgree = double.NaN);

    private static MatchHit? MatchOnField(
        ChamferField field, ShapeModel model, double rangeDeg, NccSeed nccSeed, double sceneWarpDeg)
    {
        var gray = field.Gray;
        var canonMapped = field.CanonMapped;
        var invSx = field.InvSx;
        var invSy = field.InvSy;
        var teachSx = field.TeachSx;
        var teachSy = field.TeachSy;
        var scale = field.Scale;
        var dt = field.Dt;
        var nccAngleOk = NccAngleTrustworthy(nccSeed);
        var nccTransOk = nccAngleOk && nccSeed.Score >= 0.35;
        var bandDeg = nccAngleOk
            ? nccSeed.RotationDeg
            : ActiveOptions.AngleStartDeg;
        var rotRange = nccSeed.Found ? nccSeed.RotRange : rangeDeg;
        var ax0 = scale.Active ? scale.Ax : 1.0;
        var ay0 = scale.Active ? scale.Ay : 1.0;
        var cx0 = gray.Width / 2.0 + model.HousingOffsetX * ax0;
        var cy0 = gray.Height / 2.0 + model.HousingOffsetY * ay0;
        var span = Math.Max(10, Math.Min(MaxSearchSpanPx, Math.Max(gray.Width, gray.Height) * 0.10));
        var coarseRotStep = nccAngleOk && rotRange <= 3.0
            ? 1.5
            : Math.Max(1.0, ActiveOptions.CoarseRotStep);
        var useNccTrans = nccTransOk && Math.Abs(sceneWarpDeg) >= LargeWarpNccTransAnchorDeg;
        var nccModelCenter = nccSeed.Found
            ? MapLiveToTeach(
                NccCenterToModelCenter(model, bandDeg, nccSeed.Center), canonMapped, teachSx, teachSy)
            : default;
        PoseCand? seed = null;
        var pyramidLevels = 1;
        if (!useNccTrans
            && !nccAngleOk
            && ActiveOptions.ClampedNumLevels >= 2
            && !NccFlipAmbiguous(nccSeed)
            && gray.Width >= 80
            && gray.Height >= 80)
        {
            pyramidLevels = ActiveOptions.ClampedNumLevels >= 3 && gray.Width >= 160 && gray.Height >= 160
                ? 3
                : 2;
            seed = SearchPyramid(gray, model, sceneWarpDeg, rotRange, bandDeg, span, scale, pyramidLevels);
        }
        double bandMx, bandMy, coarseSpan;
        var coarseTransStep = 3;
        if (useNccTrans)
        {
            bandMx = nccModelCenter.X;
            bandMy = nccModelCenter.Y;
            coarseSpan = Math.Max(5, span * 0.4);
            coarseTransStep = 5;
        }
        else if (nccTransOk && nccSeed.Found)
        {
            var nccC = MapLiveToTeach(
                NccCenterToModelCenter(model, bandDeg, nccSeed.Center), canonMapped, teachSx, teachSy);
            bandMx = nccC.X;
            bandMy = nccC.Y;
            coarseSpan = Math.Max(8, span * 0.5);
            coarseTransStep = 4;
        }
        else if (seed is { } pSeed)
        {
            bandMx = pSeed.Mx;
            bandMy = pSeed.My;
            coarseSpan = Math.Max(10, span * 0.55);
            coarseTransStep = 4;
        }
        else
        {
            bandMx = cx0;
            bandMy = cy0;
            coarseSpan = span;
        }
        var searchDbg = ActiveOptions.EmitSearchDebug ? new int[2] : null;
        var coarse = Search(field, model, rotRange, bandDeg, bandMx, bandMy, coarseSpan,
            transStep: coarseTransStep, rotStep: coarseRotStep, coarseFast: true, evalSlot: searchDbg, evalIndex: 0);
        if (coarse is not { } c0)
            return null;
        PoseCand micro;
        if (coarse is { } c)
        {
            var microSeed = c.Cost < 2.5 && nccAngleOk
                ? c
                : Search(field, model, Math.Min(1.8, rotRange), c.Deg, c.Mx, c.My, 6,
                    transStep: 1, rotStep: 0.5, coarseFast: true, evalSlot: searchDbg, evalIndex: 1) ?? c;
            micro = RefineSubpixel(field, model, microSeed, fast: nccAngleOk && microSeed.Cost < 3.2);
            if (micro.Hit < 0.40)
                micro = RefineScale(field, model, micro);
        }
        else
        {
            return null;
        }
        if (micro.Hit < ActiveMinHitRate - 1e-6 && useNccTrans && nccSeed.Found
            && NccAngleTrustworthy(nccSeed) && nccSeed.Score >= NccFallbackMinScore)
        {
            var nccPose = ScorePose(field, model, bandDeg, nccModelCenter.X, nccModelCenter.Y);
            if (nccPose.Cost < micro.Cost - 1e-9)
                micro = RefineSubpixel(field, model, nccPose, fast: true);
        }
        if (NccFlipAmbiguous(nccSeed) && micro.Hit < 0.55)
        {
            var altCoarse = Search(field, model, rotRange, 0, cx0, cy0, span, transStep: 2, rotStep: 0.75, coarseFast: true);
            if (altCoarse is { } ac)
            {
                var altFine = Search(field, model, Math.Min(2.5, rotRange), ac.Deg, ac.Mx, ac.My, 10,
                    transStep: 1, rotStep: 0.5, coarseFast: true);
                var altMicro = RefineSubpixel(field, model, altFine ?? ac);
                if (altMicro.Cost < micro.Cost - 1e-9
                    || (Math.Abs(altMicro.Deg) + 1e-6 < Math.Abs(micro.Deg) && altMicro.Cost <= micro.Cost * 1.04))
                    micro = altMicro;
            }
        }
        micro = RefineCenterFine(field, model, micro);
        ShapeSearchDebug? dbg = null;
        if (searchDbg is not null)
            dbg = new ShapeSearchDebug(searchDbg[0], searchDbg[1], micro.Cost, pyramidLevels);
        var viz = ActiveOptions.EnableVisualization
            ? BuildViz(field.Dt, model.Points, micro.Deg, micro.Mx, micro.My, scale, dbg, pyramidLevels)
            : ShapeViz.Empty;
        var centerLive = MapTeachToLive(new Point2d(micro.Mx, micro.My), canonMapped, invSx, invSy);
        return new MatchHit(micro.Deg, centerLive, micro.Mean, micro.Hit, viz,
            micro.DirAgree);
    }

    /// <summary>大 warp 时将现场灰度重采样到示教尺寸，在统一像素格上做 Chamfer。</summary>
    private static Mat PrepareMatchGray(
        Mat upright, ShapeModel model, double sceneWarpDeg,
        out bool canonMapped, out double invSx, out double invSy)
    {
        canonMapped = false;
        invSx = invSy = 1.0;
        if (model.NccGray.Empty())
            return ToGray(upright);
        var tw = model.NccGray.Width;
        var th = model.NccGray.Height;
        if (upright.Width == tw && upright.Height == th)
            return ToGray(upright);
        var liveSmaller = upright.Width < tw || upright.Height < th;
        var (hw, hh) = PartHalfExtents(upright.Width, upright.Height);
        var slightScale = model.PartHalfW > 1e-6 && model.PartHalfH > 1e-6
                        && IsSlightScale(hw / model.PartHalfW, hh / model.PartHalfH);
        // 现场窗 ≥ 示教：小 warp / 轻微放大可在原生格 + ChamferScale 上搜（NCC 模板能放下）。
        // 现场窗更小（零件收缩）必须升采样，否则 MatchBest 因模板大于搜索图而跳过全部角。
        if (!liveSmaller && (slightScale || Math.Abs(sceneWarpDeg) < CanonChamferWarpDeg))
            return ToGray(upright);
        using var src = ToGray(upright);
        var dst = new Mat();
        Cv2.Resize(src, dst, new Size(tw, th), 0, 0, InterpolationFlags.Cubic);
        canonMapped = true;
        invSx = upright.Width / (double)tw;
        invSy = upright.Height / (double)th;
        return dst;
    }

    private static bool IsSlightScale(double sx, double sy) =>
        Math.Abs(sx - 1.0) < 0.08 && Math.Abs(sy - 1.0) < 0.08
        && (Math.Abs(sx - 1.0) > 0.008 || Math.Abs(sy - 1.0) > 0.008);

    private static Point2d MapTeachToLive(Point2d teach, bool canonMapped, double invSx, double invSy) =>
        canonMapped ? new Point2d(teach.X * invSx, teach.Y * invSy) : teach;

    private static Point2d MapLiveToTeach(Point2d live, bool canonMapped, double sx, double sy) =>
        canonMapped ? new Point2d(live.X * sx, live.Y * sy) : live;

    /// <summary>分割轮廓映到转正窗像素：与 WarpAffine 同口径（源→目的须 Invert 旋转矩阵）。</summary>
    internal static Point2f[] ContourInUpright(UprightCropResult crop, IReadOnlyList<Point2f> contour)
    {
        using var m = Cv2.GetRotationMatrix2D(crop.RotationCenter, -crop.WarpAngleDeg, 1.0);
        using var mInv = new Mat();
        Cv2.InvertAffineTransform(m, mInv);
        var a00 = mInv.At<double>(0, 0);
        var a01 = mInv.At<double>(0, 1);
        var a02 = mInv.At<double>(0, 2);
        var a10 = mInv.At<double>(1, 0);
        var a11 = mInv.At<double>(1, 1);
        var a12 = mInv.At<double>(1, 2);
        var ox = crop.CropOriginX;
        var oy = crop.CropOriginY;
        var pts = new Point2f[contour.Count];
        for (var i = 0; i < contour.Count; i++)
        {
            var x = contour[i].X;
            var y = contour[i].Y;
            pts[i] = new Point2f(
                (float)(a00 * x + a01 * y + a02 - ox),
                (float)(a10 * x + a11 * y + a12 - oy));
        }
        return pts;
    }

    private static void OrUprightContour(
        Mat edges, IReadOnlyList<Point2f>? contourInUpright, bool canonMapped, double invSx, double invSy)
    {
        if (contourInUpright is null || contourInUpright.Count < 4)
            return;
        var sx = canonMapped ? 1.0 / Math.Max(1e-9, invSx) : 1.0;
        var sy = canonMapped ? 1.0 / Math.Max(1e-9, invSy) : 1.0;
        var pts = new Point[contourInUpright.Count];
        for (var i = 0; i < contourInUpright.Count; i++)
        {
            pts[i] = new Point(
                (int)Math.Round(contourInUpright[i].X * sx),
                (int)Math.Round(contourInUpright[i].Y * sy));
        }
        Cv2.Polylines(edges, new[] { pts }, isClosed: true, color: Scalar.All(255), thickness: 1);
    }

    private readonly record struct ChamferScale(double Ax, double Ay, bool Directed, bool Active)
    {
        public static ChamferScale Identity => new(1, 1, false, false);
        public static ChamferScale ForMatch(int cropW, int cropH, ShapeModel model, double sceneWarpDeg, bool canonMapped)
        {
            var directed = Math.Abs(sceneWarpDeg) >= 25.0;
            if (canonMapped)
                return directed ? new(1, 1, true, true) : Identity;
            if (model.PartHalfW <= 1e-6 || model.PartHalfH <= 1e-6)
                return directed ? new(1, 1, true, true) : Identity;
            var (hw, hh) = PartHalfExtents(cropW, cropH);
            var ax = hw / model.PartHalfW;
            var ay = hh / model.PartHalfH;
            // 转正窗整数取整会让单边差 1–2px，不是各向同性尺度；误用 ay≠ax 会把模板拉歪。
            var isotropic = Math.Abs(ax - ay) <= 0.025;
            var scaled = isotropic && (Math.Abs(ax - 1.0) > 0.02 || Math.Abs(ay - 1.0) > 0.02);
            if (!directed && !scaled)
                return Identity;
            if (!isotropic)
                return directed ? new(1, 1, true, true) : Identity;
            return new(ax, ay, directed, true);
        }
    }

    private readonly record struct NccSeed(
        bool Found, double Score, double RotationDeg, Point2d Center, double RotRange)
    {
        public static NccSeed None(double rangeDeg) => new(false, 0, 0, default, rangeDeg);
    }

    private static NccSeed ProbeNccSeed(
        Mat upright, ShapeModel model, double rangeDeg, double? orientationBranchDeg)
    {
        if (model.NccGray.Empty())
            return NccSeed.None(rangeDeg);
        using var gray = ToGray(upright);
        Mat? scaled = null;
        var src = gray;
        var sx = 1.0;
        var sy = 1.0;
        var tw = model.NccGray.Width;
        var th = model.NccGray.Height;
        // MatchBest 在模板大于搜索图时跳过全部角；收缩件须升到示教尺寸再搜，中心映回原窗。
        if (upright.Width < tw || upright.Height < th)
        {
            scaled = new Mat();
            Cv2.Resize(gray, scaled, new Size(tw, th), 0, 0, InterpolationFlags.Cubic);
            src = scaled;
            sx = upright.Width / (double)tw;
            sy = upright.Height / (double)th;
        }
        try
        {
            var nccRange = Math.Clamp(Math.Min(rangeDeg, 5.0), 2.0, 8.0);
            var ncc = MaskTemplateMatcher.MatchBest(
                src, model.NccGray, nccRange, NccSeedMinScore,
                orientationBranchDeg: orientationBranchDeg);
            if (ncc is null)
                return NccSeed.None(rangeDeg);
            var center = new Point2d(ncc.CenterInUpright.X * sx, ncc.CenterInUpright.Y * sy);
            // 转正窗内残差角应≈0；NCC 若给出大残差（大 warp 插值/纹理误导）不可收窄 Chamfer 旋转窗。
            var rotBand = Math.Abs(ncc.RotationDeg) <= NccSeedBandDeg
                ? Math.Min(NccSeedBandDeg, rangeDeg)
                : rangeDeg;
            return new NccSeed(true, ncc.Score, ncc.RotationDeg, center, rotBand);
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    /// <summary>转正窗 NCC 角种子可信：残差落在窄带内才用于收窄 Chamfer / 平移锚。</summary>
    private static bool NccAngleTrustworthy(NccSeed ncc) =>
        ncc.Found && Math.Abs(ncc.RotationDeg) <= NccSeedBandDeg;

    /// <summary>NCC 在转正窗内报告 ~180°：近对称/大 warp 下常见假峰，不可作平移锚或金字塔种子。</summary>
    private static bool NccFlipAmbiguous(NccSeed ncc) =>
        ncc.Found && Math.Abs(Math.Abs(ncc.RotationDeg) - 180.0) < 15.0; 

    private static MatchHit? NccTrustedHit(ChamferField field, ShapeModel model, NccSeed ncc, double sceneWarpDeg)
    {
        var liveCenter = NccCenterToModelCenter(model, ncc.RotationDeg, ncc.Center);
        var fieldCenter = MapLiveToTeach(liveCenter, field.CanonMapped, field.TeachSx, field.TeachSy);
        var chamfer = ChamferHitAtPose(field, model, ncc.RotationDeg, fieldCenter, sceneWarpDeg);
        var mean = chamfer?.MeanDistPx ?? ActiveMaxMeanDist;
        var hit = chamfer?.HitRate ?? 0;
        if (hit < ActiveMinHitRate || mean > ActiveMaxMeanDist)
        {
            hit = Math.Max(hit, Math.Clamp(ncc.Score * 1.35, ActiveMinHitRate, 1.0));
            mean = Math.Min(mean, ActiveMaxMeanDist);
        }
        return new MatchHit(
            ncc.RotationDeg, chamfer?.CenterInUpright ?? liveCenter, mean, hit,
            chamfer?.Viz ?? ShapeViz.Empty, chamfer?.DirAgree ?? double.NaN);
    }

    /// <summary>NCC 峰在模板图心；Chamfer 边点相对示教边缘质心 — 须按残差角旋转偏移。</summary>
    private static Point2d NccCenterToModelCenter(ShapeModel model, double rotDegInUpright, Point2d templateCenter)
    {
        var dx = model.CenterX - model.NccGray.Width / 2.0;
        var dy = model.CenterY - model.NccGray.Height / 2.0;
        var rad = rotDegInUpright * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        return new Point2d(
            templateCenter.X + dx * cos - dy * sin,
            templateCenter.Y + dx * sin + dy * cos);
    }

    private static MatchHit? ChamferHitAtPose(
        ChamferField field, ShapeModel model, double deg, Point2d center, double sceneWarpDeg)
    {
        _ = sceneWarpDeg;
        var span = Math.Max(6, Math.Min(12, Math.Max(field.W, field.H) * 0.05));
        // NCC 已给定位姿：无向小窗即可量 Chamfer 质量，避免有向法向在大网格上爆炸耗时。
        var refined = Search(field, model, 0, deg, center.X, center.Y, span, transStep: 2, rotStep: 1.0, coarseFast: true);
        if (refined is not { } c)
            return null;
        var micro = RefineSubpixel(field, model, c, fast: true);
        var viz = ActiveOptions.EnableVisualization
            ? BuildViz(field.Dt, model.Points, micro.Deg, micro.Mx, micro.My)
            : ShapeViz.Empty;
        var centerLive = MapTeachToLive(new Point2d(micro.Mx, micro.My), field.CanonMapped, field.InvSx, field.InvSy);
        return new MatchHit(micro.Deg, centerLive, micro.Mean, micro.Hit, viz, micro.DirAgree);
    }

    /// <summary>大角度 warp 插值使边缘变厚，对 Canny 做 1px 十字膨胀以匹配 Halcon 亚像素边带。</summary>
    private static Mat DilateEdgesForWarp(Mat edges, double sceneWarpDeg)
    {
        var result = new Mat();
        if (Math.Abs(sceneWarpDeg) < EdgeDilateWarpDeg)
        {
            edges.CopyTo(result);
            return result;
        }
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(3, 3));
        Cv2.Dilate(edges, result, kernel);
        return result;
    }

    /// <summary>大角度 warp 下合并宽阈值 Canny，补偿转正插值导致的边带漂移。</summary>
    private static Mat BuildChamferEdges(Mat gray, double sceneWarpDeg)
    {
        var minC = ActiveOptions.MinContrast;
        Mat edges;
        if (minC > 0)
        {
            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);
            var (low, high) = MaskTemplateMatcher.AdaptiveCannyThresholds(blurred);
            low = Math.Max(low, minC);
            high = Math.Max(high, minC * 2.0);
            edges = new Mat();
            Cv2.Canny(blurred, edges, low, high);
        }
        else
        {
            edges = MaskTemplateMatcher.ToCanny8u(gray);
        }
        if (Math.Abs(sceneWarpDeg) < EdgeDilateWarpDeg)
            return edges;
        using var blurred2 = new Mat();
        Cv2.GaussianBlur(gray, blurred2, new Size(3, 3), 0);
        var (low2, high2) = MaskTemplateMatcher.AdaptiveCannyThresholds(blurred2);
        using var loose = new Mat();
        Cv2.Canny(blurred2, loose, low2 * 0.5, high2 * 0.5);
        var combined = new Mat();
        Cv2.BitwiseOr(edges, loose, combined);
        edges.Dispose();
        if (Math.Abs(sceneWarpDeg) >= CanonChamferWarpDeg)
            OrContrastGradientEdges(gray, combined);
        return combined;
    }

    /// <summary>梯度对比度脊并入边缘图，减轻 warp 插值导致 Canny 与示教边点系统性错位。</summary>
    private static void OrContrastGradientEdges(Mat gray, Mat edges8u)
    {
        using var gradMag = BuildGradientMagnitude(gray, out var peak);
        if (peak <= 1e-6f)
            return;
        using var gradTh = new Mat();
        Cv2.Threshold(gradMag, gradTh, peak * GradEdgePeakRatio, 255, ThresholdTypes.Binary);
        using var grad8 = new Mat();
        gradTh.ConvertTo(grad8, MatType.CV_8UC1);
        Cv2.BitwiseOr(edges8u, grad8, edges8u);
    }

    private static PoseCand ScorePose(
        ChamferField field, ShapeModel model, double deg, double mx, double my, bool coarseFast = false)
    {
        var rad = deg * Math.PI / 180.0;
        var scored = Score(field, model, Math.Cos(rad), Math.Sin(rad), mx, my, coarseFast);
        return new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
    }

    private static PoseCand ScorePose(
        MatIndexer<float> dt, MatIndexer<byte> dirMap,
        int w, int h, ShapeModel model, double deg, double mx, double my,
        MatIndexer<float>? gradIdx = null, float gradPeak = 0f, ChamferScale scale = default)
    {
        var rad = deg * Math.PI / 180.0;
        var scored = Score(dt, dirMap, w, h, model, Math.Cos(rad), Math.Sin(rad), mx, my, gradIdx, gradPeak, scale);
        return new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
    }

    private static PoseCand? SearchPyramid(
        Mat gray, ShapeModel model, double sceneWarpDeg, double rotRange, double bandDeg,
        double span, ChamferScale scale, int levels)
    {
        using var grayHalf = DownscaleHalf(gray);
        using var grayQuarter = levels >= 3 ? DownscaleHalf(grayHalf) : null;
        PoseCand? seed = null;
        if (grayQuarter is not null)
        {
            seed = SearchOnGray(grayQuarter, model, sceneWarpDeg, rotRange, bandDeg,
                grayQuarter.Width / 2.0, grayQuarter.Height / 2.0,
                Math.Max(4, span * 0.25), transStep: 3, rotStep: 1.5, scale);
            if (seed is { } q)
                seed = q with { Mx = q.Mx * 2.0, My = q.My * 2.0 };
        }
        var halfSpan = seed is not null
            ? Math.Max(4, span * 0.30)
            : Math.Max(5, span * 0.5);
        var cxH = seed?.Mx ?? grayHalf.Width / 2.0;
        var cyH = seed?.My ?? grayHalf.Height / 2.0;
        var halfRange = seed is null ? rotRange : Math.Min(3, rotRange);
        var half = SearchOnGray(grayHalf, model, sceneWarpDeg, halfRange, seed?.Deg ?? bandDeg,
            cxH, cyH, halfSpan, transStep: seed is null ? 4 : 3, rotStep: seed is null ? 1.5 : 1.0, scale);
        if (half is { } h)
            return h with { Mx = h.Mx * 2.0, My = h.My * 2.0 };
        return seed is { } leftover
            ? leftover with { Mx = leftover.Mx * 2.0, My = leftover.My * 2.0 }
            : null;
    }

    private static PoseCand? SearchOnGray(
        Mat gray, ShapeModel model, double sceneWarpDeg, double rotRange, double bandDeg,
        double cx, double cy, double span, int transStep, double rotStep, ChamferScale scale)
    {
        using var edges = BuildChamferEdges(gray, sceneWarpDeg);
        using var edgesDt = DilateEdgesForWarp(edges, sceneWarpDeg);
        using var inv = new Mat();
        Cv2.BitwiseNot(edgesDt, inv);
        using var dt = new Mat();
        Cv2.DistanceTransform(inv, dt, DistanceTypes.L2, DistanceTransformMasks.Mask3);
        using var dir = BuildDirMap(gray);
        return Search(dt, dir, model, rotRange, bandDeg, cx, cy, span, transStep, rotStep, null, 0f, scale);
    }

    private static Mat DownscaleHalf(Mat src)
    {
        var w = Math.Max(1, src.Width / 2);
        var h = Math.Max(1, src.Height / 2);
        var dst = new Mat();
        Cv2.Resize(src, dst, new Size(w, h), 0, 0, InterpolationFlags.Area);
        return dst;
    }

    private readonly record struct PoseCand(double Deg, double Mx, double My, double Mean, double Hit, double Cost, double DirAgree);

    private static PoseCand? Search(
        Mat dt, Mat dirMap, ShapeModel model, double rangeDeg, double bandDeg,
        double cx, double cy, double span, int transStep, double rotStep,
        MatIndexer<float>? gradIdx = null, float gradPeak = 0f, ChamferScale scale = default)
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
                var scored = Score(indexer, dirIdx, w, h, model, cos, sin, mx, my, gradIdx, gradPeak, scale);
                if (best is null || scored.Cost < best.Value.Cost - 1e-9)
                    best = new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
            }
        }
        return best;
    }

    private static PoseCand? Search(
        ChamferField field, ShapeModel model, double rangeDeg, double bandDeg,
        double cx, double cy, double span, int transStep, double rotStep,
        bool coarseFast = false, int[]? evalSlot = null, int evalIndex = 0)
    {
        PoseCand? best = null;
        var lo = bandDeg - rangeDeg;
        var hi = bandDeg + rangeDeg;
        var evals = 0;
        for (var deg = lo; deg <= hi + 1e-6; deg += rotStep)
        {
            var rad = deg * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            for (var dy = -span; dy <= span + 1e-6; dy += transStep)
            for (var dx = -span; dx <= span + 1e-6; dx += transStep)
            {
                evals++;
                var mx = cx + dx;
                var my = cy + dy;
                var scored = Score(field, model, cos, sin, mx, my, coarseFast);
                if (best is null || scored.Cost < best.Value.Cost - 1e-9)
                    best = new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
            }
        }
        if (evalSlot is not null && evalIndex >= 0 && evalIndex < evalSlot.Length)
            evalSlot[evalIndex] += evals;
        return best;
    }

    private static PoseCand RefineSubpixel(ChamferField field, ShapeModel model, PoseCand seed, bool fast = false)
    {
        var best = seed;
        var passes = fast ? 1 : seed.Cost < 2.5 ? 1 : 2;
        for (var pass = 0; pass < passes; pass++)
        {
            var improved = false;
            foreach (var dDeg in new[] { -0.25, 0, 0.25 })
            {
                var deg = best.Deg + dDeg;
                var rad = deg * Math.PI / 180.0;
                var cos = Math.Cos(rad);
                var sin = Math.Sin(rad);
                for (var dy = -0.5; dy <= 0.5 + 1e-9; dy += 0.5)
                for (var dx = -0.5; dx <= 0.5 + 1e-9; dx += 0.5)
                {
                    var mx = best.Mx + dx;
                    var my = best.My + dy;
                    var scored = Score(field, model, cos, sin, mx, my, coarseFast: fast);
                    if (scored.Cost < best.Cost - 1e-9)
                    {
                        best = new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
                        improved = true;
                    }
                }
            }
            if (!improved)
                break;
        }
        if (fast && best.Cost > 3.5)
            return RefineSubpixel(field, model, best, fast: false);
        return best with { Deg = AngleGeometry.NormalizeSignedDeg(best.Deg) };
    }

    /// <summary>平移 0.1px 网格 + 抛物线插值，把模型原点收到 &lt;0.1px。</summary>
    private static PoseCand RefineCenterFine(ChamferField field, ShapeModel model, PoseCand seed)
    {
        var best = seed;
        var rad = seed.Deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        const double step = 0.1;
        for (var dy = -0.4; dy <= 0.4 + 1e-9; dy += step)
        for (var dx = -0.4; dx <= 0.4 + 1e-9; dx += step)
        {
            if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)
                continue;
            var scored = Score(field, model, cos, sin, seed.Mx + dx, seed.My + dy, coarseFast: true);
            if (scored.Cost < best.Cost - 1e-12)
                best = new PoseCand(seed.Deg, seed.Mx + dx, seed.My + dy, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
        }
        var cxm = Score(field, model, cos, sin, best.Mx - step, best.My, coarseFast: true).Cost;
        var cxp = Score(field, model, cos, sin, best.Mx + step, best.My, coarseFast: true).Cost;
        var cym = Score(field, model, cos, sin, best.Mx, best.My - step, coarseFast: true).Cost;
        var cyp = Score(field, model, cos, sin, best.Mx, best.My + step, coarseFast: true).Cost;
        var ox = ParabolaOffset(cxm, best.Cost, cxp, step);
        var oy = ParabolaOffset(cym, best.Cost, cyp, step);
        var mx = best.Mx + ox;
        var my = best.My + oy;
        var fine = Score(field, model, cos, sin, mx, my, coarseFast: true);
        if (fine.Cost <= best.Cost + 1e-12)
            return new PoseCand(best.Deg, mx, my, fine.Mean, fine.Hit, fine.Cost, fine.DirAgree);
        return best;
    }

    private static double ParabolaOffset(double left, double mid, double right, double step)
    {
        var denom = left - 2.0 * mid + right;
        if (Math.Abs(denom) < 1e-9)
            return 0;
        var t = 0.5 * (left - right) / denom * step;
        return Math.Clamp(t, -step, step);
    }

    /// <summary>命中偏低时探测 ±4% 各向同性尺度（轻微缩放/变形）。</summary>
    private static PoseCand RefineScale(ChamferField field, ShapeModel model, PoseCand seed)
    {
        var best = seed;
        var rad = seed.Deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        foreach (var s in new[] { 0.95, 0.96, 0.97, 0.98, 0.99, 1.01, 1.02, 1.03, 1.04, 1.05 })
        {
            var sc = new ChamferScale(
                (field.Scale.Active ? field.Scale.Ax : 1.0) * s,
                (field.Scale.Active ? field.Scale.Ay : 1.0) * s,
                field.Scale.Directed, true);
            var scored = Score(field, model, cos, sin, seed.Mx, seed.My, coarseFast: true, sc);
            var betterCost = scored.Cost < best.Cost - 1e-9;
            var betterHit = scored.Hit > best.Hit + 1e-6 && (seed.Hit < ActiveMinHitRate || scored.Cost <= best.Cost * 1.12);
            if (betterCost || betterHit)
                best = new PoseCand(seed.Deg, seed.Mx, seed.My, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
        }
        return best;
    }

    private static PoseCand RefineSubpixel(
        Mat dt, Mat dirMap, ShapeModel model, PoseCand seed,
        MatIndexer<float>? gradIdx = null, float gradPeak = 0f, ChamferScale scale = default)
    {
        var best = seed;
        var indexer = dt.GetGenericIndexer<float>();
        var dirIdx = dirMap.GetGenericIndexer<byte>();
        var w = dt.Cols;
        var h = dt.Rows;
        for (var pass = 0; pass < (seed.Cost < 2.5 ? 1 : 2); pass++)
        {
            var improved = false;
            foreach (var dDeg in new[] { -0.25, 0, 0.25 })
            {
                var deg = best.Deg + dDeg;
                var rad = deg * Math.PI / 180.0;
                var cos = Math.Cos(rad);
                var sin = Math.Sin(rad);
                for (var dy = -0.5; dy <= 0.5 + 1e-9; dy += 0.5)
                for (var dx = -0.5; dx <= 0.5 + 1e-9; dx += 0.5)
                {
                    var mx = best.Mx + dx;
                    var my = best.My + dy;
                    var scored = Score(indexer, dirIdx, w, h, model, cos, sin, mx, my, gradIdx, gradPeak, scale);
                    if (scored.Cost < best.Cost - 1e-9)
                    {
                        best = new PoseCand(deg, mx, my, scored.Mean, scored.Hit, scored.Cost, scored.DirAgree);
                        improved = true;
                    }
                }
            }
            if (!improved)
                break;
        }
        return best with { Deg = AngleGeometry.NormalizeSignedDeg(best.Deg) };
    }

    private readonly record struct Scored(double Mean, double Hit, double Cost, double DirAgree);

    private static Scored Score(
        ChamferField field, ShapeModel model, double cos, double sin, double mx, double my, bool coarseFast,
        ChamferScale? scaleOverride = null)
    {
        if (!coarseFast && field.Scale.Directed)
            field.EnsureGrad();
        var scale = scaleOverride ?? field.Scale;
        var directed = scale.Directed && !coarseFast;
        var gradIdx = directed ? field.GradIdx : null;
        var gradPeak = directed ? field.GradPeak : 0f;
        var ax = scale.Active ? scale.Ax : 1.0;
        var ay = scale.Active ? scale.Ay : 1.0;
        var pts = model.Points;
        var weights = model.Weights;
        var dirBins = model.DirBins;
        var n = pts.Length;
        var useDir = ActiveOptions.UseDirectionCheck;
        var dirMismatch = ActiveOptions.DirMismatchPx;
        Span<double> projX = stackalloc double[n];
        Span<double> projY = stackalloc double[n];
        MaskShapeMatchScoreSimd.ProjectModelPoints(pts, ax, ay, cos, sin, mx, my, projX, projY);
        if (!directed && (coarseFast || !useDir))
        {
            var sum = 0.0;
            var wsum = 0.0;
            var hit = 0;
            MaskShapeMatchScoreSimd.AccumulateUndirectedDt(
                field.DtFlat, field.W, field.H, projX, projY, weights,
                HitDistPx, OobPenalty, ref sum, ref wsum, ref hit);
            var mean = sum / Math.Max(1e-6, wsum);
            var hitRate = hit / (double)n;
            return new Scored(mean, hitRate, mean + 6.0 * (1.0 - hitRate), double.NaN);
        }
        var sum2 = 0.0;
        var wsum2 = 0.0;
        var hit2 = 0;
        var dirOkCount = 0;
        var dirChecked = 0;
        var rotBinOffset = (int)Math.Round(Atan2SignedDeg(cos, sin) / DirBinWidthDeg) % DirBins;
        var dt = field.DtFlat;
        var dirFlat = field.DirFlat;
        var w = field.W;
        var h = field.H;
        for (var i = 0; i < n; i++)
        {
            var x = projX[i];
            var y = projY[i];
            var wt = weights[i];
            wsum2 += wt;
            if (x < 1 || y < 1 || x >= w - 2 || y >= h - 2)
            {
                sum2 += OobPenalty * wt;
                continue;
            }
            var modelBin = dirBins[i];
            var degBins = modelBin < DirBins ? rotBinOffset : 0;
            var d = PointDistance(dt, gradIdx, gradPeak, w, h, x, y, modelBin, degBins, cos, sin, directed);
            var dirOk = true;
            if (useDir && modelBin < DirBins)
            {
                var sceneBin = SampleDirFlat(dirFlat, w, x, y);
                if (sceneBin < DirBins)
                {
                    dirChecked++;
                    var expect = (modelBin + degBins + DirBins * 4) % DirBins;
                    var rawDiff = (sceneBin - expect + DirBins * 4) % DirBins;
                    var diff = Math.Min(rawDiff, DirBins - rawDiff);
                    if (diff <= DirTolBins)
                        dirOkCount++;
                    else
                        dirOk = false;
                }
            }
            sum2 += (dirOk ? d : d + dirMismatch) * wt;
            if (dirOk && d <= HitDistPx)
                hit2++;
        }
        var mean2 = sum2 / Math.Max(1e-6, wsum2);
        var hitRate2 = hit2 / (double)n;
        var dirAgree = dirChecked == 0 ? double.NaN : dirOkCount / (double)dirChecked;
        var dirPenalty = dirChecked == 0 ? 0.0 : 1.0 - dirOkCount / (double)dirChecked;
        return new Scored(mean2, hitRate2, mean2 + 6.0 * (1.0 - hitRate2) + 2.5 * dirPenalty, dirAgree);
    }

    private static Scored Score(
        MatIndexer<float> dt, MatIndexer<byte> dirMap,
        int w, int h, ShapeModel model, double cos, double sin, double mx, double my,
        MatIndexer<float>? gradIdx = null, float gradPeak = 0f, ChamferScale scale = default)
    {
        var ax = scale.Active ? scale.Ax : 1.0;
        var ay = scale.Active ? scale.Ay : 1.0;
        var pts = model.Points;
        var weights = model.Weights;
        var dirBins = model.DirBins;
        var sum = 0.0;
        var wsum = 0.0;
        var hit = 0;
        var n = pts.Length;
        var dirOkCount = 0;
        var dirChecked = 0;
        var rotBinOffset = (int)Math.Round(Atan2SignedDeg(cos, sin) / DirBinWidthDeg) % DirBins;
        var useDir = ActiveOptions.UseDirectionCheck;
        var dirMismatch = ActiveOptions.DirMismatchPx;
        Span<double> projX = stackalloc double[n];
        Span<double> projY = stackalloc double[n];
        MaskShapeMatchScoreSimd.ProjectModelPoints(pts, ax, ay, cos, sin, mx, my, projX, projY);
        for (var i = 0; i < n; i++)
        {
            var x = projX[i];
            var y = projY[i];
            var wt = weights[i];
            wsum += wt;
            if (x < 1 || y < 1 || x >= w - 2 || y >= h - 2)
            {
                sum += OobPenalty * wt;
                continue;
            }
            var modelBin = dirBins[i];
            var degBins = modelBin < DirBins ? rotBinOffset : 0;
            var d = PointDistance(dt, gradIdx, gradPeak, w, h, x, y, modelBin, degBins, cos, sin, scale.Directed);
            // 有向 Chamfer：模型点方向 bin + 旋转偏移 = 期望方向，与现场方向图比（折叠 180° 无向）。
            // 方向失配只影响 hit（不放大距离代价）：正确位姿距离近 → mean 低仍占优；
            // 方向项用于压制"距离近但方向错"的平行干扰边（其 hit 少 → cost 升高）。
            var dirOk = true;
            var sceneBin = SampleDirNearest(dirMap, w, h, x, y);
            if (useDir && modelBin < DirBins && sceneBin < DirBins)
            {
                dirChecked++;
                var expect = (modelBin + degBins + DirBins * 4) % DirBins;
                var rawDiff = (sceneBin - expect + DirBins * 4) % DirBins;
                var diff = Math.Min(rawDiff, DirBins - rawDiff); // 环回最小差
                if (diff <= DirTolBins)
                    dirOkCount++;
                else
                    dirOk = false;
            }
            sum += (dirOk ? d : d + dirMismatch) * wt;
            if (dirOk && d <= HitDistPx)
                hit++;
        }
        var mean = sum / Math.Max(1e-6, wsum);
        var hitRate = hit / (double)n;
        var dirAgree = dirChecked == 0 ? double.NaN : dirOkCount / (double)dirChecked;
        var dirPenalty = dirChecked == 0 ? 0.0 : 1.0 - dirOkCount / (double)dirChecked;
        return new Scored(mean, hitRate, mean + 6.0 * (1.0 - hitRate) + 2.5 * dirPenalty, dirAgree);
    }

    /// <summary>沿模型边法向（梯度方向）搜索最小 DT，大 warp 下避免 2D 距离场拾取平行边。</summary>
    private static double PointDistance(
        ReadOnlySpan<float> dt, MatIndexer<float>? gradIdx, float gradPeak,
        int w, int h, double x, double y, int modelBin, int degBins, double cos, double sin, bool directed)
    {
        if (!directed || modelBin >= DirBins)
            return BlendDistance(SampleBilinear(dt, w, x, y), gradIdx, gradPeak, w, h, x, y);
        var gradBin = (modelBin + degBins + DirBins * 4) % DirBins;
        var gradDeg = (gradBin + 0.5) * DirBinWidthDeg * Math.PI / 180.0;
        var gx = Math.Cos(gradDeg);
        var gy = Math.Sin(gradDeg);
        var minD = double.MaxValue;
        var bestX = x;
        var bestY = y;
        for (var t = -DirectedSearchHalfPx; t <= DirectedSearchHalfPx + 1e-9; t += DirectedStepPx)
        {
            var sx = x + t * gx;
            var sy = y + t * gy;
            if (sx < 1 || sy < 1 || sx >= w - 2 || sy >= h - 2)
                continue;
            var d = SampleBilinear(dt, w, sx, sy);
            if (d < minD)
            {
                minD = d;
                bestX = sx;
                bestY = sy;
            }
        }
        return minD >= double.MaxValue - 1
            ? BlendDistance(SampleBilinear(dt, w, x, y), gradIdx, gradPeak, w, h, x, y)
            : BlendDistance(minD, gradIdx, gradPeak, w, h, bestX, bestY);
    }

    private static double PointDistance(
        MatIndexer<float> dt, MatIndexer<float>? gradIdx, float gradPeak,
        int w, int h, double x, double y, int modelBin, int degBins, double cos, double sin, bool directed)
    {
        if (!directed || modelBin >= DirBins)
            return BlendDistance(SampleBilinear(dt, x, y), gradIdx, gradPeak, w, h, x, y);
        var gradBin = (modelBin + degBins + DirBins * 4) % DirBins;
        var gradDeg = (gradBin + 0.5) * DirBinWidthDeg * Math.PI / 180.0;
        var gx = Math.Cos(gradDeg);
        var gy = Math.Sin(gradDeg);
        var minD = double.MaxValue;
        var bestX = x;
        var bestY = y;
        for (var t = -DirectedSearchHalfPx; t <= DirectedSearchHalfPx + 1e-9; t += DirectedStepPx)
        {
            var sx = x + t * gx;
            var sy = y + t * gy;
            if (sx < 1 || sy < 1 || sx >= w - 2 || sy >= h - 2)
                continue;
            var d = SampleBilinear(dt, sx, sy);
            if (d < minD)
            {
                minD = d;
                bestX = sx;
                bestY = sy;
            }
        }
        return minD >= double.MaxValue - 1
            ? BlendDistance(SampleBilinear(dt, x, y), gradIdx, gradPeak, w, h, x, y)
            : BlendDistance(minD, gradIdx, gradPeak, w, h, bestX, bestY);
    }

    private static byte SampleDirFlat(ReadOnlySpan<byte> dirFlat, int w, double x, double y)
    {
        var x0 = (int)Math.Round(Math.Clamp(x, 0, w - 1));
        var y0 = (int)Math.Round(Math.Clamp(y, 0, dirFlat.Length / Math.Max(1, w) - 1));
        return dirFlat[y0 * w + x0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SampleBilinear(ReadOnlySpan<float> dt, int w, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var dx = (float)(x - x0);
        var dy = (float)(y - y0);
        var row = y0 * w;
        var i00 = row + x0;
        var v00 = dt[i00];
        var v10 = dt[i00 + 1];
        var v01 = dt[i00 + w];
        var v11 = dt[i00 + w + 1];
        return (1 - dx) * (1 - dy) * v00 + dx * (1 - dy) * v10 + (1 - dx) * dy * v01 + dx * dy * v11;
    }

    private static byte SampleDirNearest(MatIndexer<byte> dirMap, int w, int h, double x, double y)
    {
        var x0 = (int)Math.Round(Math.Clamp(x, 0, w - 1));
        var y0 = (int)Math.Round(Math.Clamp(y, 0, h - 1));
        return dirMap[y0, x0];
    }

    /// <summary>从旋转矩阵余弦/正弦恢复有符号旋转角（度）。</summary>
    private static double Atan2SignedDeg(double cos, double sin) =>
        Math.Atan2(sin, cos) * 180.0 / Math.PI;

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
}
