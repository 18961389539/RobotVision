using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>在分割粗框外扩域内、粗角 [lo,hi] 与 180° 支上做局部搜索。</summary>
internal static class JlLocalSearch
{
    public static JlRefineHit MatchInBox(
        JlImage scene,
        Point2f[] contour,
        double rangeDeg,
        Func<JlImage, double, double, (bool Ok, double Row, double Col, double AngleRad, double Score)> find,
        JlFindOptions options = default) =>
        MatchInBox(scene, contour, RefineAngleWindow.Symmetric(rangeDeg), find, options);

    public static JlRefineHit MatchInBox(
        JlImage scene,
        Point2f[] contour,
        RefineAngleWindow window,
        Func<JlImage, double, double, (bool Ok, double Row, double Col, double AngleRad, double Score)> find,
        JlFindOptions options = default)
    {
        var housing = JlHousing.Fit(contour);
        var box = Cv2.BoundingRect(contour);
        var padX = Math.Max(12, (int)(box.Width * 0.15));
        var padY = Math.Max(12, (int)(box.Height * 0.15));
        var col1 = Math.Max(0, box.X - padX);
        var row1 = Math.Max(0, box.Y - padY);
        var col2 = box.X + box.Width + padX;
        var row2 = box.Y + box.Height + padY;

        using var domain = new JlRegion((double)row1, (double)col1, (double)row2, (double)col2);
        using var local = scene.ReduceDomain(domain);

        var zero = Try(local, JlImageConvert.DegToPhi(housing.WarpAngleDeg), window, find, "0");
        JlRefineHit flip = default;
        var hasFlip = false;
        if (!options.NoFlip)
        {
            flip = Try(local, JlImageConvert.DegToPhi(housing.WarpAngleDeg) + Math.PI, window, find, "180");
            hasFlip = flip.Found;
        }

        return SnapUpright(Pick(zero, hasFlip ? flip : default, hasFlip, options), options);
    }

    internal static (JlRegion Domain, JlImage Local) ReduceToContour(JlImage scene, Point2f[] contour)
    {
        var box = Cv2.BoundingRect(contour);
        var padX = Math.Max(12, (int)(box.Width * 0.15));
        var padY = Math.Max(12, (int)(box.Height * 0.15));
        var col1 = Math.Max(0, box.X - padX);
        var row1 = Math.Max(0, box.Y - padY);
        var col2 = box.X + box.Width + padX;
        var row2 = box.Y + box.Height + padY;
        var domain = new JlRegion((double)row1, (double)col1, (double)row2, (double)col2);
        return (domain, scene.ReduceDomain(domain));
    }

    internal static JlRefineHit SnapUpright(JlRefineHit hit, JlFindOptions options)
    {
        if (!options.PreferUpright || !hit.Found)
            return hit;
        var a = AngleGeometry.NormalizeSignedDeg(hit.AngleDeg);
        if (Math.Abs(a) <= 90.0)
            return hit;
        return hit with
        {
            AngleDeg = AngleGeometry.NormalizeSignedDeg(a + 180.0),
            Note = hit.Note + ";upright180",
        };
    }

    private static JlRefineHit Pick(JlRefineHit zero, JlRefineHit flip, bool hasFlip, JlFindOptions options)
    {
        if (zero.Found && hasFlip && flip.Found)
        {
            if (options.PreferUpright)
            {
                var z = Math.Abs(AngleGeometry.NormalizeSignedDeg(zero.AngleDeg));
                var f = Math.Abs(AngleGeometry.NormalizeSignedDeg(flip.AngleDeg));
                return z <= f ? zero : flip;
            }

            var margin = options.FlipScoreMargin > 0 ? options.FlipScoreMargin : 0.08;
            return flip.Score >= zero.Score + margin ? flip : zero;
        }

        if (zero.Found)
            return zero;
        if (hasFlip && flip.Found)
            return flip;
        if (!string.IsNullOrEmpty(zero.Note) && zero.Note != "无匹配")
            return zero;
        if (hasFlip && !string.IsNullOrEmpty(flip.Note))
            return flip;
        return JlRefineHit.Miss("无匹配");
    }

    private static JlRefineHit Try(
        JlImage local,
        double origin,
        RefineAngleWindow window,
        Func<JlImage, double, double, (bool Ok, double Row, double Col, double AngleRad, double Score)> find,
        string branch)
    {
        try
        {
            var loRad = window.LoDeg * Math.PI / 180.0;
            var spanRad = Math.Max(0.5 * Math.PI / 180.0, window.SpanDeg * Math.PI / 180.0);
            var hit = find(local, origin + loRad, spanRad);
            if (!hit.Ok)
                return JlRefineHit.Miss("无匹配");
            return new JlRefineHit(
                true,
                hit.Col,
                hit.Row,
                JlImageConvert.PhiToDeg(hit.AngleRad),
                hit.Score,
                $"score={hit.Score:0.000};br={branch}");
        }
        catch (JlException ex)
        {
            return JlRefineHit.Miss(ex.Message);
        }
    }
}
