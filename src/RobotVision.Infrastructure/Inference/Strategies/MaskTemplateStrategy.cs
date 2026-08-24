using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using YoloDotNet.Extensions;
using YoloDotNet.Models;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式四：分割给粗定位（最小外接矩形长边角 θ₀ + 中心），模板匹配精修：
/// 目标区域按 θ₀ 转正裁剪后与示教模板做带旋转搜索的匹配（±refineRangeDeg ∪ +180°），
/// 同时得到精修角度（亚度）与头尾方向（消 180° 歧义），中心点由匹配位置精修。
/// 匹配分数低于阈值时回退粗角度输出（与模式一相同语义，无方向 [0,180)）。
/// </summary>
public sealed class MaskTemplateStrategy(ModelManager models) : IAngleStrategy
{
    /// <summary>掩码包围盒最小面积（px²）：过小的掩码粗角度几乎随机（与模式一同口径）。</summary>
    private const double MinMaskAreaPx = 400;

    /// <summary>转正裁剪边距：为旋转模板滑窗留余量（相对矩形边长）。</summary>
    private const double CropMarginRatio = 0.3;

    public List<PixelPose> Compute(Mat undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        var useRefineTemplate = recipe.Template.RefineMethod == SegmentRefineMethod.Template;
        if (useRefineTemplate && string.IsNullOrEmpty(recipe.Template.TemplateImageBase64))
            throw new VisionException(VisionErrorCode.InternalError,
                "分割+精修（模板匹配方法）未示教模板（配方页「示教模板」自动生成，或改用直线拟合方法）");
        Mat? template = useRefineTemplate
            ? MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64)
            : null;
        try
        {
            // 推理与匹配都在 ROI 内完成（小图匹配快），坐标最后统一偏移回全图。
            // roiOwned 仅在配置 ROI 时存在（视图 Dispose 不影响原图数据）；ROI 为空直接用原图。
            using var bitmap = RoiHelper.ToBitmap(undistorted, recipe.Roi, out var ox, out var oy);
            Mat? roiOwned = recipe.Roi is null ? null : RoiHelper.Crop(undistorted, recipe.Roi, out _, out _);
            var roiView = roiOwned ?? undistorted;
            try
            {
                var session = models.Open(recipe.Models[0], InferenceTask.Segmentation);
                var results = session.Run(y =>
                    y.RunSegmentation(bitmap, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou), ct);

                var poses = new List<PixelPose>();
                foreach (var segmentation in results)
                {
                    var box = segmentation.BoundingBox;
                    if ((double)box.Width * box.Height < MinMaskAreaPx)
                        continue;

                    // 精修分支：模板匹配（吃原图纹理，可判头尾）/ 直线拟合（吃掩码轮廓，弱纹理）/
                    // 质心-内孔连线（吃掩码孔洞几何，有方向，与纹理无关）
                    PixelPose pose;
                    if (template is not null)
                    {
                        var contour = segmentation.GetContourPoints();
                        if (contour.Length < 4)
                            continue;
                        var points = new Point2f[contour.Length];
                        for (var i = 0; i < contour.Length; i++)
                            points[i] = new Point2f(contour[i].X + box.Left, contour[i].Y + box.Top);
                        pose = RefineByTemplate(roiView, points, template, recipe, segmentation.Confidence);
                    }
                    else if (recipe.Template.RefineMethod == SegmentRefineMethod.CentroidHoleLine)
                    {
                        var r = MaskTemplateMatcher.RefineByCentroidHoleLine(
                            segmentation.BitPackedPixelMask, box.Width, box.Height);
                        pose = r is null
                            ? FallbackCoarse(segmentation, segConfidence: segmentation.Confidence)
                            : new PixelPose(r.Centroid.X + box.Left, r.Centroid.Y + box.Top,
                                r.AngleDeg, segmentation.Confidence);
                    }
                    else
                    {
                        var contour = segmentation.GetContourPoints();
                        if (contour.Length < 4)
                            continue;
                        var points = new Point2f[contour.Length];
                        for (var i = 0; i < contour.Length; i++)
                            points[i] = new Point2f(contour[i].X + box.Left, contour[i].Y + box.Top);
                        pose = RefineByLineFit(points, segmentation.Confidence);
                    }
                    poses.Add(new PixelPose(pose.Cx + ox, pose.Cy + oy, pose.AngleDeg, pose.Score));
                }

                return poses.OrderByDescending(p => p.Score).ToList();
            }
            finally
            {
                roiOwned?.Dispose();
            }
        }
        finally
        {
            template?.Dispose();
        }
    }

    /// <summary>兜底粗结果：外轮廓 minAreaRect（无方向 [0,180)），孔检测失败/基线不足时使用。</summary>
    private static PixelPose FallbackCoarse(Segmentation segmentation, double segConfidence)
    {
        var box = segmentation.BoundingBox;
        var contour = segmentation.GetContourPoints();
        var points = new Point2f[contour.Length];
        for (var i = 0; i < contour.Length; i++)
            points[i] = new Point2f(contour[i].X + box.Left, contour[i].Y + box.Top);
        var (center, angle) = AngleGeometry.LongAxisFromMinAreaRect(points);
        return new PixelPose(center.X, center.Y, angle, segConfidence);
    }

    /// <summary>直线拟合精修：掩码长边 Huber 拟合修正粗角度，中心取轮廓均值（亚像素）。
    /// 角度无方向语义 [0,180)；带宽不足由 Matcher 内部兜底粗角度。</summary>
    private static PixelPose RefineByLineFit(Point2f[] contour, double segConfidence)
    {
        var (_, coarseAngle) = AngleGeometry.LongAxisFromMinAreaRect(contour);
        var (angle, center) = MaskTemplateMatcher.RefineByLineFit(contour, coarseAngle);
        return new PixelPose(center.X, center.Y, angle, segConfidence);
    }

    /// <summary>分割结果 → 转正裁剪 → 模板匹配精修（角度/方向/中心）；匹配失败回退粗结果。
    /// 输入输出均为 ROI 内坐标系。</summary>
    private static PixelPose RefineByTemplate(
        Mat roiView, Point2f[] contour, Mat template, RecipeConfig recipe, double segConfidence)
    {
        // 粗结果（回退兜底）：长边角 + 矩形中心
        var (coarseCenter, coarseAngle) = AngleGeometry.LongAxisFromMinAreaRect(contour);

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

        using (crop.Upright)
        {
            // 混合判决（UseEdgeMatch）：边缘图定角度 + 灰度图定头尾；否则灰度直匹配
            var match = recipe.Template.UseEdgeMatch
                ? MaskTemplateMatcher.MatchBestHybrid(
                    crop.Upright, template, recipe.Template.RefineRangeDeg, recipe.Template.MatchThreshold)
                : MaskTemplateMatcher.MatchBest(
                    crop.Upright, template, recipe.Template.RefineRangeDeg, recipe.Template.MatchThreshold);
            if (match is null)
                return new PixelPose(coarseCenter.X, coarseCenter.Y, coarseAngle, segConfidence);

            // 绝对角度 = 转正用的未归一化长边角 + 匹配旋转角（含 180° 头尾），有方向语义
            var angle = AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + match.RotationDeg);
            var center = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);
            return new PixelPose(center.X, center.Y, angle, segConfidence);
        }
    }
}
