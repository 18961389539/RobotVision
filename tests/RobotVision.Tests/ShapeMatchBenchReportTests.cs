using System.Globalization;
using OpenCvSharp;
using RobotVision.Tests.HalconBench;
using RobotVision.Vision;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>形状匹配合成矩阵对标（ground truth 角度），追踪向 HALCON 靠拢的进度。</summary>
[Trait("Category", "Bench")]
public sealed class ShapeMatchBenchReportTests(ITestOutputHelper output)
{
    private static readonly double[] MatrixAngles =
        [-37, -20, -8.7, 0, 8.7, 20, 37, 180];

    [Fact]
    public void Bench_shape_match_parity_matrix_report()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachContour = ShapeMatchBenchSynth.Contour(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, teachContour);
        Assert.NotNull(model);

        var rows = new List<string> { "true_deg,ok,angle_err_deg,hit_rate,mean_dist_px" };
        var errs = new List<double>();
        foreach (var deg in MatrixAngles)
        {
            using var img = ShapeMatchBenchSynth.Paint(deg);
            var contour = ShapeMatchBenchSynth.Contour(deg);
            var noFlip = Math.Abs(Math.Abs(deg) - 180.0) > 1.0;
            var r = ShapeMatchBenchSynth.RefineAngleErr(img, contour, model!, deg, refineRangeDeg: 8, noFlip: noFlip);
            rows.Add(string.Join(',',
                deg.ToString("0.######", CultureInfo.InvariantCulture),
                r.Ok ? "1" : "0",
                r.AngleErrDeg.ToString("0.######", CultureInfo.InvariantCulture),
                r.HitRate.ToString("0.######", CultureInfo.InvariantCulture),
                r.MeanDistPx.ToString("0.######", CultureInfo.InvariantCulture)));
            output.WriteLine($"shape {deg,6:0.0}° err={r.AngleErrDeg:0.00}° hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00} score={r.Score:0.00}");
            Assert.True(r.Ok, $"deg={deg} 未过门 hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00}");
            var gate = deg is -37 or -20
                ? ShapeMatchBenchGates.LargeNegativeAngleMaxDeg
                : ShapeMatchBenchGates.AngleMaxDeg;
            Assert.True(r.AngleErrDeg < gate, $"deg={deg} 角误差 {r.AngleErrDeg:0.00}° > {gate}");
            Assert.True(r.HitRate >= ShapeMatchBenchGates.OverlayMinHitRate,
                $"deg={deg} 命中 {r.HitRate:0.00} < {ShapeMatchBenchGates.OverlayMinHitRate}");
            Assert.True(r.MeanDistPx <= ShapeMatchBenchGates.OverlayMaxMeanDistPx,
                $"deg={deg} 均距 {r.MeanDistPx:0.00}px > {ShapeMatchBenchGates.OverlayMaxMeanDistPx}");
            if (Math.Abs(deg) >= 20 && Math.Abs(Math.Abs(deg) - 180.0) > 1.0)
                Assert.True(r.Score >= ShapeMatchBenchGates.LargeWarpMinScore,
                    $"deg={deg} 大角质量分 {r.Score:0.00} < {ShapeMatchBenchGates.LargeWarpMinScore}");
            errs.Add(r.AngleErrDeg);
        }

