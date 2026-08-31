using RobotVision.Core.Recipe;
using RobotVision.Teach;
using Xunit;

namespace RobotVision.Tests;

public sealed class RefineParamTunerTests
{
    [Fact]
    public void ThresholdSweep_PicksHigherGateWhenScoresAreAllHigh()
    {
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames =
        [
            [Hit(0.88, 0)],
            [Hit(0.90, 1)],
            [Hit(0.87, 0.5)],
        ];
        var current = new TemplateOptions { MatchThreshold = 0.50, TeachPeakScore = 0.90 };
        var sug = RefineParamTuner.Tune(SegmentRefineMethod.Template, frames, [1, 1, 1], current, 0.90);
        Assert.NotNull(sug);
        Assert.True(sug!.MatchThreshold >= 0.76, $"高分夹应收紧门，实际 {sug.MatchThreshold}");
        Assert.Contains("匹配门", sug.Summary, StringComparison.Ordinal);
        Assert.Contains(sug.Trials, t => t.Best);
    }

    [Fact]
    public void ThresholdSweep_LowersGateWhenHighGateDropsPassRate()
    {
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames =
        [
            [Hit(0.72, 0)],
            [Hit(0.71, 1)],
            [Hit(0.90, 0)],
            [Hit(0.70, 2)],
        ];
        var current = new TemplateOptions { MatchThreshold = 0.85, TeachPeakScore = 0.90 };
        var sug = RefineParamTuner.Tune(SegmentRefineMethod.Template, frames, [1, 1, 1, 1], current, 0.90);
        Assert.NotNull(sug);
        Assert.True(sug!.MatchThreshold is >= 0.70 and <= 0.78,
            $"应放宽到过门，实际 {sug.MatchThreshold}：{sug.Summary}");
    }

    [Fact]
    public void Untaught_DoesNotSweepThreshold()
    {
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames =
        [
            [new(SegmentRefineMethod.Template, false, true, 0, "未示教", Skipped: true)],
            [new(SegmentRefineMethod.Template, false, true, 0, "未示教", Skipped: true)],
        ];
        var sug = RefineParamTuner.Tune(
            SegmentRefineMethod.Template, frames, [1, 1], new TemplateOptions(), 0);
        Assert.True(sug is null || sug.MatchThreshold is null);
    }

    [Fact]
    public void ExpectedCount_UsesStableModeWhenTaskIsZero()
    {
        var frames = EmptyFrames(4);
        var sug = RefineParamTuner.Tune(
            SegmentRefineMethod.CaliperTab, frames, [2, 2, 2, 1], new TemplateOptions(),
            taskExpectedCount: 0);
        Assert.NotNull(sug);
        Assert.Equal(2, sug!.ExpectedCount);
        Assert.Contains("期望件数", sug.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpectedCount_DoesNotOverrideTaskValue()
    {
        var frames = EmptyFrames(3);
        var sug = RefineParamTuner.Tune(
            SegmentRefineMethod.CaliperTab, frames, [2, 2, 2], new TemplateOptions(),
            taskExpectedCount: 1);
        Assert.NotNull(sug);
        Assert.Null(sug!.ExpectedCount);
        Assert.Contains("不符", sug.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void NearCircular_HighStd_SuggestsWiderRange()
    {
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames =
        [
            [Hit(0.88, 0)],
            [Hit(0.88, 20)],
            [Hit(0.88, 40)],
            [Hit(0.88, -15)],
        ];
        var current = new TemplateOptions { MatchThreshold = 0.70, RefineRangeDeg = 5, TeachPeakScore = 0.88 };
        var sug = RefineParamTuner.Tune(
            SegmentRefineMethod.Template, frames, [1, 1, 1, 1], current, 0.88, sceneAspect: 1.1);
        Assert.NotNull(sug);
        Assert.Equal(8, sug!.RefineRangeDeg);
    }

    [Fact]
    public void ScoreThreshold_MissedFramesCountInDenominator()
    {
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames =
        [
            [Hit(0.90, 0)],
            [],
            [],
        ];
        var (score, _, note, ok) = RefineParamTuner.ScoreThreshold(
            frames, SegmentRefineMethod.Template, 0.60, directed: true);
        Assert.Equal(0.30, score, 2);
        Assert.False(ok);
        Assert.Contains("1/3", note, StringComparison.Ordinal);
    }

    private static SegmentRefineCandidate Hit(double score, double angle) =>
        new(SegmentRefineMethod.Template, score >= 0.6, true, score, "hit", angle);

    private static IReadOnlyList<SegmentRefineCandidate>[] EmptyFrames(int n) =>
        Enumerable.Range(0, n).Select(_ => (IReadOnlyList<SegmentRefineCandidate>)[]).ToArray();
}
