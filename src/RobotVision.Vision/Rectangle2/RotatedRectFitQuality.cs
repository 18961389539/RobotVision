namespace RobotVision.Vision;

/// <summary>
/// 轮廓级质量含归一化 RMS（相对短边，对标 HALCON 相对残差）。
/// Score ∈ [0,1]，越高越可靠。
/// </summary>
public readonly record struct RotatedRectFitQuality(
    double Score,
    double RmsPx,
    int Inliers,
    double? MaxParallelDeg)
{
    /// <summary>轮廓级 <see cref="RotatedRectFitter"/> 结果。</summary>
    public static RotatedRectFitQuality FromContour(RotatedRectFitResult fit)
    {
        if (!fit.Ok)
            return new(0, double.NaN, 0, null);
        var rmsTerm = Math.Clamp(1.0 - fit.RmsPx / 2.5, 0, 1);
        var inlierTerm = Math.Clamp(fit.Inliers / 48.0, 0, 1);
        var nrms = NormalizedRms(fit);
        var nrmsTerm = double.IsFinite(nrms) ? Math.Clamp(1.0 - nrms / 0.08, 0, 1) : rmsTerm;
        var score = 0.45 * rmsTerm + 0.35 * inlierTerm + 0.20 * nrmsTerm;
        return new(score, fit.RmsPx, fit.Inliers, null);
    }

    /// <summary>亚像素四边 <see cref="RotatedRectSubpixel"/> 结果。</summary>
    internal static RotatedRectFitQuality FromSubpixel(RotatedRectSubpixel.Result sub) =>
        new(RotatedRectSubpixel.QualityScore(sub), sub.RmsPx, sub.Inliers, sub.MaxParallelDeg);

    /// <summary>可读摘要（日志/示教备注）。</summary>
    public string FormatNote(string prefix = "rectangle2")
    {
        if (!double.IsFinite(RmsPx))
            return $"{prefix}·质量不可用";
        return MaxParallelDeg is { } par
            ? $"{prefix}（平行差 {par:0.0}°，RMS {RmsPx:0.00}px，Q {Score:0.00}）"
            : $"{prefix}（RMS {RmsPx:0.00}px，Q {Score:0.00}）";
    }

    /// <summary>轮廓/亚像素 RMS 相对短边归一化（对标 HALCON 相对残差）。</summary>
    public static double NormalizedRms(RotatedRectFitResult fit) =>
        fit.Ok && fit.ShortLen > 1e-3 ? fit.RmsPx / fit.ShortLen : double.NaN;
}
