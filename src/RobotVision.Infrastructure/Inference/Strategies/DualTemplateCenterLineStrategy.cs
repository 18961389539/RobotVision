using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Vision;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式六：双模板连线（纯图像处理，无需模型）。NCC 匹配：
/// 模板1 只在配方 <c>Roi</c>（ROI1）内检测，匹配中心定位 XY；
/// 设了 <see cref="DualTemplateOptions.SecondaryRoi"/> 时模板2 只在 ROI2 内检测（互斥）；
/// 未设次区时在模板1 匹配盒按 CropExpandRatio 外扩窗口内搜模板2。
/// 角度 = 模板1 中心→模板2 中心连线（(-180,180] 有方向）。任一侧未命中则不输出。
/// </summary>
public sealed class DualTemplateCenterLineStrategy : IAngleStrategy
{
    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        using var mat = VisionImageCv.AsMat(undistorted);
        return ComputeCore(mat, recipe, ct);
    }

    private static List<PixelPose> ComputeCore(Mat undistorted, RecipeConfig recipe, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var opt = recipe.DualTemplate;
        if (string.IsNullOrEmpty(opt.TemplateABase64) || string.IsNullOrEmpty(opt.TemplateBBase64))
            return [];

        var useSecondaryRoi = recipe.SecondarySearchRoi is not null;
        if (useSecondaryRoi && recipe.Roi is null)
            return [];

        using var templateA = MaskTemplateMatcher.DecodeTemplatePng(opt.TemplateABase64);
        using var templateB = MaskTemplateMatcher.DecodeTemplatePng(opt.TemplateBBase64);
        ct.ThrowIfCancellationRequested();

        var hitA = MatchInRegion(undistorted, recipe.Roi, templateA, opt, ct);
        if (hitA is null)
            return [];

        DualTemplateMatcher.Hit? hitB;
        if (useSecondaryRoi)
        {
            hitB = MatchInRegion(undistorted, recipe.SecondarySearchRoi, templateB, opt, ct);
        }
        else
        {
            var window = Expand(
                BoxOf(hitA.Value), opt.CropExpandRatio, undistorted.Width, undistorted.Height);
            hitB = MatchInRect(undistorted, window, templateB, opt, ct);
        }

        if (hitB is not { } b)
            return [];

        var a = hitA.Value;
        var distance = DualTemplateMatcher.Distance(a.Cx, a.Cy, b.Cx, b.Cy);
        if (distance < opt.MinPairDistancePx || distance > opt.MaxPairDistancePx)
            return [];

        var angleDeg = DualTemplateMatcher.LineAngleDeg(a.Cx, a.Cy, b.Cx, b.Cy);
        var score = Math.Min(a.Score, b.Score);
        return
        [
            new PixelPose(a.Cx, a.Cy, angleDeg, score)
            {
                Overlay = new PoseOverlay
                {
                    Boxes =
                    [
                        BoxAround(a),
                        BoxAround(b),
                    ],
                    Baseline =
                    [
                        new PixelPoint(a.Cx, a.Cy),
                        new PixelPoint(b.Cx, b.Cy),
                    ],
                },
            },
        ];
    }

    private static DualTemplateMatcher.Hit? MatchInRegion(
        Mat image, Roi? roi, Mat template, DualTemplateOptions opt, CancellationToken ct)
    {
        double ox = 0, oy = 0;
        using var roiOwned = roi is null ? null : RoiHelper.Crop(image, roi, out ox, out oy);
        var view = roiOwned ?? image;
        var hit = DualTemplateMatcher.Match(view, template, opt.RefineRangeDeg, opt.MatchThreshold);
        ct.ThrowIfCancellationRequested();
        return Offset(hit, ox, oy);
    }

    private static DualTemplateMatcher.Hit? MatchInRect(
        Mat image, Rect window, Mat template, DualTemplateOptions opt, CancellationToken ct)
    {
        if (window.Width <= 0 || window.Height <= 0)
            return null;
        using var view = new Mat(image, window);
        var hit = DualTemplateMatcher.Match(view, template, opt.RefineRangeDeg, opt.MatchThreshold);
        ct.ThrowIfCancellationRequested();
        return Offset(hit, window.X, window.Y);
    }

    private static DualTemplateMatcher.Hit? Offset(DualTemplateMatcher.Hit? hit, double ox, double oy) =>
        hit is { } h ? h with { Cx = h.Cx + ox, Cy = h.Cy + oy } : null;

    private static Rect BoxOf(DualTemplateMatcher.Hit hit) =>
        new(
            (int)Math.Round(hit.Cx - hit.TemplateWidth / 2.0),
            (int)Math.Round(hit.Cy - hit.TemplateHeight / 2.0),
            hit.TemplateWidth,
            hit.TemplateHeight);

    private static PixelRect BoxAround(DualTemplateMatcher.Hit hit) =>
        new(
            hit.Cx - hit.TemplateWidth / 2.0,
            hit.Cy - hit.TemplateHeight / 2.0,
            hit.TemplateWidth,
            hit.TemplateHeight);

    private static Rect Expand(Rect box, double ratio, int imageWidth, int imageHeight)
    {
        var dx = (int)Math.Round(box.Width * ratio);
        var dy = (int)Math.Round(box.Height * ratio);
        var expanded = new Rect(box.X - dx, box.Y - dy,
            box.Width + 2 * dx, box.Height + 2 * dy);
        return expanded & new Rect(0, 0, imageWidth, imageHeight);
    }
}
