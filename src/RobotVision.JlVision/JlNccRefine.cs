using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>JlNCCModel + FindNccModel：示教灰度模板在分割框内局部精修（对照用）。</summary>
public static class JlNccRefine
{
    public static JlNCCModel CreateModel(Mat templateGray)
    {
        using var img = JlImageConvert.FromGrayMat(templateGray);
        return new JlNCCModel(img, "auto", -Math.PI, 2 * Math.PI, "auto", "use_polarity");
    }

    public static JlRefineHit TryRefine(
        JlImage scene,
        Point2f[] contour,
        JlNCCModel model,
        double rangeDeg,
        double minScore,
        JlFindOptions options = default) =>
        TryRefine(scene, contour, model, RefineAngleWindow.Symmetric(rangeDeg), minScore, options);

    public static JlRefineHit TryRefine(
        JlImage scene,
        Point2f[] contour,
        JlNCCModel model,
        RefineAngleWindow window,
        double minScore,
        JlFindOptions options = default)
    {
        return JlLocalSearch.MatchInBox(scene, contour, window, (img, start, extent) =>
        {
            img.FindNccModel(
                model,
                start,
                extent,
                minScore,
                1,
                0.5,
                "true",
                0,
                out var row,
                out var col,
                out var angle,
                out var score);
            try
            {
                if (!JlImageConvert.TryFirst(score, out var s) ||
                    !JlImageConvert.TryFirst(row, out var r) ||
                    !JlImageConvert.TryFirst(col, out var c) ||
                    !JlImageConvert.TryFirst(angle, out var a))
                    return (false, 0, 0, 0, 0);
                return (true, r, c, a, s);
            }
            finally
            {
                row.Dispose();
                col.Dispose();
                angle.Dispose();
                score.Dispose();
            }
        }, options);
    }
}
