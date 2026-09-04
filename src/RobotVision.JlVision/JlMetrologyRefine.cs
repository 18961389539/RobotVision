using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.JlVision;

/// <summary>
/// FitRectangle2ContourXld 给粗矩形，再 JlMetrologyModel 四边卡尺亚像素拟合。
/// 输出无向角，与壳体粗角融合得到有向表示（头尾仍可能差 180°）。
/// </summary>
public static class JlMetrologyRefine
{
    public static JlRefineHit TryRefine(JlImage scene, Point2f[] contour)
    {
        if (contour.Length < 8)
            return JlRefineHit.Miss("轮廓点过少");

        var housing = JlHousing.Fit(contour);
        try
        {
            using var xld = ContourToXld(contour);
            xld.FitRectangle2ContourXld(
                "tukey", -1, 0.0, 2, 3, 2.0,
                out double r, out double c, out double p, out double l1, out double l2, out string _);
            var (domain, local) = JlLocalSearch.ReduceToContour(scene, contour);
            using (domain)
            using (local)
            {
                scene.GetImageSize(out int width, out int height);
                using var metro = new JlMetrologyModel();
                metro.SetMetrologyModelImageSize(width, height);
                var search = Math.Max(12.0, Math.Min(l1, l2) * 0.35);
                using var empty = new JlTuple();
                var idx = metro.AddMetrologyObjectRectangle2Measure(
                    r, c, p, l1, l2,
                    search, 5.0, 1.0, 20.0,
                    empty, empty);
                metro.ApplyMetrologyModel(local);
                var n = metro.GetMetrologyObjectNumInstances(idx);
                if (n < 1)
                    return JlRefineHit.Miss("计量无实例");

                using var names = new JlTuple("result_type");
                using var vals = new JlTuple("all_param");
                using var result = metro.GetMetrologyObjectResult(idx, "all", names, vals);
                var arr = result.ToDArr();
                if (arr.Length < 5)
                    return JlRefineHit.Miss($"计量参数长度 {arr.Length}");

                var angle = AngleGeometry.FuseDirected(
                    JlImageConvert.PhiToDeg(arr[2]),
                    housing.WarpAngleDeg);
                return JlLocalSearch.SnapUpright(
                    new JlRefineHit(true, arr[1], arr[0], angle, 1.0, $"metro n={n:0}"),
                    JlFindOptions.ProductDefault);
            }
        }
        catch (JlException ex)
        {
            return JlRefineHit.Miss(ex.Message);
        }
    }

    internal static JlXLDCont ContourToXld(Point2f[] contour)
    {
        var n = contour.Length;
        var rows = new double[n + 1];
        var cols = new double[n + 1];
        for (var i = 0; i < n; i++)
        {
            rows[i] = contour[i].Y;
            cols[i] = contour[i].X;
        }

        rows[n] = rows[0];
        cols[n] = cols[0];
        return new JlXLDCont(rows, cols);
    }
}
