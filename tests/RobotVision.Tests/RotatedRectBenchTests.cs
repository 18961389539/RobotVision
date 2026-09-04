using OpenCvSharp;
using RobotVision.Tests.HalconBench;
using RobotVision.Vision;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// HALCON rectangle2 对标基准：精度 / 鲁棒性 / 退化场景。
/// 阈值在合成 ground truth 上标定，用于防回归（非真机规格书）。
/// </summary>
[Trait("Category", "Bench")]
public sealed class RotatedRectBenchTests(ITestOutputHelper output)
{
    private const int W = 480;
    private const int H = 360;
    private const double TrueDeg = 22.0;
    private const double TrueCx = 240;
    private const double TrueCy = 180;
    private const double TrueLong = 200;
    private const double TrueShort = 50;

    [Fact]
    public void Bench_precision_contour_vs_subpixel()
    {
        using var gray = Stripe(TrueCx, TrueCy, TrueDeg, TrueShort / 2);
        var contour = RectContour(TrueCx, TrueCy, TrueDeg, TrueLong, TrueShort, jitter: 0.5);

        var obb = Cv2.MinAreaRect(contour);
        var contourFit = RotatedRectFitter.Fit(MaskHousing.CorePoints(contour), TrueDeg);
        var full = RotatedRectPipeline.Fit(contour, gray, TrueDeg);

        var obbErr = UndirectedErr(obb.Size.Width >= obb.Size.Height ? obb.Angle : obb.Angle + 90, TrueDeg);
        var contourErr = UndirectedErr(contourFit.AngleDeg, TrueDeg);
        var fullErr = UndirectedErr(full.AngleDeg, TrueDeg);

        output.WriteLine($"OBB角误差={obbErr:0.000}° 轮廓={contourErr:0.000}° 全链路={fullErr:0.000}°");
        output.WriteLine($"轮廓RMS={contourFit.RmsPx:0.00}px 全链路RMS={full.RmsPx:0.00}px Q={RotatedRectPipeline.QualityScore(full):0.00}");

        Assert.True(contourErr < 0.35);
        Assert.True(fullErr < 0.15);
        Assert.True(full.RmsPx < contourFit.RmsPx + 0.1);
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(1.5)]
    [InlineData(2.5)]
    public void Bench_robustness_contour_noise(double jitterPx)
    {
        var contour = RectContour(TrueCx, TrueCy, TrueDeg, TrueLong, TrueShort, jitter: jitterPx);
        var fit = RotatedRectFitter.Fit(contour, TrueDeg);
        Assert.True(fit.Ok);
        var err = UndirectedErr(fit.AngleDeg, TrueDeg);
        var limit = jitterPx < 1.0 ? 0.45 : jitterPx < 2.0 ? 0.65 : 0.85;
        output.WriteLine($"jitter={jitterPx:0.0}px 角误差={err:0.000}° RMS={fit.RmsPx:0.00}");
        Assert.True(err < limit);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Bench_degradation_blur_subpixel(int blurK)
    {
        using var sharp = Stripe(TrueCx, TrueCy, TrueDeg, TrueShort / 2);
        using var blurred = new Mat();
        Cv2.GaussianBlur(sharp, blurred, new Size(blurK | 1, blurK | 1), 0);
        var contour = RectContour(TrueCx, TrueCy, TrueDeg, TrueLong, TrueShort, jitter: 0.3);

        var sharpOpt = new RectFitOptions { EdgeMeasureMode = RectEdgeMeasureMode.Sharp };
        var fuzzyOpt = new RectFitOptions { EdgeMeasureMode = RectEdgeMeasureMode.Fuzzy };

        var sharpFit = RotatedRectPipeline.Fit(contour, blurred, TrueDeg, sharpOpt);
        var fuzzyFit = RotatedRectPipeline.Fit(contour, blurred, TrueDeg, fuzzyOpt);

        var sharpErr = UndirectedErr(sharpFit.AngleDeg, TrueDeg);
        var fuzzyErr = UndirectedErr(fuzzyFit.AngleDeg, TrueDeg);
        output.WriteLine($"blur={blurK} sharp={sharpErr:0.000}° fuzzy={fuzzyErr:0.000}°");

        Assert.True(fuzzyErr <= sharpErr + 0.25,
            $"模糊边在 blur={blurK} 应不差于锐边 sharp={sharpErr:0.00} fuzzy={fuzzyErr:0.00}");
    }

    [Fact]
    public void Bench_constraints_fixed_angle()
    {
        var contour = RectContour(TrueCx, TrueCy, TrueDeg, TrueLong, TrueShort, jitter: 1.0);
        var options = new RectFitOptions
        {
            Constraints = new RectFitConstraints(FixedAngleDeg: TrueDeg),
        };
        var fit = RotatedRectFitter.Fit(contour, seedAngleDeg: TrueDeg + 3.0, options);
        Assert.True(fit.Ok);
        Assert.Equal(TrueDeg, fit.AngleDeg, 3);
    }

    [Fact]
    public void Bench_outlier_fraction_tab_contour()
    {
        var contour = TabContour();
        var obb = Cv2.MinAreaRect(contour);
        var fit = RotatedRectFitter.Fit(MaskHousing.CorePoints(contour));
        Assert.True(fit.Ok);
        var obbDy = Math.Abs(obb.Center.Y - TrueCy);
        var fitDy = Math.Abs(fit.Center.Y - TrueCy);
        output.WriteLine($"凸起轮廓：OBB ΔY={obbDy:0.1} 鲁棒 ΔY={fitDy:0.1}");
        Assert.True(fitDy < obbDy - 0.5);
    }

    [Theory]
    [InlineData(-18.0)]
    [InlineData(22.0)]
    [InlineData(45.0)]
    [InlineData(135.0)]
    public void Bench_subpixel_geometry_problem_angles(double trueDeg)
    {
        using var gray = Stripe(TrueCx, TrueCy, trueDeg, TrueShort / 2);
        var contour = RectContour(TrueCx, TrueCy, trueDeg, TrueLong, TrueShort, jitter: 0.4);
        var benchOpt = new RectFitOptions { StripTabProtrusion = false };
        var full = RotatedRectPipeline.Fit(contour, gray, trueDeg, benchOpt);
        var contourOnly = RotatedRectPipeline.FitContour(contour, trueDeg, benchOpt);

        Assert.True(full.Ok && contourOnly.Ok);
        var angErr = UndirectedErr(full.AngleDeg, trueDeg);
        var cxErr = Math.Abs(full.Center.X - TrueCx);
        var cyErr = Math.Abs(full.Center.Y - TrueCy);
        var longErr = Math.Min(Math.Abs(full.LongLen - TrueLong), Math.Abs(full.LongLen - TrueShort));
        var shortErr = Math.Min(Math.Abs(full.ShortLen - TrueShort), Math.Abs(full.ShortLen - TrueLong));
        var q = RotatedRectPipeline.QualityScore(full);

        output.WriteLine(
            $"θ={trueDeg:0}° Δθ={angErr:0.000}° Δc=({cxErr:0.00},{cyErr:0.00}) " +
            $"ΔL={longErr:0.00} ΔS={shortErr:0.00} RMS={full.RmsPx:0.00} Q={q:0.00}");

        var centerErr = Math.Sqrt(cxErr * cxErr + cyErr * cyErr);
        Assert.True(angErr < RotatedRectHalconBenchGates.SpecAngleDeg, $"角误差 {angErr:0.000}° @ {trueDeg}°");
        Assert.True(centerErr < RotatedRectHalconBenchGates.SpecCenterPx, $"中心误差 {centerErr:0.000}px");
        Assert.True(longErr < 3.0 && shortErr < 2.0);
        Assert.True(full.RmsPx <= contourOnly.RmsPx + 0.2);
        Assert.True(q > 0.65);
    }

    [Fact]
    public void Bench_partial_edge_dropout_contour_still_stable()
    {
        var fullContour = RectContour(TrueCx, TrueCy, TrueDeg, TrueLong, TrueShort, jitter: 0.6);
        // 去掉顶边约 35% 采样点，模拟遮挡/分割缺口
        var partial = fullContour
            .Where((_, i) => i % 40 >= 14 || i % 40 < 10)
            .ToArray();
        Assert.True(partial.Length >= 80);

        var fit = RotatedRectFitter.Fit(MaskHousing.CorePoints(partial), TrueDeg);
        Assert.True(fit.Ok);
        var err = UndirectedErr(fit.AngleDeg, TrueDeg);
        output.WriteLine($"缺边轮廓 角误差={err:0.000}° RMS={fit.RmsPx:0.00}");
        Assert.True(err < RotatedRectHalconBenchGates.PartialEdgeAngleDeg);
        Assert.True(fit.RmsPx < 2.0);
    }

    [Fact]
    public void Bench_timing_throughput()
    {
        using var gray = RotatedRectBenchSynth.Rectangle(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, TrueDeg,
            RotatedRectBenchSynth.Long / 2, RotatedRectBenchSynth.Short / 2);
        var contour = RotatedRectBenchSynth.RectContour(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, TrueDeg,
            RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter: 0.6);
        var clip0 = new RectFitOptions { StripTabProtrusion = false, ClipEndPoints = 0 };
        var clip2 = clip0 with { ClipEndPoints = 2 };
        const int n = 300;

        output.WriteLine($"夹具 {W}x{H} 轮廓点={contour.Length}");
        output.WriteLine($"contour clip=0: {TimeMs(n, () => RotatedRectPipeline.FitContour(contour, TrueDeg, clip0)):0.00} ms/op");
        output.WriteLine($"full clip=0:    {TimeMs(n, () => RotatedRectPipeline.Fit(contour, gray, TrueDeg, clip0)):0.00} ms/op");
        output.WriteLine($"contour clip=2: {TimeMs(n, () => RotatedRectPipeline.FitContour(contour, TrueDeg, clip2)):0.00} ms/op");
        output.WriteLine($"full clip=2:    {TimeMs(n, () => RotatedRectPipeline.Fit(contour, gray, TrueDeg, clip2)):0.00} ms/op");
    }

    private static double TimeMs(int n, Func<RotatedRectFitResult> work)
    {
        for (var i = 0; i < 50; i++)
            _ = work();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < n; i++)
            _ = work();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / n;
    }

