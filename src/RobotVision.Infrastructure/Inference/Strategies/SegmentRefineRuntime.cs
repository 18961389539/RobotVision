using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.JlVision;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>一次分割实例的精修输入（ROI 图 + 轮廓）。</summary>
public readonly struct SegmentRefineRequest
{
    public required Mat RoiView { get; init; }
    public required Point2f[] Points { get; init; }
    public required RecipeConfig Recipe { get; init; }
    public required double SegmentConfidence { get; init; }
    public byte[]? BitPackedMask { get; init; }
    public int MaskWidth { get; init; }
    public int MaskHeight { get; init; }
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
/// 全部后端为 JLVision。
/// </summary>
public interface ISegmentRefineRuntime
{
    SegmentRefineMethod Method { get; }

    SegmentRefineHit Refine(in SegmentRefineRequest request);
}

/// <summary>按 <see cref="SegmentRefineMethod"/> 分发精修实现。</summary>
public sealed class SegmentRefineRuntimeRegistry
{
    public static SegmentRefineRuntimeRegistry Default { get; } = CreateDefault();

    private readonly Dictionary<SegmentRefineMethod, ISegmentRefineRuntime> _runtimes = [];

    public static SegmentRefineRuntimeRegistry CreateDefault()
    {
        var registry = new SegmentRefineRuntimeRegistry();
        return registry;
    }

    public SegmentRefineRuntimeRegistry()
    {
        Register(new TemplateSegmentRefineRuntime());
        Register(new SiftSegmentRefineRuntime());
        Register(new ShapeMatchSegmentRefineRuntime());
        Register(new CaliperTabSegmentRefineRuntime());
        Register(new CentroidHoleLineSegmentRefineRuntime());
        Register(new LineFitSegmentRefineRuntime());
    }

    public void Register(ISegmentRefineRuntime runtime) => _runtimes[runtime.Method] = runtime;

    public bool IsKnown(SegmentRefineMethod method) => _runtimes.ContainsKey(method);

    public IReadOnlyList<SegmentRefineMethod> Methods =>
        _runtimes.Keys.OrderBy(m => (int)m).ToList();

    public ISegmentRefineRuntime Get(SegmentRefineMethod method) =>
        _runtimes.TryGetValue(method, out var runtime)
            ? runtime
            : _runtimes[SegmentRefineMethod.LineFit];
}

internal static class SegmentRefineOps
{
    public static SegmentRefineHit Fallback(
        Point2f[] contour, double segmentConfidence, RecipeConfig recipe)
    {
        var frame = JlHousing.FitObb(contour);
        var angle = frame.WarpAngleDeg;
        var center = new Point2d(frame.Center.X, frame.Center.Y);
        return new SegmentRefineHit
        {
            Pose = Ok(
                center.X, center.Y, angle, 0.2, segmentConfidence,
                recipe.Template.AllowCoarseFallback),
            TabMarker = HeadingMarker(center, angle, frame),
            QualityNote = recipe.Template.AllowCoarseFallback
                ? "精修失败，回退粗角"
                : null,
        };
    }

    public static PixelPose Ok(
        double cx, double cy, double angleDeg, double score, double segmentConfidence, bool usable = true) =>
        new(cx, cy, angleDeg, score)
        {
            SegmentScore = segmentConfidence,
            Usable = usable,
        };

    public const double CropMarginRatio = JlTemplateIo.CropMarginRatio;

    public static PixelPoint[] HeadingMarker(Point2d center, double angleDeg, Point2f[] contour)
    {
        var frame = JlHousing.FitObb(contour);
        return HeadingMarker(center, angleDeg, frame);
    }

    public static PixelPoint[] HeadingMarker(Point2d center, double angleDeg, HousingFrame frame)
    {
        var halfLong = frame.LongLen * 0.35;
        var rad = angleDeg * Math.PI / 180.0;
        var dir = new Point2d(Math.Cos(rad), Math.Sin(rad));
        var from = center - dir * (halfLong * 0.3);
        var to = center + dir * halfLong;
        return
        [
            new PixelPoint(from.X, from.Y),
            new PixelPoint(to.X, to.Y),
        ];
    }

    public static SegmentRefineHit FromJl(
        in SegmentRefineRequest request, JlRefineHit hit, string tag, bool alignOrigin = true)
    {
        if (!hit.Found)
        {
            var fallback = Fallback(request.Points, request.SegmentConfidence, request.Recipe);
            return fallback with { QualityNote = "JLVision " + hit.Note };
        }

        var angle = JlPoseAlign.AlignToTeachAngle(request.Points, hit.AngleDeg);
        var cx = hit.Cx;
        var cy = hit.Cy;
        if (alignOrigin)
        {
            var off = JlShapeTeachCache.HousingOffset;
            var origin = JlPoseAlign.AlignToTeachOrigin(request.Points, angle, off.Ox, off.Oy);
            cx = origin.X;
            cy = origin.Y;
        }

        return new SegmentRefineHit
        {
            Pose = Ok(cx, cy, angle, hit.Score, request.SegmentConfidence),
            TabMarker = HeadingMarker(new Point2d(cx, cy), angle, request.Points),
            QualityNote = $"JLVision {tag} {hit.Note}",
        };
    }
}

internal sealed class TemplateSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.Template;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        try
        {
            var model = JlNccTeachCache.GetOrCreate(request.Recipe);
            if (model is null)
            {
                var fallback = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe);
                return fallback with { QualityNote = "未示教模板或 NCC 模型创建失败" };
            }

            var housing = JlHousing.Fit(request.Points);
            var bounds = JlHousing.AdaptiveRefineBounds(request.Recipe.Template.GetRefineAngleWindow(), housing);
            using var scene = JlImageConvert.FromGrayMat(request.RoiView);
            var hit = JlNccRefine.TryRefine(
                scene, request.Points, model, bounds,
                Math.Min(request.Recipe.Template.MatchThreshold, 0.40),
                JlFindOptions.ForRecipe(request.Recipe.Template));
            if (!hit.Found || hit.Score < request.Recipe.Template.MatchThreshold)
            {
                var fallback = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe);
                return fallback with
                {
                    QualityNote = hit.Found
                        ? $"JLVision ncc {hit.Score:0.00} < 门 {request.Recipe.Template.MatchThreshold:0.00}"
                        : "JLVision ncc " + hit.Note,
                };
            }

            var angle = JlPoseAlign.AlignToTeachAngle(request.Points, hit.AngleDeg);
            var size = JlNccTeachCache.TemplateSize;
            var window = PoseOverlay.TemplateMatchWindow(hit.Cx, hit.Cy, angle, size.Width, size.Height);
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Ok(hit.Cx, hit.Cy, angle, hit.Score, request.SegmentConfidence),
                MatchWindow = window,
                TabMarker = SegmentRefineOps.HeadingMarker(
                    new Point2d(hit.Cx, hit.Cy), angle, request.Points),
                QualityNote = $"JLVision ncc {hit.Note}",
            };
        }
        catch (Exception)
        {
            return SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe)
                with { QualityNote = "JLVision ncc 异常" };
        }
    }
}

