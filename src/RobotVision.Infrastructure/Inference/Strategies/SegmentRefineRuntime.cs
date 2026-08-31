using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>一次分割实例的精修输入（ROI 图 + 轮廓 + 可选示教缓存）。</summary>
public readonly struct SegmentRefineRequest
{
    public required Mat RoiView { get; init; }
    public required Point2f[] Points { get; init; }
    public required RecipeConfig Recipe { get; init; }
    public required double SegmentConfidence { get; init; }
    public byte[]? BitPackedMask { get; init; }
    public int MaskWidth { get; init; }
    public int MaskHeight { get; init; }
    public MaskTemplateRotationPack? Pack { get; init; }
    public Mat? Template { get; init; }
    public MaskSiftRefine.TeachModel? SiftTeach { get; init; }
    public MaskShapeMatch.ShapeModel? ShapeModel { get; init; }
    public double BoxLeft { get; init; }
    public double BoxTop { get; init; }
}

/// <summary>精修输出：位姿 + 叠加（箭头/卡尺/匹配窗）。由 <see cref="MaskTemplateStrategy"/> 再加分割轮廓。</summary>
public readonly struct SegmentRefineHit
{
    public required PixelPose Pose { get; init; }
    public PixelPoint[]? TabMarker { get; init; }
    public OverlayLine[]? DebugLines { get; init; }
    public OverlayDot[]? DebugDots { get; init; }
    public PixelPoint[]? MatchWindow { get; init; }
    public string? QualityNote { get; init; }
}

/// <summary>
/// 分割精修运行时：一种 <see cref="SegmentRefineMethod"/> 对应一个实现。
/// 新增方法 = 实现本接口并 <c>SegmentRefineRuntimeRegistry.Default.Register</c>，
/// 不再改 <see cref="MaskTemplateStrategy.Compute"/> 主循环。
/// </summary>
public interface ISegmentRefineRuntime
{
    SegmentRefineMethod Method { get; }

    SegmentRefineHit Refine(in SegmentRefineRequest request);
}

/// <summary>精修方法注册表：与 <see cref="AngleStrategyTypeRegistry"/> 同构。</summary>
public sealed class SegmentRefineRuntimeRegistry
{
    public static SegmentRefineRuntimeRegistry Default { get; } = CreateDefault();

    private readonly Dictionary<SegmentRefineMethod, ISegmentRefineRuntime> _runtimes = [];

    public static SegmentRefineRuntimeRegistry CreateDefault()
    {
        var registry = new SegmentRefineRuntimeRegistry();
        registry.Register(new TemplateSegmentRefineRuntime());
        registry.Register(new SiftSegmentRefineRuntime());
        registry.Register(new ShapeMatchSegmentRefineRuntime());
        registry.Register(new CaliperTabSegmentRefineRuntime());
        registry.Register(new CentroidHoleSegmentRefineRuntime());
        registry.Register(new LineFitSegmentRefineRuntime());
        return registry;
    }

    public void Register(ISegmentRefineRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtimes[runtime.Method] = runtime;
    }

    public bool IsKnown(SegmentRefineMethod method) => _runtimes.ContainsKey(method);

    public IReadOnlyList<SegmentRefineMethod> Methods =>
        _runtimes.Keys.OrderBy(m => m).ToArray();

    /// <summary>未注册方法回退直线拟合（与旧 switch 的 else 分支一致）。</summary>
    public ISegmentRefineRuntime Get(SegmentRefineMethod method) =>
        _runtimes.TryGetValue(method, out var runtime)
            ? runtime
            : _runtimes[SegmentRefineMethod.LineFit];
}

internal static class SegmentRefineOps
{
    public const double CropMarginRatio = 0.15;

    public static PixelPose Fallback(Point2f[] contourPoints, double segConfidence, RecipeConfig recipe, double score)
    {
        var housing = MaskHousing.Fit(contourPoints);
        return new PixelPose(housing.Center.X, housing.Center.Y, housing.LongAxisDeg, score)
        {
            SegmentScore = segConfidence,
            Usable = recipe.Template.AllowCoarseFallback,
        };
    }