    [Fact]
    public void Bench_measure_pairs_asymmetric_blur()
    {
        using var sharp = Stripe(TrueCx, TrueCy, TrueDeg, TrueShort / 2);
        using var gray = sharp.Clone();
        using (var blurL = new Mat())
        {
            Cv2.GaussianBlur(sharp, blurL, new Size(9, 9), 0);
            blurL[new Rect(0, 0, W / 2, H)].CopyTo(gray[new Rect(0, 0, W / 2, H)]);
        }

        var contour = RectContour(TrueCx, TrueCy, TrueDeg, TrueLong, TrueShort, jitter: 0.4);
        var benchOpt = new RectFitOptions { StripTabProtrusion = false };
        var full = RotatedRectPipeline.Fit(contour, gray, TrueDeg, benchOpt);
        var fuzzy = RotatedRectPipeline.Fit(contour, gray, TrueDeg, benchOpt with { EdgeMeasureMode = RectEdgeMeasureMode.Fuzzy });

        var sharpErr = UndirectedErr(full.AngleDeg, TrueDeg);
        var fuzzyErr = UndirectedErr(fuzzy.AngleDeg, TrueDeg);
        output.WriteLine($"非对称模糊 sharp θΔ={sharpErr:0.000}° fuzzy θΔ={fuzzyErr:0.000}° RMS={fuzzy.RmsPx:0.00}");

        Assert.True(full.Ok && fuzzy.Ok);
        Assert.True(sharpErr < 0.35 && fuzzyErr < 0.35);
        Assert.True(Math.Abs(fuzzy.Center.X - TrueCx) < 2.0 && Math.Abs(fuzzy.Center.Y - TrueCy) < 2.0);
        Assert.True(RotatedRectPipeline.QualityScore(fuzzy) > 0.6);
    }