internal sealed class SiftSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.Sift;

    public SegmentRefineHit Refine(in SegmentRefineRequest request) =>
        ShapeMatchSegmentRefineRuntime.RefineJlShape(request, "sift→shape");
}

internal sealed class ShapeMatchSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.ShapeMatch;

    public SegmentRefineHit Refine(in SegmentRefineRequest request) =>
        RefineJlShape(request, "shape");

    internal static SegmentRefineHit RefineJlShape(in SegmentRefineRequest request, string tag)
    {
        try
        {
            var model = JlShapeTeachCache.GetOrCreate(request.Recipe);
            if (model is null)
            {
                var fallback = SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe);
                return fallback with { QualityNote = "JLVision 无形状模型" };
            }

            var housing = JlHousing.Fit(request.Points);
            var bounds = JlHousing.AdaptiveRefineBounds(request.Recipe.Template.GetRefineAngleWindow(), housing);
            using var scene = JlImageConvert.FromGrayMat(request.RoiView);
            var minScore = Math.Clamp(
                request.Recipe.Template.MatchThreshold > 0
                    ? request.Recipe.Template.MatchThreshold
                    : JlShapeDefaults.FindMinScore,
                0.01, 1.0);
            var hit = JlShapeRefine.TryRefine(
                scene, request.Points, model, bounds,
                minScore,
                JlFindOptions.ForRecipe(request.Recipe.Template));
            var viaShape = hit.Found;
            if (!hit.Found)
                hit = JlGeometryFallback.TryRefine(
                    scene, request.Points, request.Recipe.Template.HousingEdgePolarity);
            var used = viaShape ? tag : "geo";
            return SegmentRefineOps.FromJl(request, hit, used);
        }
        catch (Exception)
        {
            return SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe)
                with { QualityNote = "JLVision shape 异常" };
        }
    }
}

