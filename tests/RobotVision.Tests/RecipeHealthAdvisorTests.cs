using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

public sealed class RecipeHealthAdvisorTests
{
    [Fact]
    public void Empty_NoHints()
    {
        Assert.Empty(RecipeHealthAdvisor.Analyze(0, [], [], null));
        Assert.Empty(RecipeHealthAdvisor.Analyze(5, [new ResultCodeCount(1019, 1)], [], null));
    }

    [Fact]
    public void RefineFailRate_WhenHigh()
    {
        var hints = RecipeHealthAdvisor.Analyze(
            25, [new ResultCodeCount(1019, 4), new ResultCodeCount(0, 21)], [], null);
        Assert.Contains(hints, h => h.Id == "refine_fail_rate");
    }

    [Fact]
    public void Bimodal_NearZeroAndNear180()
    {
        var angles = Enumerable.Repeat(2.0, 8).Concat(Enumerable.Repeat(178.0, 8)).ToArray();
        var hints = RecipeHealthAdvisor.Analyze(16, [new ResultCodeCount(0, 16)], angles, null);
        Assert.Contains(hints, h => h.Id == "angle_bimodal");
    }

    [Fact]
    public void ScoreDrift_VsTeachPeak()
    {
        var spread = new ResultPoseSpread(
            null, null, null, null, null, null, null, null, null, null, null, null, 0.4);
        var hints = RecipeHealthAdvisor.Analyze(20, [new ResultCodeCount(0, 20)], [], spread, teachPeakScore: 0.85);
        Assert.Contains(hints, h => h.Id == "score_drift");
    }

    [Fact]
    public void ToPlaybookPrior_RefineFailDownranksCurrent()
    {
        var hints = RecipeHealthAdvisor.Analyze(
            25, [new ResultCodeCount(1019, 4), new ResultCodeCount(0, 21)], [], null);
        var prior = RecipeHealthAdvisor.ToPlaybookPrior(hints, SegmentRefineMethod.Template);
        Assert.NotNull(prior);
        Assert.Equal(SegmentRefineMethod.Template, prior!.Downrank);
        Assert.Contains("1019", prior.Reason);
    }
}
