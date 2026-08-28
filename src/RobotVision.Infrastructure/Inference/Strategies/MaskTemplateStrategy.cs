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
/// 模板匹配与卡尺融合：卡尺/直线定无向角与短轴中心，模板或凸起极性定 ±180°。
/// 精修不过门默认不输出无向粗角（Usable=false → TRIGGER 1019）；AllowCoarseFallback 恢复旧行为。
/// </summary>
public sealed class MaskTemplateStrategy(ModelManager models, MaskTemplateRotationCache? rotations = null) : IAngleStrategy
{
    private readonly MaskTemplateRotationCache _rotations = rotations ?? MaskTemplateRotationCache.Shared;
    private const double MinMaskAreaPx = 400;
    private const double CropMarginRatio = 0.15;
    private const double MaxGeometryTemplateFightDeg = 4.0;

    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        var useRefineTemplate = recipe.Template.RefineMethod == SegmentRefineMethod.Template;
        if (useRefineTemplate && string.IsNullOrEmpty(recipe.Template.TemplateImageBase64))
            throw new VisionException(VisionErrorCode.InvalidRecipeConfig,
                "分割+精修（模板匹配方法）未示教模板（配方页「示教模板」自动生成，或改用直线拟合 / 卡尺+凸起）");
        var pack = useRefineTemplate ? _rotations.GetOrCreate(recipe) : null;
        var template = pack?.Gray.Source;
        var ownedTemplate = false;
        if (useRefineTemplate && template is null)
        {
            template = MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
            ownedTemplate = true;
        }
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
                        if (template is not null)
                        {
                            var fused = RefineByTemplate(roiView, points, template, recipe, segmentation.Confidence, pack);
                            pose = fused.Pose;
                            tabMarker = fused.TabMarker;
                            debugLines = fused.DebugLines;
                            debugDots = fused.DebugDots;
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
                            var housing = MaskHousing.Fit(points);
                            var aspect = housing.LongLen / Math.Max(1.0, housing.ShortLen);
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
        PixelPose Pose, PixelPoint[]? TabMarker, OverlayLine[]? DebugLines, OverlayDot[]? DebugDots);

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

    /// <summary>转正裁剪 + 模板匹配；成功时用卡尺几何定 XY/无向角，模板定头尾。</summary>
    private static TemplateRefine RefineByTemplate(
        Mat roiView, Point2f[] contour, Mat template, RecipeConfig recipe, double segConfidence,
        MaskTemplateRotationPack? pack)
    {
        var housing = MaskHousing.Fit(contour);
        var range = MaskHousing.AdaptiveRefineRange(recipe.Template.RefineRangeDeg, housing);
        var attempt = MaskCaliperTab.TryRefine(roiView, contour, CaliperRefineOptions.From(recipe.Template));
        MapCaliperDebug(attempt.Viz, 0, 0, out var debugLines, out var debugDots);
        PixelPoint[]? tabMarker = null;
        if (attempt.Pose is { } cal)
        {
            tabMarker =
            [
                new PixelPoint(cal.TabMarkerFrom.X, cal.TabMarkerFrom.Y),
                new PixelPoint(cal.TabMarkerTo.X, cal.TabMarkerTo.Y),
            ];
        }

        UprightCropResult crop;
        try
        {
            crop = MaskTemplateMatcher.UprightCrop(roiView, contour, CropMarginRatio);
        }
        catch (InvalidOperationException)
        {
            return new TemplateRefine(Fallback(contour, segConfidence, recipe, 0), tabMarker, debugLines, debugDots);
        }

        try
        {
            var match = MatchOnUpright(crop.Upright, template, recipe, pack, range);
            if (match is not null && MaskTemplateMatcher.NeedsUprightAlign(match))
            {
                UprightCropResult? flipped = null;
                try
                {
                    flipped = MaskTemplateMatcher.UprightCrop(
                        roiView, contour, CropMarginRatio, extraWarpDeg: 180);
                    var rematch = MatchOnUpright(flipped.Upright, template, recipe, pack, range, forceZeroBranch: true);
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
                return new TemplateRefine(Fallback(contour, segConfidence, recipe, 0), tabMarker, debugLines, debugDots);

            var tplSigned = AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + match.RotationDeg);
            var tplCenter = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);

            if (attempt.Pose is { } geo)
            {
                var geoU = AngleGeometry.NormalizeDeg(geo.AngleDeg);
                if (AngleGeometry.UndirectedDeltaDeg(geoU, tplSigned) > MaxGeometryTemplateFightDeg)
                    return new TemplateRefine(
                        Fallback(contour, segConfidence, recipe, match.Score), tabMarker, debugLines, debugDots);

                var fused = AngleGeometry.FuseDirected(geoU, tplSigned);
                var score = Math.Clamp(0.65 * match.Score + 0.35 * CaliperScore(MaskCaliperTab.LastDebug), 0, 1);
                return new TemplateRefine(
                    Ok(geo.Center.X, geo.Center.Y, fused, score, segConfidence),
                    tabMarker, debugLines, debugDots);
            }

            return new TemplateRefine(
                Ok(tplCenter.X, tplCenter.Y, tplSigned, match.Score, segConfidence),
                tabMarker, debugLines, debugDots);
        }
        finally
        {
            crop.Upright.Dispose();
        }
    }

    private static MaskTemplateMatchResult? MatchOnUpright(
        Mat upright, Mat template, RecipeConfig recipe, MaskTemplateRotationPack? pack,
        double refineRangeDeg, bool forceZeroBranch = false)
    {
        double? branch = forceZeroBranch ? 0.0 : null;
        return recipe.Template.UseEdgeMatch
            ? MaskTemplateMatcher.MatchBestHybrid(
                upright, template, refineRangeDeg, recipe.Template.MatchThreshold,
                pack?.Gray, pack?.Edge, branch)
            : MaskTemplateMatcher.MatchBest(
                upright, template, refineRangeDeg, recipe.Template.MatchThreshold,
                pack?.Gray, orientationBranchDeg: branch);
    }
}
