using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>JlShapeModel + FindShapeModel：示教模板在分割框内局部精修。</summary>
public static class JlShapeRefine
{
    public static JlShapeModel CreateModel(Mat templateGray, TemplateOptions? options = null)
    {
        using var img = JlImageConvert.FromGrayMat(templateGray);
        var metric = options?.ShapeMatchMetric switch
        {
            ShapeMatchMetric.IgnoreLocalPolarity => "ignore_local_polarity",
            ShapeMatchMetric.IgnoreGlobalPolarity => "ignore_global_polarity",
            _ => "use_polarity",
        };
        var minContrast = options is { ShapeMatchMinContrast: > 0 }
            ? options.ShapeMatchMinContrast.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : "auto";
        return new JlShapeModel(
            img,
            "auto",
            -Math.PI,
            2 * Math.PI,
            "auto",
            "auto",
            metric,
            "auto",
            minContrast);
    }

    public static JlRefineHit TryRefine(
        JlImage scene,
        Point2f[] contour,
        JlShapeModel model,
        double rangeDeg,
        double minScore,
        JlFindOptions options = default) =>
        TryRefine(scene, contour, model, RefineAngleWindow.Symmetric(rangeDeg), minScore, options);

    public static JlRefineHit TryRefine(
        JlImage scene,
        Point2f[] contour,
        JlShapeModel model,
        RefineAngleWindow window,
        double minScore,
        JlFindOptions options = default)
    {
        var greediness = options.Greediness > 0 ? options.Greediness : 0.9;
        var numLevels = options.NumLevels;
        return JlLocalSearch.MatchInBox(scene, contour, window, (img, start, extent) =>
        {
            img.FindShapeModel(
                model,
                start,
                extent,
                minScore,
                1,
                0.5,
                "least_squares",
                numLevels,
                greediness,
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
