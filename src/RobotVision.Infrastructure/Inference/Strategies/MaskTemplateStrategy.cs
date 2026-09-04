using System.Diagnostics.CodeAnalysis;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.JlVision;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式四：分割给粗框，精修给亚像素角与头尾。精修全部走 JLVision。
/// 精修不过门默认不输出无向粗角（Usable=false → TRIGGER 1019）；AllowCoarseFallback 恢复旧行为。
/// </summary>
public sealed class MaskTemplateStrategy(ModelManager models) : IAngleStrategy
{
    private const double MinMaskAreaPx = 400;
    private readonly SegmentRefineRuntimeRegistry _refine = SegmentRefineRuntimeRegistry.Default;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "ROI Mat 在 finally 释放。")]
    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        var method = recipe.Template.RefineMethod;
        var needsTemplate = method is SegmentRefineMethod.Template
            or SegmentRefineMethod.Sift
            or SegmentRefineMethod.ShapeMatch;
        if (needsTemplate && string.IsNullOrEmpty(recipe.Template.TemplateImageBase64))
            throw new VisionException(VisionErrorCode.InvalidRecipeConfig,
                "分割+精修（模板匹配 / 形状匹配）未示教模板（配方页「示教模板」自动生成，或改用直线拟合 / 卡尺）");

        if (method is SegmentRefineMethod.ShapeMatch or SegmentRefineMethod.Sift)
            JlShapeTeachCache.Warm(recipe);
        if (method == SegmentRefineMethod.Template)
            JlNccTeachCache.Warm(recipe);

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

            var runtime = _refine.Get(method);
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

                    var hit = runtime.Refine(new SegmentRefineRequest
                    {
                        RoiView = roiView,
                        Points = points,
                        Recipe = recipe,
                        SegmentConfidence = segmentation.Confidence,
                        BitPackedMask = segmentation.BitPackedMask,
                        MaskWidth = box.Width,
                        MaskHeight = box.Height,
                        BoxLeft = box.Left,
                        BoxTop = box.Top,
                    });

                    var pose = hit.Pose;
                    var usable = pose.Usable;
                    if (usable)
                    {
                        var area = Cv2.ContourArea(points);
                        var aspect = JlHousing.Aspect(JlHousing.FitObb(points));
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
                            Baseline = hit.TabMarker is null
                                ? null
                                : [new PixelPoint(hit.TabMarker[0].X + ox, hit.TabMarker[0].Y + oy),
                                   new PixelPoint(hit.TabMarker[1].X + ox, hit.TabMarker[1].Y + oy)],
                            DebugLines = ShiftLines(hit.DebugLines, ox, oy),
                            DebugDots = ShiftDots(hit.DebugDots, ox, oy),
                            MatchWindow = ShiftPoints(hit.MatchWindow, ox, oy),
                            RefineQualityNote = hit.QualityNote,
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
}
