using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Tests.HalconBench;
using RobotVision.Vision;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>诊断转正窗与示教的对齐残差（NCC rotation ≈ 0 为理想）。</summary>
public sealed class ShapeMatchUprightAlignTests(ITestOutputHelper output)
{
    private const double Margin = 0.15;

    [Theory]
    [InlineData(-37)]
    [InlineData(-20)]
    [InlineData(-8.7)]
    [InlineData(0)]
    [InlineData(8.7)]
    [InlineData(20)]
    [InlineData(37)]
    public void UprightCrop_NccResidual_vsTeach(double deg)
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachContour = ShapeMatchBenchSynth.Contour(0);
        using var liveImg = ShapeMatchBenchSynth.Paint(deg);
        var liveContour = ShapeMatchBenchSynth.Contour(deg);

        var teachCrop = MaskTemplateMatcher.UprightCrop(teachImg, teachContour, Margin);
        var liveCrop = MaskTemplateMatcher.UprightCrop(liveImg, liveContour, Margin);
        var housing = MaskHousing.Fit(liveContour);
        var lf = MaskTemplateMatcher.RefineByLineFit(liveContour, housing.WarpAngleDeg);

        using (teachCrop.Upright)
        using (liveCrop.Upright)
        {
            var m = MaskTemplateMatcher.MatchBest(liveCrop.Upright, teachCrop.Upright, refineRangeDeg: 12, minScore: 0.15);
            output.WriteLine(
                $"deg={deg,5:0.0} warp={liveCrop.WarpAngleDeg,6:0.00} housing={housing.WarpAngleDeg,6:0.00} " +
                $"lineFit={lf.AngleDeg,6:0.00} fitted={lf.Fitted} nccRot={m?.RotationDeg ?? double.NaN,6:0.00}");
            Assert.NotNull(m);
        }
    }

    [Fact]
    public void ProbeNccScore_atLargeNegative()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachCrop = MaskTemplateMatcher.UprightCrop(teachImg, ShapeMatchBenchSynth.Contour(0), 0);
        MaskShapeMatch.ShapeModel? model;
        using (teachCrop.Upright)
            model = MaskShapeMatch.BuildTeach(teachCrop.Upright);
        Assert.NotNull(model);
        using var img = ShapeMatchBenchSynth.Paint(-37);
        var liveCrop = MaskTemplateMatcher.UprightCrop(img, ShapeMatchBenchSynth.Contour(-37), 0.15);
        using (liveCrop.Upright)
        {
            var ncc = MaskTemplateMatcher.MatchBest(
                liveCrop.Upright, model.NccGray, 8, 0.05, orientationBranchDeg: 0);
            output.WriteLine($"ncc score={ncc?.Score:0.000} rot={ncc?.RotationDeg:0.00}");
            Assert.NotNull(ncc);
        }
    }

    [Fact]
    public void ChamferOverlay_raw_at_identity_residual()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teachImg, ShapeMatchBenchSynth.Contour(0));
        Assert.NotNull(model);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        foreach (var deg in new[] { 0.0, 8.7, -20.0, 37.0 })
        {
            using var img = ShapeMatchBenchSynth.Paint(deg);
            var contour = ShapeMatchBenchSynth.Contour(deg);
            var crop = MaskTemplateMatcher.UprightCrop(img, contour, MaskShapeMatch.CropMarginRatio);
            using (crop.Upright)
            {
                var housing = MaskHousing.Fit(contour);
                var hu = MaskTemplateMatcher.MapSourceToUpright(crop, new Point2d(housing.Center.X, housing.Center.Y));
                var mapped = MaskShapeMatch.ContourInUpright(crop, contour);
                var minX = double.PositiveInfinity;
                var minY = double.PositiveInfinity;
                var maxX = double.NegativeInfinity;
                var maxY = double.NegativeInfinity;
                var inside = 0;
                foreach (var u in mapped)
                {
                    minX = Math.Min(minX, u.X);
                    minY = Math.Min(minY, u.Y);
                    maxX = Math.Max(maxX, u.X);
                    maxY = Math.Max(maxY, u.Y);
                    if (u.X >= 0 && u.Y >= 0 && u.X < crop.Upright.Width && u.Y < crop.Upright.Height)
                        inside++;
                }

                var cx = crop.Upright.Width / 2.0 + model!.HousingOffsetX;
                var cy = crop.Upright.Height / 2.0 + model.HousingOffsetY;
                var atSeed = MaskShapeMatch.DebugChamferAt(crop.Upright, model, 0, new Point2d(cx, cy), crop.WarpAngleDeg, mapped);

                output.WriteLine(
                    $"deg={deg.ToString("0.0", inv)} warp={crop.WarpAngleDeg.ToString("0.02", inv)} " +
                    $"crop={crop.Upright.Width}x{crop.Upright.Height} " +
                    $"housingU=({hu.X.ToString("0.1", inv)},{hu.Y.ToString("0.1", inv)}) " +
                    $"cropC=({(crop.Upright.Width / 2.0).ToString("0.1", inv)},{(crop.Upright.Height / 2.0).ToString("0.1", inv)}) " +
                    $"hOff=({model.HousingOffsetX.ToString("0.1", inv)},{model.HousingOffsetY.ToString("0.1", inv)}) " +
                    $"contourBB=({minX.ToString("0.0", inv)},{minY.ToString("0.0", inv)})-({maxX.ToString("0.0", inv)},{maxY.ToString("0.0", inv)}) inside={inside}/{contour.Length} " +
                    $"seed mean={atSeed.Mean.ToString("0.02", inv)} hit={atSeed.Hit.ToString("0.00", inv)}");
                Assert.True(inside == contour.Length, $"deg={deg} contour outside crop inside={inside}/{contour.Length}");
                Assert.True(atSeed.Hit >= 0.70, $"deg={deg} overlay hit {atSeed.Hit:0.00}");
                Assert.True(atSeed.Mean <= 3.0, $"deg={deg} overlay mean {atSeed.Mean:0.00}");
            }
        }
    }

    [Fact]
    public void ChamferResidual_TeachMargin0_vs015()
    {
        using var teachImg = ShapeMatchBenchSynth.Paint(0);
        var teachContour = ShapeMatchBenchSynth.Contour(0);
        foreach (var deg in new[] { -37.0, 37.0, 20.0, 0.0 })
        {
            using var img = ShapeMatchBenchSynth.Paint(deg);
            var contour = ShapeMatchBenchSynth.Contour(deg);
            foreach (var teachMargin in new[] { 0.0, 0.15 })
            {
                var teachCrop = MaskTemplateMatcher.UprightCrop(teachImg, teachContour, teachMargin);
                MaskShapeMatch.ShapeModel? model;
                using (teachCrop.Upright)
                    model = MaskShapeMatch.BuildTeach(teachCrop.Upright);
                Assert.NotNull(model);
                var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8, noFlip: true);
                var err = attempt.Pose is null
                    ? double.NaN
                    : Math.Abs(AngleGeometry.NormalizeSignedDeg(attempt.Pose.AngleDeg - deg));
                output.WriteLine(
                    $"deg={deg,5:0.0} teachMargin={teachMargin:0.00} ok={attempt.Pose is not null} " +
                    $"err={err:0.00} residual={MaskShapeMatch.LastDebug.ResidualDeg:0.00} " +
                    $"hit={MaskShapeMatch.LastDebug.HitRate:0.00} mean={MaskShapeMatch.LastDebug.MeanDist:0.00}");
            }
        }
    }
}
