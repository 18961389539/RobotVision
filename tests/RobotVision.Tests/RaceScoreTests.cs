using RobotVision.Core.Recipe;
using RobotVision.Teach;
using Xunit;

namespace RobotVision.Tests;

/// <summary>RaceScore 共享打分聚合单测（Aggregate 与 ScoreThreshold 现在共用它）。</summary>
public sealed class RaceScoreTests
{
    private static SegmentRefineCandidate Row(double score, bool ok, bool directed, double angle) =>
        new(SegmentRefineMethod.Template, ok, directed, score, "", angle);

    [Fact]
    public void Consistency_PenalizesFlippedAngles_WhenDirected()
    {
        var stable = RaceScore.Compute([Row(0.8, true, true, 0), Row(0.8, true, true, 1)], totalFrames: 2, directed: true);
        var flip = RaceScore.Compute([Row(0.8, true, true, 0), Row(0.8, true, true, 180)], totalFrames: 2, directed: true);

        Assert.Equal(0.8, stable.MeanScore, 3);
        Assert.True(stable.Consistency > flip.Consistency);
        Assert.True(stable.Score > flip.Score);
    }

    [Fact]
    public void PassRate_CountsMissedFramesInDenominator()
    {
        var agg = RaceScore.Compute([Row(0.9, true, false, 10)], totalFrames: 3, directed: false);
        Assert.Equal(1, agg.OkCount);
        Assert.Equal(3, agg.TotalFrames);
        Assert.True(Math.Abs(agg.Score - 0.9 / 3.0) < 1e-9); // consistency=1（样本<2），均分 0.9，过门率 1/3
        Assert.Equal(RaceScore.OkGate, 0.35, 3);
    }

    [Fact]
    public void NoOkRows_FailsGateAndStoresNaNStats()
    {
        var agg = RaceScore.Compute([], totalFrames: 2, directed: true);
        Assert.False(agg.Ok);
        Assert.Equal(0, agg.Score);
        Assert.True(double.IsNaN(agg.AngleStdDeg));
        Assert.True(double.IsNaN(agg.SampleAngleDeg));
        Assert.Equal(0, agg.AngleSampleCount);
    }

    [Fact]
    public void BelowGate_NotOk()
    {
        // 均分高但过门率低 → 复合分低于门。
        var agg = RaceScore.Compute([Row(0.8, true, false, 0)], totalFrames: 5, directed: false);
        Assert.True(agg.Score < RaceScore.OkGate);
        Assert.False(agg.Ok);
    }
}
