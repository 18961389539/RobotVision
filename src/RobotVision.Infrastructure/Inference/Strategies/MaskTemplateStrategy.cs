using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Geometry;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式四：分割给粗定位（最小外接矩形长边角 θ₀ + 中心），模板匹配精修：
/// 目标区域按 θ₀ 转正裁剪后与示教模板做带旋转搜索的匹配（±refineRangeDeg ∪ +180°），
/// 同时得到精修角度（亚度）与头尾方向（消 180° 歧义），中心点由匹配位置精修。
/// 匹配分数低于阈值时回退粗角度输出（与模式一相同语义，无方向 [0,180)）。
/// </summary>
public sealed class MaskTemplateStrategy(ModelManager models, MaskTemplateRotationCache? rotations = null) : IAngleStrategy
{
    private readonly MaskTemplateRotationCache _rotations = rotations ?? MaskTemplateRotationCache.Shared;
    /// <summary>掩码包围盒最小面积（px²）：过小的掩码粗角度几乎随机（与模式一同口径）。</summary>
    private const double MinMaskAreaPx = 400;

    /// <summary>转正裁剪边距：相对矩形边长。窗口约 1.3×外接矩形（1 + 2×0.15）。</summary>
    private const double CropMarginRatio = 0.15;

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
                        if (template is not null)
                        {
                            pose = RefineByTemplate(roiView, points, template, recipe, segmentation.Confidence, pack);
                        }
                        else if (recipe.Template.RefineMethod == SegmentRefineMethod.CentroidHoleLine)
                        {
                            var r = MaskTemplateMatcher.RefineByCentroidHoleLine(
                                segmentation.BitPackedMask, box.Width, box.Height);
                            pose = r is null
                                ? FallbackCoarse(points, segmentation.Confidence)
                                : new PixelPose(r.Centroid.X + box.Left, r.Centroid.Y + box.Top,
                                    r.AngleDeg, segmentation.Confidence);
                        }
                        else if (recipe.Template.RefineMethod == SegmentRefineMethod.CaliperTab)
                        {
                            var r = MaskCaliperTab.Refine(roiView, points);
                            if (r is null)
                            {
                                pose = FallbackCoarse(points, segmentation.Confidence);
                            }
                            else
                            {
                                pose = new PixelPose(r.Center.X, r.Center.Y, r.AngleDeg, segmentation.Confidence);
                                tabMarker =
                                [
                                    new PixelPoint(r.TabMarkerFrom.X, r.TabMarkerFrom.Y),
                                    new PixelPoint(r.TabMarkerTo.X, r.TabMarkerTo.Y),
                                ];
                            }
                        }
                        else
                        {
                            pose = RefineByLineFit(points, segmentation.Confidence);
                        }
                        poses.Add(new PixelPose(pose.Cx + ox, pose.Cy + oy, pose.AngleDeg, pose.Score)
                        {
                            Overlay = new PoseOverlay
                            {
                                Contour = points.Select(p => new PixelPoint(p.X + ox, p.Y + oy)).ToArray(),
                                Boxes = [new PixelRect(box.Left + ox, box.Top + oy, box.Width, box.Height)],
                                Baseline = tabMarker is null
                                    ? null
                                    : [new PixelPoint(tabMarker[0].X + ox, tabMarker[0].Y + oy),
                                       new PixelPoint(tabMarker[1].X + ox, tabMarker[1].Y + oy)],
                                Label = segmentation.Label,
                            },
                        });
                    }

                    return poses.OrderByDescending(p => p.Score).ToList();
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

    /// <summary>兜底粗结果：外轮廓 minAreaRect（无方向 [0,180)），孔检测失败/基线不足时使用。
    /// 轮廓点由调用方传入（已在循环顶部提取并校验点数 ≥4）。</summary>
    private static PixelPose FallbackCoarse(Point2f[] contourPoints, double segConfidence)
    {
        var (center, angle) = MinAreaRectGeometry.LongAxis(contourPoints);
        return new PixelPose(center.X, center.Y, angle, segConfidence);
    }

    /// <summary>直线拟合精修：掩码长边 Huber 拟合修正粗角度，中心取轮廓均值（亚像素）。
    /// 角度无方向语义 [0,180)；带宽不足由 Matcher 内部兜底粗角度。</summary>
    private static PixelPose RefineByLineFit(Point2f[] contour, double segConfidence)
    {
        var (_, coarseAngle) = MinAreaRectGeometry.LongAxis(contour);
        var (angle, center) = MaskTemplateMatcher.RefineByLineFit(contour, coarseAngle);
        return new PixelPose(center.X, center.Y, angle, segConfidence);
    }

    /// <summary>分割结果 → 转正裁剪 → 模板匹配精修（角度/方向/中心）；匹配失败回退粗结果。
    /// 输入输出均为 ROI 内坐标系。</summary>
    private static PixelPose RefineByTemplate(
        Mat roiView, Point2f[] contour, Mat template, RecipeConfig recipe, double segConfidence,
        MaskTemplateRotationPack? pack)
    {
        // 粗结果（回退兜底）：长边角 + 矩形中心
        var (coarseCenter, coarseAngle) = MinAreaRectGeometry.LongAxis(contour);

        UprightCropResult crop;
        try
        {
            crop = MaskTemplateMatcher.UprightCrop(roiView, contour, CropMarginRatio);
        }
        catch (InvalidOperationException)
        {
            // 靠边目标转正裁剪失败：退回粗结果
            return new PixelPose(coarseCenter.X, coarseCenter.Y, coarseAngle, segConfidence);
        }

        try
        {
            var match = MatchOnUpright(crop.Upright, template, recipe, pack);
            // minAreaRect 长边有 180° 歧义：有的帧转正后凸起朝下、有的朝上。
            // 朝上时匹配走 180° 支，模板几何中心相对凸起偏了一侧，映回原图 Y 会跳一档（OSDP ~20px）。
            // 再转 180° 让转正窗与示教同向，只搜 0°±range，中心与 0° 支对齐。
            if (match is not null && MaskTemplateMatcher.NeedsUprightAlign(match))
            {
                UprightCropResult? flipped = null;
                try
                {
                    flipped = MaskTemplateMatcher.UprightCrop(
                        roiView, contour, CropMarginRatio, extraWarpDeg: 180);
                    var rematch = MatchOnUpright(flipped.Upright, template, recipe, pack, forceZeroBranch: true);
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
                    // 二次转正失败：保留第一次匹配
                }
                finally
                {
                    flipped?.Upright.Dispose();
                }
            }

            if (match is null)
                return new PixelPose(coarseCenter.X, coarseCenter.Y, coarseAngle, segConfidence);

            // 绝对角度 = 转正用的未归一化长边角 + 匹配旋转角（含 180° 头尾），有方向语义
            var angle = AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + match.RotationDeg);
            var center = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);
            return new PixelPose(center.X, center.Y, angle, segConfidence);
        }
        finally
        {
            crop.Upright.Dispose();
        }
    }

    private static MaskTemplateMatchResult? MatchOnUpright(
        Mat upright, Mat template, RecipeConfig recipe, MaskTemplateRotationPack? pack,
        bool forceZeroBranch = false)
    {
        double? branch = forceZeroBranch ? 0.0 : null;
        return recipe.Template.UseEdgeMatch
            ? MaskTemplateMatcher.MatchBestHybrid(
                upright, template, recipe.Template.RefineRangeDeg, recipe.Template.MatchThreshold,
                pack?.Gray, pack?.Edge, branch)
            : MaskTemplateMatcher.MatchBest(
                upright, template, recipe.Template.RefineRangeDeg, recipe.Template.MatchThreshold,
                pack?.Gray, orientationBranchDeg: branch);
    }
}
