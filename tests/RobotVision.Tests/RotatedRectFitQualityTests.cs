using FluentAssertions;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

public sealed class RotatedRectFitQualityTests
{
    [Fact]
    public void FromContour_mapsRmsAndInliers()
    {
        var fit = new RotatedRectFitResult(new(100, 100), 22, 200, 50, 40, 0.5, true);
        var q = RotatedRectFitQuality.FromContour(fit);
        q.RmsPx.Should().Be(0.5);
        q.Inliers.Should().Be(40);
        q.MaxParallelDeg.Should().BeNull();
        q.Score.Should().BeInRange(0.5, 1.0);
        RotatedRectFitQuality.NormalizedRms(fit).Should().BeApproximately(0.01, 1e-9);
    }

    [Fact]
    public void EvaluateQuality_withParallel_blendsTerms()
    {
        var fit = new RotatedRectFitResult(new(100, 100), 22, 200, 50, 40, 0.2, true);
        var q = RotatedRectPipeline.EvaluateQuality(fit, maxParallelDeg: 0.5);
        q.MaxParallelDeg.Should().Be(0.5);
        q.Score.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void FormatNote_includesParallelWhenPresent()
    {
        var q = new RotatedRectFitQuality(0.91, 0.12, 36, 0.8);
        q.FormatNote().Should().Contain("平行差");
        q.FormatNote().Should().Contain("0.91");
    }
}
