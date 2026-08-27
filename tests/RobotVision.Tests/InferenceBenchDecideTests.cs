using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.InferenceBench;
using Xunit;

namespace RobotVision.Tests;

public class InferenceBenchDecideTests
{
    [Fact]
    public void Percentile_Interpolates()
    {
        var sorted = Enumerable.Range(1, 5).Select(i => (double)i).ToArray();
        Assert.Equal(3, Stats.Percentile(sorted, 50));
        Assert.Equal(1, Stats.Percentile(sorted, 0));
        Assert.Equal(5, Stats.Percentile(sorted, 100));
    }

    [Fact]
    public void PhaseA_PrefersClearlyFasterOpenVino()
    {
        var decision = BenchDecide.From(
        [
            Sample("CPU", 200),
            Sample("OpenVINO CPU", 80),
        ], serialWinner: null, parallelSameEp: null, mixedControl: null);

        Assert.Equal("OpenVINO CPU", decision.WinnerEp);
        Assert.True(decision.SwitchFromCpu);
        Assert.Contains(decision.Lines, l => l.Contains("全局换成", StringComparison.Ordinal));
    }

    [Fact]
    public void PhaseA_StaysCpuWhenGainUnderTenPercent()
    {
        var decision = BenchDecide.From(
        [
            Sample("CPU", 100),
            Sample("OpenVINO CPU", 95),
        ], null, null, null);

        Assert.False(decision.SwitchFromCpu);
        Assert.Contains(decision.Lines, l => l.Contains("维持 OpenVINO CPU", StringComparison.Ordinal));
    }

    [Fact]
    public void PhaseB_DropsMixedWhenCpuSlowerThanSerialTwice()
    {
        var serial = Overlap("串行", 160);
        var parallel = Overlap("并行", 90);
        var mixed = Overlap("混池", 200);
        var decision = BenchDecide.From(
        [
            Sample("CPU", 200),
            Sample("OpenVINO CPU", 80),
        ], serial, parallel, mixed);

        Assert.True(decision.AddSecondSameEpSession);
        Assert.True(decision.DropMixedPool);
        Assert.Contains(decision.Lines, l => l.Contains("去掉 CPU+OpenVINO 混池", StringComparison.Ordinal));
    }

    [Fact]
    public void RoiCrop_MatchesRoundedIntersection()
    {
        using var mat = new Mat(100, 200, MatType.CV_8UC3, Scalar.Black);
        using var cropped = BenchImage.Crop(mat, new Roi(0.25, 0.25, 0.5, 0.5), out var ox, out var oy);
        Assert.Equal(50, ox);
        Assert.Equal(25, oy);
        Assert.Equal(100, cropped.Width);
        Assert.Equal(50, cropped.Height);
    }

    private static EpSample Sample(string name, double p50) => new()
    {
        Name = name,
        Latency = new Percentiles(p50, p50 * 1.1, p50 * 1.2, p50, p50, p50),
    };

    private static OverlapSummary Overlap(string name, double p50) => new()
    {
        Name = name,
        Makespan = new Percentiles(p50, p50, p50, p50, p50, p50),
        WaitA = new Percentiles(p50, p50, p50, p50, p50, p50),
        WaitB = new Percentiles(p50, p50, p50, p50, p50, p50),
    };
}