internal sealed class CaliperTabSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.CaliperTab;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        try
        {
            using var scene = JlImageConvert.FromGrayMat(request.RoiView);
            var hit = JlMeasureRefine.TryRefine(
                scene, request.Points, request.Recipe.Template.HousingEdgePolarity);
            return SegmentRefineOps.FromJl(request, hit, "measure", alignOrigin: false);
        }
        catch (Exception)
        {
            return SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe)
                with { QualityNote = "JLVision measure 异常" };
        }
    }
}

internal sealed class CentroidHoleLineSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.CentroidHoleLine;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        if (request.BitPackedMask is null || request.MaskWidth < 1 || request.MaskHeight < 1)
            return SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe);

        var r = JlCentroidHole.TryRefine(request.BitPackedMask, request.MaskWidth, request.MaskHeight);
        if (r is null)
            return SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe);

        var cx = r.Value.Centroid.X + request.BoxLeft;
        var cy = r.Value.Centroid.Y + request.BoxTop;
        return new SegmentRefineHit
        {
            Pose = SegmentRefineOps.Ok(cx, cy, r.Value.AngleDeg, r.Value.Quality, request.SegmentConfidence),
            QualityNote = "JLVision centroid-hole",
        };
    }
}

internal sealed class LineFitSegmentRefineRuntime : ISegmentRefineRuntime
{
    public SegmentRefineMethod Method => SegmentRefineMethod.LineFit;

    public SegmentRefineHit Refine(in SegmentRefineRequest request)
    {
        var housing = JlHousing.Fit(request.Points);
        var line = JlPoseAlign.TryLineFit(request.Points, housing.LongAxisDeg);
        if (line.Fitted)
        {
            var residual = Math.Abs(AngleGeometry.UndirectedDeltaDeg(line.AngleDeg, housing.LongAxisDeg));
            var score = Math.Clamp(1.0 - residual / 5.0, 0.2, 1);
            var angle = JlPoseAlign.AlignToTeachAngle(request.Points, line.AngleDeg);
            return new SegmentRefineHit
            {
                Pose = SegmentRefineOps.Ok(
                    line.Center.X, line.Center.Y, angle, score, request.SegmentConfidence),
                TabMarker = SegmentRefineOps.HeadingMarker(line.Center, angle, request.Points),
                QualityNote = $"JLVision line residual={residual:0.00}°",
            };
        }

        try
        {
            using var scene = JlImageConvert.FromGrayMat(request.RoiView);
            var hit = JlMeasureRefine.TryRefine(
                scene, request.Points, request.Recipe.Template.HousingEdgePolarity);
            return SegmentRefineOps.FromJl(request, hit, "line→measure", alignOrigin: false);
        }
        catch (Exception)
        {
            return SegmentRefineOps.Fallback(request.Points, request.SegmentConfidence, request.Recipe);
        }
    }
}
