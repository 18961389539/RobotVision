using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式四：分割给粗框，精修给亚像素角与头尾。
/// 模板匹配：NCC 匹配峰定 XY（映回原图），转正窗（可关）定有向角，不跑卡尺。卡尺只在 RefineMethod=CaliperTab 时使用。
/// 精修不过门默认不输出无向粗角（Usable=false → TRIGGER 1019）；AllowCoarseFallback 恢复旧行为。
/// </summary>
public sealed class MaskTemplateStrategy(ModelManager models, MaskTemplateRotationCache? rotations = null) : IAngleStrategy
{
    private readonly MaskTemplateRotationCache _rotations = rotations ?? MaskTemplateRotationCache.Shared;
    private const double MinMaskAreaPx = 400;
    private const double CropMarginRatio = 0.15;

    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        var useRefineTemplate = recipe.Template.RefineMethod == SegmentRefineMethod.Template;
        var useSift = recipe.Template.RefineMethod == SegmentRefineMethod.Sift;
        var useShapeMatch = recipe.Template.RefineMethod == SegmentRefineMethod.ShapeMatch;
        if ((useRefineTemplate || useSift || useShapeMatch) && string.IsNullOrEmpty(recipe.Template.TemplateImageBase64))
            throw new VisionException(VisionErrorCode.InvalidRecipeConfig,
                "分割+精修（模板匹配 / SIFT / 形状匹配）未示教模板（配方页「示教模板」自动生成，或改用直线拟合 / 卡尺+凸起）");
        var pack = useRefineTemplate ? _rotations.GetOrCreate(recipe) : null;
        var template = pack?.Gray.Source;
        var ownedTemplate = false;
        if (useRefineTemplate && template is null)
        {
            template = MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
            ownedTemplate = true;
        }

