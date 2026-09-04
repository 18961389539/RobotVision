using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>
/// 形状匹配失败后走 JlMeasure 长边卡尺。
/// FitRectangle2 未在 Dev 上单独过门，不进 TRIGGER；全量 Metrology.Apply 的 P90 超过 180ms。
/// </summary>
public static class JlGeometryFallback
{
    public static JlRefineHit TryRefine(JlImage scene, Point2f[] contour, HousingEdgePolarity polarity)
    {
        var measure = JlMeasureRefine.TryRefine(scene, contour, polarity);
        if (Accept(measure))
            return measure with { Note = "fallback-measure;" + measure.Note };
        return measure;
    }

    private static bool Accept(JlRefineHit hit) =>
        hit.Found && Math.Abs(AngleGeometry.NormalizeSignedDeg(hit.AngleDeg)) <= 150.0;
}