    [Fact]
    public void Bench_halcon_parity_synthetic_matrix()
    {
        var benchOpt = new RectFitOptions { StripTabProtrusion = false };
        foreach (var deg in new[] { -18.0, 22.0, 135.0 })
        {
            using var gray = Stripe(TrueCx, TrueCy, deg, TrueShort / 2);
            var contour = RectContour(TrueCx, TrueCy, deg, TrueLong, TrueShort, jitter: 1.0);
            var contourFit = RotatedRectPipeline.FitContour(contour, deg, benchOpt);
            var full = RotatedRectPipeline.Fit(contour, gray, deg, benchOpt);
            var cq = RotatedRectFitQuality.FromContour(contourFit);
            var contourErr = UndirectedErr(contourFit.AngleDeg, deg);
            var fullErr = UndirectedErr(full.AngleDeg, deg);

            output.WriteLine(
                $"θ={deg,5:0}° contour θΔ={contourErr:0.000}° RMS={contourFit.RmsPx:0.00} Q={cq.Score:0.00} | " +
                $"full θΔ={fullErr:0.000}° RMS={full.RmsPx:0.00} Q={RotatedRectPipeline.QualityScore(full):0.00}");

            Assert.True(contourFit.Ok && full.Ok);
            Assert.True(contourErr < RotatedRectHalconBenchGates.SpecAngleDeg, $"轮廓角 @ {deg}°");
            Assert.True(fullErr < RotatedRectHalconBenchGates.SpecAngleDeg, $"全链路角 @ {deg}°");
            Assert.True(full.RmsPx <= contourFit.RmsPx + 0.25);
            // 无限条纹无短边，沿长轴中心不可观，不套 0.1px 规格。
            Assert.True(Math.Abs(full.Center.X - TrueCx) < 1.5 && Math.Abs(full.Center.Y - TrueCy) < 1.5);
            Assert.True(RotatedRectPipeline.QualityScore(full) > 0.7);
        }
    }