    public static PixelPose Ok(double cx, double cy, double angleDeg, double refineScore, double segConfidence) =>
        new(cx, cy, angleDeg, refineScore)
        {
            SegmentScore = segConfidence,
            Usable = true,
        };

    public static PixelPoint[] HeadingMarker(Point2d center, double angleDeg, Point2f[] contour)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var housing = MaskHousing.Fit(contour);
        var markerLen = Math.Max(24, housing.LongLen / 2.0);
        return
        [
            new PixelPoint(center.X, center.Y),
            new PixelPoint(
                center.X + Math.Cos(rad) * markerLen,
                center.Y + Math.Sin(rad) * markerLen),
        ];
    }

    public static OverlayDot[]? MapMatchDots(
        IReadOnlyList<Point2d> inliers, IReadOnlyList<Point2d> rejected)
    {
        var n = inliers.Count + rejected.Count;
        if (n == 0)
            return null;

        var buf = new OverlayDot[n];
        var d = 0;
        foreach (var p in inliers)
            buf[d++] = new OverlayDot(new PixelPoint(p.X, p.Y), OverlayDotKind.Inlier);
        foreach (var p in rejected)
            buf[d++] = new OverlayDot(new PixelPoint(p.X, p.Y), OverlayDotKind.Rejected);
        return buf;
    }

    public static void MapCaliperDebug(
        MaskCaliperTab.CaliperViz viz,
        out OverlayLine[]? lines, out OverlayDot[]? dots)
    {
        PixelPoint Map(Point2d p) => new(p.X, p.Y);
        OverlayLine Line(MaskCaliperTab.Segment s, OverlayLineKind kind) =>
            new(Map(s.A), Map(s.B), kind);

        var nLines = viz.SearchBars.Count + viz.InvalidBars.Count
            + (viz.FittedMinus is null ? 0 : 1) + (viz.FittedPlus is null ? 0 : 1);
        var nDots = viz.Inliers.Count + viz.Rejected.Count;
        if (nLines == 0 && nDots == 0)
        {
            lines = null;
            dots = null;
            return;
        }

        var lineBuf = nLines == 0 ? Array.Empty<OverlayLine>() : new OverlayLine[nLines];
        var i = 0;
        foreach (var bar in viz.SearchBars)
            lineBuf[i++] = Line(bar, OverlayLineKind.Caliper);
        foreach (var bar in viz.InvalidBars)
            lineBuf[i++] = Line(bar, OverlayLineKind.InvalidCaliper);
        if (viz.FittedMinus is { } fm)
            lineBuf[i++] = Line(fm, OverlayLineKind.FittedEdge);
        if (viz.FittedPlus is { } fp)
            lineBuf[i++] = Line(fp, OverlayLineKind.FittedEdge);
        lines = nLines == 0 ? null : lineBuf;

        if (nDots == 0)
        {
            dots = null;
            return;
        }

        var dotBuf = new OverlayDot[nDots];
        var d = 0;
        foreach (var p in viz.Inliers)
            dotBuf[d++] = new OverlayDot(Map(p), OverlayDotKind.Inlier);
        foreach (var p in viz.Rejected)
            dotBuf[d++] = new OverlayDot(Map(p), OverlayDotKind.Rejected);
        dots = dotBuf;
    }
}