        var siftTeach = useSift ? MaskSiftRefine.GetOrCreate(recipe) : null;
        var shapeModel = useShapeMatch ? MaskShapeMatch.GetOrCreate(recipe) : null;
        try
        {
            using var roiImage = RoiHelper.CropToVisionImage(undistorted, recipe.Roi, out var ox, out var oy);
            var input = roiImage ?? undistorted;
            using var full = VisionImageCv.AsMat(undistorted);
            Mat? roiOwned = recipe.Roi is null ? null : RoiHelper.Crop(full, recipe.Roi, out _, out _);
            var roiView = roiOwned ?? full;
            try
            {
                var session = models.Open(recipe.Models[0], InferenceTask.Segmentation);
                var results = InferenceStageClock.MeasureSegment(() =>
                    session.Run(y =>
                        y.RunSegmentation(input, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou), ct));

                return InferenceStageClock.MeasureRefine(() =>
                {
                    var poses = new List<PixelPose>();
                    foreach (var segmentation in results)
                    {
                        var box = segmentation.Box;
                        if ((double)box.Width * box.Height < MinMaskAreaPx)
                            continue;

                        var contour = segmentation.ContourLocal;
                        if (contour.Count < 4)
                            continue;
                        var points = new Point2f[contour.Count];
                        for (var i = 0; i < contour.Count; i++)
                            points[i] = new Point2f((float)(contour[i].X + box.Left), (float)(contour[i].Y + box.Top));

                        PixelPose pose;
                        PixelPoint[]? tabMarker = null;
                        OverlayLine[]? debugLines = null;
                        OverlayDot[]? debugDots = null;
                        PixelPoint[]? matchWindow = null;
                        if (useSift)
                        {
                            var sift = MaskSiftRefine.TryRefine(roiView, points, siftTeach);
                            MapMatchDots(sift.Viz.Inliers, sift.Viz.Rejected, 0, 0, out debugDots);
                            if (sift.Pose is null)
                            {
                                pose = Fallback(points, segmentation.Confidence, recipe, score: 0);
                            }
                            else
                            {
                                var r = sift.Pose;
                                pose = Ok(r.Center.X, r.Center.Y, r.AngleDeg, r.Score, segmentation.Confidence);
                                tabMarker = HeadingMarker(r.Center, r.AngleDeg, points);
                            }
                        }
                        else if (useShapeMatch)
                        {
                            var housing = MaskHousing.Fit(points);
                            var range = MaskHousing.AdaptiveRefineRange(recipe.Template.RefineRangeDeg, housing);
                            var shape = MaskShapeMatch.TryRefine(roiView, points, shapeModel, range);
                            MapMatchDots(shape.Viz.Inliers, shape.Viz.Rejected, 0, 0, out debugDots);
                            if (shape.Pose is null)
                            {
                                pose = Fallback(points, segmentation.Confidence, recipe, score: 0);
                            }
                            else
                            {
                                var r = shape.Pose;
                                pose = Ok(r.Center.X, r.Center.Y, r.AngleDeg, r.Score, segmentation.Confidence);
                                tabMarker = HeadingMarker(r.Center, r.AngleDeg, points);
                            }
                        }
                        else if (template is not null)
                        {
                            var refined = RefineByTemplate(roiView, points, template, recipe, segmentation.Confidence, pack);
                            pose = refined.Pose;
                            tabMarker = refined.TabMarker;
                            matchWindow = refined.MatchWindow;
                        }
                        else if (recipe.Template.RefineMethod == SegmentRefineMethod.CentroidHoleLine)
                        {
                            var r = MaskTemplateMatcher.RefineByCentroidHoleLine(
                                segmentation.BitPackedMask, box.Width, box.Height);
                            pose = r is null
                                ? Fallback(points, segmentation.Confidence, recipe, score: 0)
                                : Ok(r.Centroid.X + box.Left, r.Centroid.Y + box.Top,
                                    r.AngleDeg, 0.85, segmentation.Confidence);
                        }
                        else if (recipe.Template.RefineMethod == SegmentRefineMethod.CaliperTab)
                        {
                            var attempt = MaskCaliperTab.TryRefine(
                                roiView, points, CaliperRefineOptions.From(recipe.Template));
                            MapCaliperDebug(attempt.Viz, 0, 0, out debugLines, out debugDots);
                            if (attempt.Pose is null)
                            {
                                pose = Fallback(points, segmentation.Confidence, recipe, score: 0);
                            }
                            else
                            {
                                var r = attempt.Pose;
                                pose = Ok(r.Center.X, r.Center.Y, r.AngleDeg,
                                    CaliperScore(MaskCaliperTab.LastDebug), segmentation.Confidence);
                                tabMarker =
                                [
                                    new PixelPoint(r.TabMarkerFrom.X, r.TabMarkerFrom.Y),
                                    new PixelPoint(r.TabMarkerTo.X, r.TabMarkerTo.Y),
                                ];
                            }
                        }
                        else
                        {
                            pose = RefineByLineFit(points, segmentation.Confidence, recipe);
                        }

                        var usable = pose.Usable;
                        if (usable)
                        {
                            var area = Cv2.ContourArea(points);
                            var aspect = MaskHousing.Aspect(MaskHousing.FitObb(points));
                            if (!InstanceGeometry.Accepts(recipe.Template, area, aspect))
                                usable = false;
                        }

                        poses.Add(new PixelPose(pose.Cx + ox, pose.Cy + oy, pose.AngleDeg, pose.Score)
                        {
                            SegmentScore = pose.SegmentScore ?? segmentation.Confidence,
                            Usable = usable,
                            Overlay = new PoseOverlay
                            {
                                Contour = points.Select(p => new PixelPoint(p.X + ox, p.Y + oy)).ToArray(),
                                Boxes = [new PixelRect(box.Left + ox, box.Top + oy, box.Width, box.Height)],
                                Baseline = tabMarker is null
                                    ? null
                                    : [new PixelPoint(tabMarker[0].X + ox, tabMarker[0].Y + oy),
                                       new PixelPoint(tabMarker[1].X + ox, tabMarker[1].Y + oy)],
                                DebugLines = ShiftLines(debugLines, ox, oy),
                                DebugDots = ShiftDots(debugDots, ox, oy),
                                MatchWindow = ShiftPoints(matchWindow, ox, oy),
                                Label = segmentation.Label,
                                BitPackedMask = segmentation.BitPackedMask is { Length: > 0 } packed
                                    ? packed
                                    : null,
                                MaskWidth = box.Width,
                                MaskHeight = box.Height,
                            },
                        });
                    }

                    PixelPoseOutput.EnforceExpectedCount(poses, recipe.Template.ExpectedCount);
                    return poses
                        .OrderByDescending(p => p.Usable)
                        .ThenByDescending(p => p.Score)
                        .ToList();
                });
            }
            finally
            {
                roiOwned?.Dispose();
            }
        }
        finally
        {
            if (ownedTemplate)
                template?.Dispose();
        }
    }

    private readonly record struct TemplateRefine(
        PixelPose Pose, PixelPoint[]? TabMarker, PixelPoint[]? MatchWindow = null);

    private static PixelPose Fallback(Point2f[] contourPoints, double segConfidence, RecipeConfig recipe, double score)
    {
        var housing = MaskHousing.Fit(contourPoints);
        return new PixelPose(housing.Center.X, housing.Center.Y, housing.LongAxisDeg, score)
        {
            SegmentScore = segConfidence,
            Usable = recipe.Template.AllowCoarseFallback,
        };
    }

    private static PixelPose Ok(double cx, double cy, double angleDeg, double refineScore, double segConfidence) =>
        new(cx, cy, angleDeg, refineScore)
        {
            SegmentScore = segConfidence,
            Usable = true,
        };

    private static double CaliperScore(MaskCaliperTab.DebugInfo d) =>
        MaskCaliperTab.QualityScore(d);

    private static OverlayLine[]? ShiftLines(OverlayLine[]? lines, double ox, double oy)
    {
        if (lines is null || lines.Length == 0)
            return lines;
        var copy = new OverlayLine[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            copy[i] = new OverlayLine(
                new PixelPoint(l.From.X + ox, l.From.Y + oy),
                new PixelPoint(l.To.X + ox, l.To.Y + oy),
                l.Kind);
        }

        return copy;
    }

    private static OverlayDot[]? ShiftDots(OverlayDot[]? dots, double ox, double oy)
    {
        if (dots is null || dots.Length == 0)
            return dots;
        var copy = new OverlayDot[dots.Length];
        for (var i = 0; i < dots.Length; i++)
            copy[i] = new OverlayDot(new PixelPoint(dots[i].At.X + ox, dots[i].At.Y + oy), dots[i].Kind);
        return copy;
    }

    private static PixelPoint[]? ShiftPoints(PixelPoint[]? points, double ox, double oy)
    {
        if (points is null || points.Length == 0)
            return points;
        var copy = new PixelPoint[points.Length];
        for (var i = 0; i < points.Length; i++)
            copy[i] = new PixelPoint(points[i].X + ox, points[i].Y + oy);
        return copy;
    }

    private static PixelPoint[] HeadingMarker(Point2d center, double angleDeg, Point2f[] contour)
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

    private static void MapMatchDots(
        IReadOnlyList<Point2d> inliers, IReadOnlyList<Point2d> rejected, double ox, double oy, out OverlayDot[]? dots)
    {
        var n = inliers.Count + rejected.Count;
        if (n == 0)
        {
            dots = null;
            return;
        }

        var buf = new OverlayDot[n];
        var d = 0;
        foreach (var p in inliers)
            buf[d++] = new OverlayDot(new PixelPoint(p.X + ox, p.Y + oy), OverlayDotKind.Inlier);
        foreach (var p in rejected)
            buf[d++] = new OverlayDot(new PixelPoint(p.X + ox, p.Y + oy), OverlayDotKind.Rejected);
        dots = buf;
    }

    private static void MapCaliperDebug(
        MaskCaliperTab.CaliperViz viz, double ox, double oy,
        out OverlayLine[]? lines, out OverlayDot[]? dots)
    {
        PixelPoint Map(Point2d p) => new(p.X + ox, p.Y + oy);
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

    private static PixelPose RefineByLineFit(Point2f[] contour, double segConfidence, RecipeConfig recipe)
    {
        var housing = MaskHousing.Fit(contour);
        var (angle, center, fitted) = MaskTemplateMatcher.RefineByLineFit(contour, housing.LongAxisDeg);
        if (!fitted)
            return Fallback(contour, segConfidence, recipe, score: 0);
        var residual = Math.Abs(AngleGeometry.UndirectedDeltaDeg(angle, housing.LongAxisDeg));
        var score = Math.Clamp(1.0 - residual / 5.0, 0.2, 1);
        return Ok(center.X, center.Y, angle, score, segConfidence);
    }

    /// <summary>可选转正裁剪 + 模板匹配；XY 用 NCC 匹配峰映回原图，有向角用 NCC（特征框决定示教吃哪块，峰在哪 XY 就在哪）。</summary>
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
                ? MaskTemplateMatcher.UprightCrop(roiView, contour, CropMarginRatio)
                : MaskTemplateMatcher.AxisAlignedCrop(roiView, contour, CropMarginRatio);
        }
        catch (InvalidOperationException)
        {
            return new TemplateRefine(Fallback(contour, segConfidence, recipe, 0), null);
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
                        roiView, contour, CropMarginRatio, extraWarpDeg: 180);
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
                return new TemplateRefine(Fallback(contour, segConfidence, recipe, 0), null);

            var tplSigned = AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + match.RotationDeg);
            var angled = AngleGeometry.FuseDirected(housing.LongAxisDeg, tplSigned);
            var center = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);
            var window = PoseOverlay.TemplateMatchWindow(
                center.X, center.Y, angled, template.Width, template.Height);
            return new TemplateRefine(
                Ok(center.X, center.Y, angled, match.Score, segConfidence),
                HeadingMarker(center, angled, contour),
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
