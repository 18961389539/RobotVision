using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

public sealed class ResultAnalysisTests
{
    [Fact]
    public void BuildHistogram_MatchesAnalysisPageBins()
    {
        Assert.Empty(ResultAnalysis.BuildHistogram([]));

        var same = ResultAnalysis.BuildHistogram([1.5, 1.5, 1.5]);
        Assert.Single(same);
        Assert.Equal(3, same[0].Count);
        Assert.Equal(1, same[0].Ratio);
        Assert.True(same[0].End > same[0].Start);

        var spread = ResultAnalysis.BuildHistogram([-6, 0, 6], binCount: 3);
        Assert.Equal(3, spread.Count);
        Assert.Equal(3, spread.Sum(b => b.Count));
        Assert.Equal(1, spread.Max(b => b.Ratio));
        Assert.Equal(-6, spread[0].Start, 6);
        Assert.Equal(-2, spread[0].End, 6);
    }

    [Fact]
    public void DescribeCode_KnownAndUnknown()
    {
        Assert.Equal("合格", ResultAnalysis.DescribeCode(0));
        Assert.Equal("1007 未检出", ResultAnalysis.DescribeCode(1007));
        Assert.Equal("1018 过程联锁", ResultAnalysis.DescribeCode(1018));
        Assert.Equal("42", ResultAnalysis.DescribeCode(42));
    }

    [Fact]
    public void PopulationStd_ZeroAndSpread()
    {
        Assert.Null(ResultAnalysis.PopulationStd(null, 1));
        Assert.Equal(0, ResultAnalysis.PopulationStd(3, 9));
        Assert.Equal(5, ResultAnalysis.PopulationStd(5, 50)!.Value, 6);
    }
}
