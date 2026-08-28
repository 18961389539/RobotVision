using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class DetectionThresholdAdvisorTests
{
    [Fact]
    public void SuggestBoxConfidence_KeepsLooseDefaultWhenInstanceIsHigh()
    {
        Assert.Equal(0.5, DetectionThresholdAdvisor.SuggestBoxConfidence(0.92, 0.5));
    }

    [Fact]
    public void SuggestBoxConfidence_LowersWhenBarelyPassing()
    {
        var next = DetectionThresholdAdvisor.SuggestBoxConfidence(0.52, 0.5);
        Assert.InRange(next, 0.20, 0.50);
        Assert.True(next < 0.5);
    }

    [Fact]
    public void SuggestPixelConfidence_OnlyRelaxesFactoryDefault()
    {
        Assert.Equal(0.55, DetectionThresholdAdvisor.SuggestPixelConfidence(0.65));
        Assert.Equal(0.40, DetectionThresholdAdvisor.SuggestPixelConfidence(0.40));
    }
}
