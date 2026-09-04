using OpenCvSharp;
using RobotVision.Vision;

namespace RobotVision.Vision;

/// <summary>
/// rectangle2 对外统一入口：轮廓鲁棒拟合 → 可选原图亚像素 → 质量评估。
/// 底层 <see cref="RotatedRectFitter"/> / <see cref="RotatedRectSubpixel"/> 为程序集内实现；
/// LineFit 业务精修见 <see cref="LineFitRefine"/>。
/// </summary>
public static class RotatedRectPipeline
{
    /// <summary>仅轮廓级拟合（对标 fit_rectangle2_contour_xld）。</summary>
    public static RotatedRectFitResult FitContour(
        IReadOnlyList<Point2f> contour,
        double? seedAngleDeg = null,
        RectFitOptions? options = null)
    {
        options ??= RectFitOptions.Default;
        var core = options.StripTabProtrusion ? MaskHousing.CorePoints(contour) : contour;
        return RotatedRectFitter.Fit(core, seedAngleDeg, options);
    }

    /// <summary>轮廓 + 原图亚像素（对标 fit_rectangle2 + measure_pairs）。</summary>
    public static RotatedRectFitResult Fit(
        IReadOnlyList<Point2f> contour,
        Mat gray,
        double? seedAngleDeg = null,
        RectFitOptions? options = null) =>
        RotatedRectSubpixel.RefineFromContour(gray, contour, seedAngleDeg, options);

    /// <summary>质量分 [0,1]（亚像素结果可用时取亚像素，否则轮廓级 RMS 映射）。</summary>
    public static double QualityScore(RotatedRectFitResult fit) =>
        EvaluateQuality(fit).Score;

    /// <summary>统一质量评估：有亚像素平行差时由调用方传入 <paramref name="maxParallelDeg"/>。</summary>
    public static RotatedRectFitQuality EvaluateQuality(RotatedRectFitResult fit, double? maxParallelDeg = null)
    {
        var baseQ = RotatedRectFitQuality.FromContour(fit);
        if (maxParallelDeg is not { } par || !fit.Ok)
            return baseQ;
        var angleTerm = Math.Clamp(1.0 - par / 3.5, 0, 1);
        var rmsTerm = Math.Clamp(1.0 - fit.RmsPx / 2.0, 0, 1);
        var inlierTerm = Math.Clamp(fit.Inliers / 48.0, 0, 1);
        var nrms = RotatedRectFitQuality.NormalizedRms(fit);
        var nrmsTerm = double.IsFinite(nrms) ? Math.Clamp(1.0 - nrms / 0.08, 0, 1) : rmsTerm;
        var score = 0.35 * angleTerm + 0.30 * rmsTerm + 0.20 * inlierTerm + 0.15 * nrmsTerm;
        return baseQ with { Score = score, MaxParallelDeg = par };
    }
}