        errs.Sort();
        var p90 = errs[(int)Math.Ceiling(errs.Count * 0.9) - 1];
        output.WriteLine($"P90 angle err={p90:0.00}° (gate {ShapeMatchBenchGates.AngleP90Deg})");
        Assert.True(p90 <= ShapeMatchBenchGates.AngleP90Deg + 1e-6, $"P90 角误差 {p90:0.000}° > {ShapeMatchBenchGates.AngleP90Deg}");
        Assert.True(rows.Count > MatrixAngles.Length);
    }

    [Fact]
    public void Bench_shape_match_center_p90_report()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachContour = ShapeMatchBenchSynth.Contour(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, teachContour);
        Assert.NotNull(model);
        var teachOrigin = ShapeMatchBenchSynth.TeachOriginSource(teachImg, teachContour, model!);

        var originErrs = new List<double>();
        foreach (var deg in MatrixAngles)
        {
            using var img = ShapeMatchBenchSynth.Paint(deg);
            var contour = ShapeMatchBenchSynth.Contour(deg);
            var noFlip = Math.Abs(Math.Abs(deg) - 180.0) > 1.0;
            var expected = ShapeMatchBenchSynth.RotateAroundPaint(teachOrigin, deg);
            var attempt = MaskShapeMatch.TryRefine(img, contour, model!, refineRangeDeg: 8, noFlip, ShapeMatchBenchSynth.BenchOptions);
            var err = attempt.Pose is { } pose
                ? Math.Sqrt(
                    (pose.Center.X - expected.X) * (pose.Center.X - expected.X)
                    + (pose.Center.Y - expected.Y) * (pose.Center.Y - expected.Y))
                : double.PositiveInfinity;
            output.WriteLine(
                $"center deg={deg,6:0.0} originErr={err:0.000}px residual={MaskShapeMatch.LastDebug.ResidualDeg:0.000}° hit={MaskShapeMatch.LastDebug.HitRate:0.00}");
            originErrs.Add(err);
        }

        originErrs.Sort();
        var p90 = originErrs[(int)Math.Ceiling(originErrs.Count * 0.9) - 1];
        output.WriteLine($"P90 center vs teach-origin={p90:0.000}px (spec {ShapeMatchBenchGates.SpecCenterPx})");
        Assert.True(p90 < ShapeMatchBenchGates.SpecCenterPx, $"P90 相对示教原点 {p90:0.000}px > {ShapeMatchBenchGates.SpecCenterPx}");
    }

    [Fact]
    public void Bench_shape_match_latency_p90_under_spec()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, ShapeMatchBenchSynth.Contour(0));
        Assert.NotNull(model);

        using var img = ShapeMatchBenchSynth.Paint(-20);
        var contour = ShapeMatchBenchSynth.Contour(-20);
        for (var w = 0; w < 3; w++)
            ShapeMatchBenchSynth.RefineAngleErr(img, contour, model, -20, refineRangeDeg: 8, noFlip: true);

        var samples = new List<double>();
        for (var i = 0; i < 12; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ShapeMatchBenchSynth.RefineAngleErr(img, contour, model, -20, refineRangeDeg: 8, noFlip: true);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var p90 = samples[(int)Math.Ceiling(samples.Count * 0.9) - 1];
        Assert.True(p90 < ShapeMatchBenchGates.SpecLatencyMs,
            $"P90 耗时 {p90:0.0}ms > 规格 {ShapeMatchBenchGates.SpecLatencyMs}ms");
        output.WriteLine($"latency P90={p90:0.0}ms (spec {ShapeMatchBenchGates.SpecLatencyMs}) samples=[{string.Join(',', samples.Select(s => s.ToString("0.0", CultureInfo.InvariantCulture)))}]");
    }

    [Theory]
    [InlineData(37, 1.0)]
    [InlineData(37, 0.97)]
    [InlineData(-20, 0.97)]
    public void Bench_shape_match_latency_large_warp_and_shrink(double deg, double scale)
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, ShapeMatchBenchSynth.Contour(0));
        Assert.NotNull(model);
        using var img = ShapeMatchBenchSynth.Paint(deg, scale: scale);
        var contour = ShapeMatchBenchSynth.Contour(deg, scale: scale);
        for (var w = 0; w < 2; w++)
            ShapeMatchBenchSynth.RefineAngleErr(img, contour, model, deg, refineRangeDeg: 8, noFlip: true);

        var samples = new List<double>();
        for (var i = 0; i < 8; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ShapeMatchBenchSynth.RefineAngleErr(img, contour, model, deg, refineRangeDeg: 8, noFlip: true);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var p90 = samples[(int)Math.Ceiling(samples.Count * 0.9) - 1];
        output.WriteLine($"latency deg={deg:0.0} scale={scale:0.00} P90={p90:0.0}ms samples=[{string.Join(',', samples.Select(s => s.ToString("0.0", CultureInfo.InvariantCulture)))}]");
        Assert.True(p90 < ShapeMatchBenchGates.SpecLatencyMs,
            $"deg={deg} scale={scale} P90 耗时 {p90:0.0}ms > 规格 {ShapeMatchBenchGates.SpecLatencyMs}ms");
    }

    [Fact]
    public void Bench_shape_match_matrix_success_rate()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, ShapeMatchBenchSynth.Contour(0));
        Assert.NotNull(model);

        var ok = 0;
        foreach (var deg in MatrixAngles)
        {
            using var img = ShapeMatchBenchSynth.Paint(deg);
            var contour = ShapeMatchBenchSynth.Contour(deg);
            var noFlip = Math.Abs(Math.Abs(deg) - 180.0) > 1.0;
            var r = ShapeMatchBenchSynth.RefineAngleErr(img, contour, model, deg, refineRangeDeg: 8, noFlip);
            if (r.Ok)
                ok++;
        }

        var rate = ok / (double)MatrixAngles.Length;
        output.WriteLine($"success rate={rate:P2} (spec {ShapeMatchBenchGates.SpecSuccessRate:P1})");
        Assert.True(rate >= ShapeMatchBenchGates.SuccessRateBaseline, $"成功率 {rate:P2}");
        Assert.True(rate >= ShapeMatchBenchGates.SpecSuccessRate, $"成功率 {rate:P2} < 规格 {ShapeMatchBenchGates.SpecSuccessRate:P1}");
    }

    [Fact]
    public void Bench_shape_match_robustness_success_rate()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, ShapeMatchBenchSynth.Contour(0));
        Assert.NotNull(model);

        var cases = new List<(string Name, double Deg, Action<Mat> Perturb, double Scale, double Shear)>();
        foreach (var deg in new[] { 0.0, 8.7, -20.0, 37.0 })
        {
            cases.Add(($"gain0.7@{deg}", deg, img => ShapeMatchBenchSynth.ApplyGainBias(img, 0.70, -8), 1, 0));
            cases.Add(($"gain1.25@{deg}", deg, img => ShapeMatchBenchSynth.ApplyGainBias(img, 1.25, 12), 1, 0));
            cases.Add(($"noise@{deg}", deg, img => ShapeMatchBenchSynth.AddGaussianNoise(img, 8, seed: 7), 1, 0));
            cases.Add(($"occ@{deg}", deg, img => ShapeMatchBenchSynth.PaintOcclusion(img, deg), 1, 0));
            cases.Add(($"scale0.97@{deg}", deg, _ => { }, 0.97, 0));
            cases.Add(($"scale1.03@{deg}", deg, _ => { }, 1.03, 0));
            cases.Add(($"shear@{deg}", deg, img => ShapeMatchBenchSynth.ApplyShear(img, 0.03), 1, 0.03));
        }

        var ok = 0;
        foreach (var c in cases)
        {
            using var img = ShapeMatchBenchSynth.Paint(c.Deg, scale: c.Scale);
            c.Perturb(img);
            var contour = Math.Abs(c.Shear) > 1e-9
                ? ShapeMatchBenchSynth.ContourSheared(c.Deg, c.Shear)
                : ShapeMatchBenchSynth.Contour(c.Deg, scale: c.Scale);
            var r = ShapeMatchBenchSynth.RefineAngleErr(img, contour, model, c.Deg, refineRangeDeg: 8, noFlip: true);
            if (r.Ok && r.AngleErrDeg < ShapeMatchBenchGates.AngleMaxDeg)
                ok++;
            else
                output.WriteLine($"FAIL {c.Name} ok={r.Ok} err={r.AngleErrDeg:0.00} hit={r.HitRate:0.00}");
        }

        var rate = ok / (double)cases.Count;
        output.WriteLine($"robust success={ok}/{cases.Count}={rate:P2} (spec {ShapeMatchBenchGates.SpecSuccessRate:P1})");
        Assert.True(rate >= ShapeMatchBenchGates.SpecSuccessRate,
            $"鲁棒成功率 {rate:P2} < {ShapeMatchBenchGates.SpecSuccessRate:P1}（{ok}/{cases.Count}）");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(-8.7)]
    [InlineData(-37)]
    public void Bench_shape_match_orthogonal_bar_in_margin(double sceneDeg)
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachContour = ShapeMatchBenchSynth.Contour(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, teachContour);
        Assert.NotNull(model);

        using var img = ShapeMatchBenchSynth.Paint(sceneDeg);
        ShapeMatchBenchSynth.PaintOrthogonalBarInMargin(img, sceneDeg);
        var contour = ShapeMatchBenchSynth.Contour(sceneDeg);
        var r = ShapeMatchBenchSynth.RefineAngleErr(img, contour, model!, sceneDeg, refineRangeDeg: 8, noFlip: true);
        output.WriteLine($"orthogonal deg={sceneDeg:0.0}° err={r.AngleErrDeg:0.00}° hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00}");
        Assert.True(r.Ok, $"deg={sceneDeg} 正交条 未过门 hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00}");
        Assert.True(r.AngleErrDeg < ShapeMatchBenchGates.AngleMaxDeg,
            $"deg={sceneDeg} 正交条 角误差 {r.AngleErrDeg:0.00}° > {ShapeMatchBenchGates.AngleMaxDeg}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void Bench_shape_match_parallel_bar_in_margin(double sceneDeg)
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachContour = ShapeMatchBenchSynth.Contour(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, teachContour);
        Assert.NotNull(model);

        using var img = ShapeMatchBenchSynth.Paint(sceneDeg);
        ShapeMatchBenchSynth.PaintParallelBarInMargin(img, sceneDeg);
        var contour = ShapeMatchBenchSynth.Contour(sceneDeg);
        var r = ShapeMatchBenchSynth.RefineAngleErr(img, contour, model!, sceneDeg, refineRangeDeg: 8, noFlip: true);
        output.WriteLine($"parallel deg={sceneDeg:0.0}° err={r.AngleErrDeg:0.00}° hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00}");
        Assert.True(r.Ok, $"deg={sceneDeg} 平行条 未过门 hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00}");
        Assert.True(r.AngleErrDeg < ShapeMatchBenchGates.AngleMaxDeg,
            $"deg={sceneDeg} 平行条 角误差 {r.AngleErrDeg:0.00}° > {ShapeMatchBenchGates.AngleMaxDeg}");
    }

    [Theory]
    [InlineData(0, 0.97)]
    [InlineData(8.7, 0.97)]
    [InlineData(-20, 0.97)]
    [InlineData(37, 0.97)]
    [InlineData(8.7, 1.03)]
    public void Bench_slight_scale_recovers_angle(double deg, double scale)
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, ShapeMatchBenchSynth.Contour(0));
        Assert.NotNull(model);
        using var img = ShapeMatchBenchSynth.Paint(deg, scale: scale);
        var contour = ShapeMatchBenchSynth.Contour(deg, scale: scale);
        var r = ShapeMatchBenchSynth.RefineAngleErr(img, contour, model!, deg, refineRangeDeg: 8, noFlip: true);
        output.WriteLine($"scale={scale:0.00} deg={deg:0.0} ok={r.Ok} err={r.AngleErrDeg:0.00}° hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00}");
        Assert.True(r.Ok, $"scale={scale} deg={deg} 未过门 hit={r.HitRate:0.00} mean={r.MeanDistPx:0.00}");
        Assert.True(r.AngleErrDeg < ShapeMatchBenchGates.SpecAngleDeg,
            $"scale={scale} deg={deg} 角误差 {r.AngleErrDeg:0.00}° > {ShapeMatchBenchGates.SpecAngleDeg}");
    }
}
