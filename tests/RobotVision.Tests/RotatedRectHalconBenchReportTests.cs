using System.Globalization;
using OpenCvSharp;
using RobotVision.Tests.HalconBench;
using RobotVision.Vision;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// HALCON rectangle2 全矩阵对标报告：合成 ground truth 上输出 CSV 行并断言门槛。
/// </summary>
[Trait("Category", "Bench")]
public sealed class RotatedRectHalconBenchReportTests(ITestOutputHelper output)
{
    [Fact]
    public void Bench_halcon_parity_full_matrix_report()
    {
        var rows = new List<string>
        {
            "scenario,angle_deg,contour_theta_err,full_theta_err,center_err_px,contour_rms,full_rms,quality,oracle_theta_err,oracle_center_err",
        };

        foreach (var fx in RotatedRectHalconFixtureCatalog.All())
        {
            using var gray = fx.CreateImage();
            var contour = fx.CreateContour();
            AssertRow(fx.Scenario, fx.TrueDeg, contour, gray, fx.Options, rows);
        }

        output.WriteLine("HALCON rectangle2 合成对标矩阵（CSV）");
        foreach (var row in rows)
            output.WriteLine(row);
        Assert.True(rows.Count > 7);
    }

    [Fact]
    public void Bench_halcon_contour_halcon_clip0_profile_gates()
    {
        RotatedRectHalconBenchIo.AssertHalconClip0ContourGates(output.WriteLine);
        RotatedRectHalconBenchIo.AssertHalconClip0FullGates(output.WriteLine);
    }

    [Fact]
    public void Bench_standard_subpixel_refines_contour_long_length()
    {
        foreach (var deg in new[] { -18.0, 0.0, 22.0, 45.0, 88.0, 135.0 })
        {
            var fx = RotatedRectHalconFixtureCatalog.All().First(f => f.Id == $"standard_{deg:0}");
            using var gray = fx.CreateImage();
            var contour = fx.CreateContour();
            var truth = RotatedRectSyntheticOracle.From(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
                deg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
            var contourFit = RotatedRectPipeline.FitContour(contour, deg, fx.Options);
            var full = RotatedRectPipeline.Fit(contour, gray, deg, fx.Options);
            Assert.True(contourFit.Ok && full.Ok);

            var cLong = RotatedRectSyntheticOracle.Compare(contourFit, truth).LongPx;
            var fLong = RotatedRectSyntheticOracle.Compare(full, truth).LongPx;
            output.WriteLine($"standard_{deg:0}: contour ΔL={cLong:0.000}px full ΔL={fLong:0.000}px");
            if (deg == 0.0)
                Assert.True(cLong < 0.5, $"contour 长边 @ 0° ΔL={cLong:0.000}px");
            if (deg == 135.0)
                Assert.True(cLong < 0.5, $"contour 长边 @ 135° ΔL={cLong:0.000}px");
            if (deg == -18.0)
                Assert.True(cLong < RotatedRectHalconBenchGates.TruthContourLongP90,
                    $"contour 长边 @ -18° ΔL={cLong:0.000}px（HALCON 级 ≤{RotatedRectHalconBenchGates.TruthContourLongP90}）");
            Assert.True(fLong < RotatedRectHalconBenchGates.TruthFullLongP90,
                $"full 长边 @ {deg}° ΔL={fLong:0.000}px");
            Assert.True(fLong <= cLong + 0.05,
                $"亚像素应不长于轮廓 @ {deg}° contour={cLong:0.000} full={fLong:0.000}");
        }
    }

    [Fact]
    public void Bench_high_jitter_with_seed_rejects_wrong_branch()
    {
        var contour = RotatedRectBenchSynth.RectContour(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, -18.0,
            RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter: 1.0);
        var fit = RotatedRectFitter.Fit(contour, -18.0, new RectFitOptions { StripTabProtrusion = false });
        Assert.True(fit.Ok);
        var err = UndirectedErr(fit.AngleDeg, -18.0);
        output.WriteLine($"高 jitter + 种子 角误差={err:0.000}° RMS={fit.RmsPx:0.00}");
        Assert.True(err < RotatedRectHalconBenchGates.HighJitterContourAngleDeg);
    }

    private static void AssertRow(
        string scenario, double trueDeg, Point2f[] contour, Mat gray, RectFitOptions opt, List<string> rows)
    {
        var contourFit = RotatedRectPipeline.FitContour(contour, trueDeg, opt);
        var full = RotatedRectPipeline.Fit(contour, gray, trueDeg, opt);
        Assert.True(contourFit.Ok && full.Ok);

        var cErr = UndirectedErr(contourFit.AngleDeg, trueDeg);
        var fErr = UndirectedErr(full.AngleDeg, trueDeg);
        var centerErr = Math.Sqrt(
            Math.Pow(full.Center.X - RotatedRectBenchSynth.Cx, 2) +
            Math.Pow(full.Center.Y - RotatedRectBenchSynth.Cy, 2));
        var q = RotatedRectPipeline.QualityScore(full);
        var truth = RotatedRectSyntheticOracle.From(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy,
            trueDeg, RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short);
        var oracle = RotatedRectSyntheticOracle.Compare(full, truth);

        rows.Add(string.Join(',',
            scenario,
            trueDeg.ToString("0", CultureInfo.InvariantCulture),
            cErr.ToString("0.000", CultureInfo.InvariantCulture),
            fErr.ToString("0.000", CultureInfo.InvariantCulture),
            centerErr.ToString("0.00", CultureInfo.InvariantCulture),
            contourFit.RmsPx.ToString("0.00", CultureInfo.InvariantCulture),
            full.RmsPx.ToString("0.00", CultureInfo.InvariantCulture),
            q.ToString("0.00", CultureInfo.InvariantCulture),
            oracle.AngleDeg.ToString("0.000", CultureInfo.InvariantCulture),
            oracle.CenterPx.ToString("0.00", CultureInfo.InvariantCulture)));

        Assert.True(cErr < RotatedRectHalconBenchGates.ContourAngleDeg, $"轮廓 @ {trueDeg}°");
        Assert.True(fErr < RotatedRectHalconBenchGates.FullAngleDeg, $"全链路 @ {trueDeg}°");
        Assert.True(centerErr < RotatedRectHalconBenchGates.CenterPx);
        Assert.True(full.RmsPx <= contourFit.RmsPx + RotatedRectHalconBenchGates.RmsSlackPx);
        Assert.True(q > RotatedRectHalconBenchGates.QualityMin);
        RotatedRectSyntheticOracle.AssertHalconGrade(oracle, $"{scenario}@{trueDeg}°");
    }

    private static double UndirectedErr(double got, double truth)
    {
        var d = Math.Abs(got - truth);
        return Math.Min(d, 180 - d);
    }
}
