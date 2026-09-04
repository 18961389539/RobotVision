using OpenCvSharp;

namespace RobotVision.Vision;

/// <summary>模板匹配结果：匹配分数、相对转正模板的旋转角（度）、匹配中心在转正图坐标系的位置。</summary>
public sealed record MaskTemplateMatchResult(double Score, double RotationDeg, Point2d CenterInUpright);

/// <summary>
/// 转正裁剪结果。匹配中心在 <see cref="Upright"/> 坐标系，映射回原图须加裁剪原点后再
/// 用与 WarpAffine 相同的 <see cref="WarpAngleDeg"/> 做逆变换——不能对裁剪坐标直接 Invert。
/// </summary>
public sealed record UprightCropResult(
    Mat Upright,
    double WarpAngleDeg,
    Point2f RotationCenter,
    double CropOriginX,
    double CropOriginY);

/// <summary>
/// 分割+模板匹配（MaskTemplate 模式）的共享几何/匹配辅助：
/// 策略（运行时精修）与配方页「示教模板」（转正裁剪生成模板）共用同一套变换，
/// 保证示教坐标系与运行时坐标系完全一致。
/// 约定：所有角度遵循 AngleGeometry（y 轴向下，度，逆时针为正）。
/// </summary>
public static partial class MaskTemplateMatcher
{
    internal readonly record struct MatchOrientationDebug(
        double Score0, double Score180, int? TemplateSign, int? SceneSign, double PeakDistPx,
        double PeakSharpness = 0,
        // 失败归因（minScore 不过 / 无候选时写入，供调用方拼接可读诊断）：
        double BestScore = double.NaN, double MinScore = double.NaN, double BestDeg = double.NaN,
        double SecondPeakRatio = double.NaN);

    [ThreadStatic]
    internal static MatchOrientationDebug LastDebug;

    public sealed record CentroidHoleResult(double AngleDeg, Point2d Centroid, double Quality = 0.85);
}