    private static double UndirectedErr(double got, double truth)
    {
        var d = Math.Abs(got - truth);
        return Math.Min(d, 180 - d);
    }

    private static Mat Stripe(double cx, double cy, double deg, double halfShort)
    {
        var mat = new Mat(H, W, MatType.CV_8UC1, new Scalar(20));
        var rad = deg * Math.PI / 180.0;
        var nx = -Math.Sin(rad);
        var ny = Math.Cos(rad);
        const double ramp = 3.0;
        for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
        {
            var across = Math.Abs((x - cx) * nx + (y - cy) * ny);
            var t = Math.Clamp((halfShort - across) / ramp + 0.5, 0, 1);
            mat.Set(y, x, (byte)Math.Round(20 + 180 * t));
        }
        return mat;
    }

    private static Point2f[] RectContour(double cx, double cy, double deg, double longLen, double shortLen, double jitter)
    {
        var rng = new Random(11);
        var hw = longLen / 2.0;
        var hh = shortLen / 2.0;
        var corners = new[]
        {
            new Point2d(cx - hw, cy - hh), new Point2d(cx + hw, cy - hh),
            new Point2d(cx + hw, cy + hh), new Point2d(cx - hw, cy + hh),
        };
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2d Rot(Point2d p) => new(
            cx + (p.X - cx) * cos - (p.Y - cy) * sin,
            cy + (p.X - cx) * sin + (p.Y - cy) * cos);
        var rotated = corners.Select(Rot).ToArray();
        var pts = new List<Point2f>();
        for (var e = 0; e < 4; e++)
        {
            var a = rotated[e];
            var b = rotated[(e + 1) % 4];
            for (var s = 0; s < 40; s++)
            {
                var t = (double)s / 40;
                pts.Add(new Point2f(
                    (float)(a.X + (b.X - a.X) * t + Gaussian(rng) * jitter),
                    (float)(a.Y + (b.Y - a.Y) * t + Gaussian(rng) * jitter)));
            }
        }
        return pts.ToArray();
    }

    private static Point2f[] TabContour()
    {
        using var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
        Cv2.FillConvexPoly(mask, RectCorners(TrueCx, TrueCy, 110, 28), Scalar.All(255));
        Cv2.FillConvexPoly(mask, RectCorners(TrueCx, TrueCy + 28 + 9, 22, 9), Scalar.All(255));
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        return contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();
    }

    private static Point[] RectCorners(double cx, double cy, double hw, double hh) =>
    [
        new((int)Math.Round(cx - hw), (int)Math.Round(cy - hh)),
        new((int)Math.Round(cx + hw), (int)Math.Round(cy - hh)),
        new((int)Math.Round(cx + hw), (int)Math.Round(cy + hh)),
        new((int)Math.Round(cx - hw), (int)Math.Round(cy + hh)),
    ];

    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