internal sealed class TemplateSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.Template;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        var template = request.Template;
        if (template is null)
        {
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe, 0),
            };
        }

        var refined = RefineByTemplate(
            request.RoiView, request.Points, template, request.Recipe,
            request.SegmentConfidence, request.Pack);
        return new SegmentRefineHit
        {
            Pose = refined.Pose,
            TabMarker = refined.TabMarker,
            MatchWindow = refined.MatchWindow,
        };
    }

    private readonly record struct TemplateRefine(
        PixelPose Pose, PixelPoint[]? TabMarker, PixelPoint[]? MatchWindow = null);

    private static TemplateRefine RefineByTemplate(
        Mat roiView, Point2f[] contour, Mat template, RecipeConfig recipe, double segConfidence,
        MaskTemplateRotationPack? pack)
    {
        var housing = MaskHousing.Fit(contour);
        var range = MaskHousing.AdaptiveRefineRange(recipe.Template.RefineRangeDeg, housing);
        var useUpright = recipe.Template.UseUprightCrop;

        UprightCropResult crop;
        try
        {
            crop = useUpright
                ? MaskTemplateMatcher.UprightCrop(roiView, contour, SegmentRefineOps.CropMarginRatio)
                : MaskTemplateMatcher.AxisAlignedCrop(roiView, contour, SegmentRefineOps.CropMarginRatio);
        }
        catch (InvalidOperationException)
        {
            return new TemplateRefine(SegmentRefineOps.Fallback(contour, segConfidence, recipe, 0), null);
        }

        try
        {
            var origin = useUpright ? 0.0 : housing.WarpAngleDeg;
            var bank = useUpright ? pack : null;
            var match = MatchOnUpright(crop.Upright, template, recipe, bank, range, origin);
            if (useUpright && match is not null && MaskTemplateMatcher.NeedsUprightAlign(match))
            {
                UprightCropResult? flipped = null;
                try
                {
                    flipped = MaskTemplateMatcher.UprightCrop(
                        roiView, contour, SegmentRefineOps.CropMarginRatio, extraWarpDeg: 180);
                    var rematch = MatchOnUpright(
                        flipped.Upright, template, recipe, bank, range, origin, forceZeroBranch: true);
                    if (rematch is not null && !MaskTemplateMatcher.IsOrientationFlip(rematch.RotationDeg))
                    {
                        crop.Upright.Dispose();
                        crop = flipped;
                        flipped = null;
                        match = rematch;
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

            if (match is null)
                return new TemplateRefine(SegmentRefineOps.Fallback(contour, segConfidence, recipe, 0), null);

            var tplSigned = AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + match.RotationDeg);
            var angled = AngleGeometry.FuseDirected(housing.LongAxisDeg, tplSigned);
            var center = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);
            var window = PoseOverlay.TemplateMatchWindow(
                center.X, center.Y, angled, template.Width, template.Height);
            return new TemplateRefine(
                SegmentRefineOps.Ok(center.X, center.Y, angled, match.Score, segConfidence),
                SegmentRefineOps.HeadingMarker(center, angled, contour),
                window);
        }
        finally
        {
            crop.Upright.Dispose();
        }
    }

    private static MaskTemplateMatchResult? MatchOnUpright(
        Mat upright, Mat template, RecipeConfig recipe, MaskTemplateRotationPack? pack,
        double refineRangeDeg, double searchOriginDeg, bool forceZeroBranch = false)
    {
        double? branch = forceZeroBranch ? searchOriginDeg : null;
        return recipe.Template.UseEdgeMatch
            ? MaskTemplateMatcher.MatchBestHybrid(
                upright, template, refineRangeDeg, recipe.Template.MatchThreshold,
                pack?.Gray, pack?.Edge, branch, searchOriginDeg)
            : MaskTemplateMatcher.MatchBest(
                upright, template, refineRangeDeg, recipe.Template.MatchThreshold,
                pack?.Gray, orientationBranchDeg: branch, searchOriginDeg: searchOriginDeg);
    }
}

internal sealed class SiftSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.Sift;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        var sift = MaskSiftRefine.TryRefine(request.RoiView, request.Points, request.SiftTeach);
        var dots = SegmentRefineOps.MapMatchDots(sift.Viz.Inliers, sift.Viz.Rejected);
        if (sift.Pose is null)
        {
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe, 0),
                DebugDots = dots,
            };
        }

        var r = sift.Pose;
        return new SegmentRefineHit
        {
            Pose = SegmentRefineOps.Ok(r.Center.X, r.Center.Y, r.AngleDeg, r.Score, request.SegmentConfidence),
            TabMarker = SegmentRefineOps.HeadingMarker(r.Center, r.AngleDeg, request.Points),
            DebugDots = dots,
            QualityNote = MaskSiftRefine.FormatQualityNote(
                r.Score, request.Recipe.Template.MatchThreshold, MaskSiftRefine.LastDebug),
        };
    }
}

