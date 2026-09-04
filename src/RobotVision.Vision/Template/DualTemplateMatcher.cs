using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Vision;

/// <summary>
/// 双模板连线：在搜索图内对单张示教模板做带旋转 NCC，返回匹配中心（搜索图坐标）与分数。
/// 位姿角由策略用两次匹配中心连线给出，本类不管头尾。
/// </summary>
public static class DualTemplateMatcher
{
    public readonly record struct Hit(double Cx, double Cy, double Score, int TemplateWidth, int TemplateHeight);

    /// <summary>
    /// 在 <paramref name="search"/> 内匹配 <paramref name="template"/>。
    /// 模板任一边大于搜索图时返回 null。
    /// </summary>
    public static Hit? Match(Mat search, Mat template, double rangeDeg, double minScore)
    {
        if (search.Empty() || template.Empty())
            return null;

        using var searchGray = ToGray(search);
        using var templateGray = ToGray(template);
        if (templateGray.Width > searchGray.Width || templateGray.Height > searchGray.Height)
            return null;

        var result = MaskTemplateMatcher.MatchBest(
            searchGray, templateGray, rangeDeg, minScore,
            useMatchedPolarity: false);
        if (result is null)
            return null;

        return new Hit(
            result.CenterInUpright.X,
            result.CenterInUpright.Y,
            result.Score,
            templateGray.Width,
            templateGray.Height);
    }

    public static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double LineAngleDeg(double ax, double ay, double bx, double by) =>
        AngleGeometry.FromTwoPoints(ax, ay, bx, by).AngleDeg;

    private static Mat ToGray(Mat image)
    {
        var gray = image.Channels() switch
        {
            1 => image.Clone(),
            3 => new Mat(),
            4 => new Mat(),
            _ => throw new ArgumentException($"不支持的图像通道数: {image.Channels()}", nameof(image)),
        };
        if (image.Channels() == 3)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else if (image.Channels() == 4)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
        if (gray.Depth() == MatType.CV_8U)
            return gray;

        var normalized = new Mat();
        Cv2.Normalize(gray, normalized, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        gray.Dispose();
        return normalized;
    }
}
