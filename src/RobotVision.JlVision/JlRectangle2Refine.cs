using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.JlVision;

/// <summary>
/// FitRectangle2ContourXld：轮廓矩形，不跑 ApplyMetrologyModel（后者 P90 &gt; 1s）。
/// 形状与卡尺都失败时的 Goal C 快速兜底。
/// </summary>
public static class JlRectangle2Refine
{
    public static JlRefineHit TryRefine(Point2f[] contour)
    {
        if (contour.Length < 8)
            return JlRefineHit.Miss("轮廓点过少");

        var housing = JlHousing.Fit(contour);
        try
        {
            using var xld = JlMetrologyRefine.ContourToXld(contour);
            xld.FitRectangle2ContourXld(
                "tukey", -1, 0.0, 2, 3, 2.0,
                out double r, out double c, out double p, out double l1, out double l2, out string _);
            if (l1 < 4 || l2 < 4)
                return JlRefineHit.Miss("矩形过小");

            var angle = AngleGeometry.FuseDirected(
                JlImageConvert.PhiToDeg(p),
                housing.WarpAngleDeg);
            return JlLocalSearch.SnapUpright(
                new JlRefineHit(true, c, r, angle, 0.55, $"l1={l1:0.0};l2={l2:0.0}"),
                JlFindOptions.ProductDefault);
        }
        catch (JlException ex)
        {
            return JlRefineHit.Miss(ex.Message);
        }
    }
}
