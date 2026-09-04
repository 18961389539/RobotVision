using RobotVision.Core.Recipe;
using RobotVision.Tests.HalconBench;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

public sealed class ShapeMatchOptionsTests
{
    [Fact]
    public void FromTemplate_mapsHalconLikeFields()
    {
        var t = new TemplateOptions
        {
            RefineRangeDeg = 6,
            ShapeMatchNumLevels = 3,
            ShapeMatchMinContrast = 12,
            ShapeMatchMetric = ShapeMatchMetric.IgnoreLocalPolarity,
        };
        var o = ShapeMatchOptions.From(t);
        Assert.Equal(3, o.NumLevels);
        Assert.Equal(12, o.MinContrast);
        Assert.Equal(ShapeMatchMetric.IgnoreLocalPolarity, o.Metric);
        Assert.Equal(12, o.AngleExtentDeg);
        Assert.Equal(-6, o.AngleStartDeg);
    }

    [Fact]
    public void Default_matchesRuntimeGates()
    {
        var o = ShapeMatchOptions.Default;
        Assert.Equal(0.18, o.MinHitRate, 3);
        Assert.Equal(10.0, o.MaxMeanDistPx);
        Assert.Equal(ShapeMatchMetric.UsePolarity, o.Metric);
        Assert.Equal(2, o.NumLevels);
    }

    [Fact]
    public void MinContrast_and_NumLevels3_still_match_identity()
    {
        using var img = ShapeMatchBenchSynth.Paint(0);
        var contour = ShapeMatchBenchSynth.Contour(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(img, contour);
        Assert.NotNull(model);
        var opt = new ShapeMatchOptions
        {
            NumLevels = 3,
            MinContrast = 18,
            EnableVisualization = true,
            EmitSearchDebug = true,
            AngleStartDeg = 0,
            AngleExtentDeg = 16,
        };
        var attempt = MaskShapeMatch.TryRefine(img, contour, model, refineRangeDeg: 8, noFlip: true, options: opt);
        Assert.NotNull(attempt.Pose);
        Assert.True(Math.Abs(attempt.Pose!.AngleDeg) < 0.5);
        Assert.NotNull(attempt.Viz.DistHistogram);
        Assert.Equal(8, attempt.Viz.DistHistogram!.Count);
        Assert.True(attempt.Viz.Inliers.Count + attempt.Viz.Rejected.Count > 0);
        Assert.NotNull(attempt.Viz.SearchDebug);
        Assert.InRange(attempt.Viz.PyramidLevels, 1, 3);
    }

    [Fact]
    public void SameInput_isDeterministic()
    {
        using var teach = ShapeMatchBenchSynth.Paint(0);
        var model = ShapeMatchBenchSynth.TeachFromZero(teach, ShapeMatchBenchSynth.Contour(0));
        using var img = ShapeMatchBenchSynth.Paint(-20);
        var contour = ShapeMatchBenchSynth.Contour(-20);
        var a = MaskShapeMatch.TryRefine(img, contour, model, 8, noFlip: true);
        var b = MaskShapeMatch.TryRefine(img, contour, model, 8, noFlip: true);
        Assert.NotNull(a.Pose);
        Assert.NotNull(b.Pose);
        Assert.Equal(a.Pose!.AngleDeg, b.Pose!.AngleDeg, 6);
        Assert.Equal(a.Pose.Center.X, b.Pose.Center.X, 6);
        Assert.Equal(a.Pose.Center.Y, b.Pose.Center.Y, 6);
        Assert.Equal(a.Pose.Score, b.Pose.Score, 6);
    }
}
