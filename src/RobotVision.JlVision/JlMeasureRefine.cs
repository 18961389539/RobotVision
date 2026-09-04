using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>沿壳体两条长边布 JlMeasure 卡尺，FitLineContourXld 拟合，均角为精修角。</summary>
public static class JlMeasureRefine
{
    public static JlRefineHit TryRefine(JlImage scene, Point2f[] contour, HousingEdgePolarity polarity)
    {
        if (contour.Length < 8)
            return JlRefineHit.Miss("轮廓点过少");

        var housing = JlHousing.Fit(contour);
        scene.GetImageSize(out int width, out int height);
        var rad = housing.WarpAngleDeg * Math.PI / 180.0;
        var alongX = Math.Cos(rad);
        var alongY = Math.Sin(rad);
        var acrossX = -alongY;
        var acrossY = alongX;
        var probes = JlHousing.ProbeCount(housing.LongLen);
        var inset = JlHousing.EndInsetRatio(housing.LongLen, housing.ShortLen);
        var search = Math.Max(10.0, housing.ShortLen * 0.35);
        var halfAvg = 2.5;
        var phi = Math.Atan2(acrossY, acrossX);
        var transition = polarity switch
        {
            HousingEdgePolarity.BrightToDark => "negative",
            HousingEdgePolarity.DarkToBright => "positive",
            _ => "all",
        };

        try
        {
            var angles = new List<double>(2);
            var mids = new List<Point2d>(2);
            foreach (var side in new[] { -1.0, 1.0 })
            {
                var rows = new List<double>(probes);
                var cols = new List<double>(probes);
                for (var i = 0; i < probes; i++)
                {
                    var t = (i + 0.5) / probes;
                    var u = (t - 0.5) * (1.0 - 2 * inset) * housing.LongLen;
                    var cx = housing.Center.X + alongX * u + acrossX * side * (housing.ShortLen * 0.5);
                    var cy = housing.Center.Y + alongY * u + acrossY * side * (housing.ShortLen * 0.5);
                    using var cal = new JlMeasure(cy, cx, phi, search, halfAvg, width, height, "nearest_neighbor");
                    cal.MeasurePos(scene, 1.0, 12.0, transition, "all",
                        out var er, out var ec, out var amp, out var dist);
                    try
                    {
                        if (er.Length < 1)
                            continue;
                        var best = 0;
                        var bestAmp = 0.0;
                        for (var k = 0; k < amp.Length; k++)
                        {
                            var a = Math.Abs(amp[k].D);
                            if (a > bestAmp)
                            {
                                bestAmp = a;
                                best = k;
                            }
                        }

                        if (bestAmp < 8)
                            continue;
                        rows.Add(er[best].D);
                        cols.Add(ec[best].D);
                    }
                    finally
                    {
                        er.Dispose();
                        ec.Dispose();
                        amp.Dispose();
                        dist.Dispose();
                    }
                }

                if (rows.Count < 4)
                    continue;

                using var line = new JlXLDCont(rows.ToArray(), cols.ToArray());
                line.FitLineContourXld(
                    "tukey", -1, 0, 5, 2.0,
                    out double r0, out double c0, out double r1, out double c1,
                    out double _, out double _, out double _);
                var dx = c1 - c0;
                var dy = r1 - r0;
                if (dx * alongX + dy * alongY < 0)
                {
                    dx = -dx;
                    dy = -dy;
                }

                var ang = AngleGeometry.NormalizeSignedDeg(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                angles.Add(ang);
                mids.Add(new Point2d((c0 + c1) / 2.0, (r0 + r1) / 2.0));
            }

            if (angles.Count < 2)
                return JlRefineHit.Miss($"卡尺边不足 {angles.Count}");

            var a0 = AngleGeometry.FuseDirected(angles[0], housing.WarpAngleDeg);
            var a1 = AngleGeometry.FuseDirected(angles[1], housing.WarpAngleDeg);
            var mean = AngleGeometry.NormalizeSignedDeg(0.5 * (a0 + a1));
            var cxOut = (mids[0].X + mids[1].X) / 2.0;
            var cyOut = (mids[0].Y + mids[1].Y) / 2.0;
            var par = AngleGeometry.UndirectedDeltaDeg(angles[0], angles[1]);
            if (par > 4.0)
                return JlRefineHit.Miss($"平行差 {par:0.00}°");
            return JlLocalSearch.SnapUpright(
                new JlRefineHit(true, cxOut, cyOut, mean, Math.Clamp(1.0 - par / 4.0, 0.2, 1),
                    $"par={par:0.00}°"),
                JlFindOptions.ProductDefault);
        }
        catch (JlException ex)
        {
            return JlRefineHit.Miss(ex.Message);
        }
    }
}
