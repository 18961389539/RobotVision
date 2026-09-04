using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.JlVision;

namespace RobotVision.Teach;

/// <summary>
/// ScenePlaybook —— 极性推断：亮/暗场各跑一次 JL 卡尺，取分数高者的边缘极性。
/// </summary>
public static partial class ScenePlaybook
{
    public static (HousingEdgePolarity Edge, TabPolarityLock Tab) InferPolarity(
        Mat bgr, IReadOnlyList<Point2f> contour)
    {
        var pts = contour as Point2f[] ?? [.. contour];
        try
        {
            using var scene = JlImageConvert.FromGrayMat(bgr);
            var bright = JlMeasureRefine.TryRefine(scene, pts, HousingEdgePolarity.BrightToDark);
            var dark = JlMeasureRefine.TryRefine(scene, pts, HousingEdgePolarity.DarkToBright);
            if (bright.Found && (!dark.Found || bright.Score >= dark.Score))
                return (HousingEdgePolarity.BrightToDark, TabPolarityLock.Auto);
            if (dark.Found)
                return (HousingEdgePolarity.DarkToBright, TabPolarityLock.Auto);
        }
        catch
        {
        }

        return (HousingEdgePolarity.Auto, TabPolarityLock.Auto);
    }
}