internal sealed class ShapeMatchSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.ShapeMatch;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        var housing = MaskHousing.Fit(request.Points);
        var range = MaskHousing.AdaptiveRefineRange(request.Recipe.Template.RefineRangeDeg, housing);
        var shape = MaskShapeMatch.TryRefine(request.RoiView, request.Points, request.ShapeModel, range);
        var dots = SegmentRefineOps.MapMatchDots(shape.Viz.Inliers, shape.Viz.Rejected);
        if (shape.Pose is null)
        {
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe, 0),
                DebugDots = dots,
            };
        }

        var r = shape.Pose;
        return new SegmentRefineHit
        {
            Pose = SegmentRefineOps.Ok(r.Center.X, r.Center.Y, r.AngleDeg, r.Score, request.SegmentConfidence),
            TabMarker = SegmentRefineOps.HeadingMarker(r.Center, r.AngleDeg, request.Points),
            DebugDots = dots,
            QualityNote = MaskShapeMatch.FormatQualityNote(
                r.Score, request.Recipe.Template.MatchThreshold, MaskShapeMatch.LastDebug),
        };
    }
}

internal sealed class CaliperTabSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.CaliperTab;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        var attempt = MaskCaliperTab.TryRefine(
            request.RoiView, request.Points, CaliperRefineOptions.From(request.Recipe.Template));
        SegmentRefineOps.MapCaliperDebug(attempt.Viz, out var lines, out var dots);
        if (attempt.Pose is null)
        {
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe, 0),
                DebugLines = lines,
                DebugDots = dots,
            };
        }

        var r = attempt.Pose;
        return new SegmentRefineHit
        {
            Pose = SegmentRefineOps.Ok(r.Center.X, r.Center.Y, r.AngleDeg,
                MaskCaliperTab.QualityScore(MaskCaliperTab.LastDebug), request.SegmentConfidence),
            TabMarker =
            [
                new PixelPoint(r.TabMarkerFrom.X, r.TabMarkerFrom.Y),
                new PixelPoint(r.TabMarkerTo.X, r.TabMarkerTo.Y),
            ],
            DebugLines = lines,
            DebugDots = dots,
        };
    }
}

internal sealed class CentroidHoleSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.CentroidHoleLine;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        var mask = request.BitPackedMask;
        if (mask is null || request.MaskWidth <= 0 || request.MaskHeight <= 0)
        {
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe, 0),
            };
        }

        var box = Cv2.BoundingRect(request.Points);
        var r = MaskTemplateMatcher.RefineByCentroidHoleLine(mask, request.MaskWidth, request.MaskHeight);
        var pose = r is null
            ? SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe, 0)
            : SegmentRefineOps.Ok(r.Centroid.X + box.Left, r.Centroid.Y + box.Top,
                r.AngleDeg, 0.85, request.SegmentConfidence);
        return new SegmentRefineHit { Pose = pose };
    }
}

internal sealed class LineFitSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.LineFit;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        var housing = MaskHousing.Fit(request.Points);
        var (angle, center, fitted) = MaskTemplateMatcher.RefineByLineFit(request.Points, housing.LongAxisDeg);
        if (!fitted)
        {
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe, 0),
            };
        }

        var residual = Math.Abs(AngleGeometry.UndirectedDeltaDeg(angle, housing.LongAxisDeg));
        var score = Math.Clamp(1.0 - residual / 5.0, 0.2, 1);
        return new SegmentRefineHit
        {
            Pose = SegmentRefineOps.Ok(center.X, center.Y, angle, score, request.SegmentConfidence),
        };
    }
}
