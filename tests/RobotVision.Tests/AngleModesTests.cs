using FluentAssertions;
using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class AngleModesTests
{
    [Theory]
    [InlineData(AngleMode.DualBlobCenterLine, true)]
    [InlineData(AngleMode.DualTemplateCenterLine, true)]
    [InlineData(AngleMode.MaskTemplate, false)]
    [InlineData(AngleMode.DualCenterLine, false)]
    [InlineData(AngleMode.KeyPointLine, false)]
    [InlineData(AngleMode.MaskMinAreaRect, false)]
    public void IsModelFree_MatchesRequiresOnnxInverse(AngleMode mode, bool modelFree)
    {
        AngleModes.IsModelFree(mode).Should().Be(modelFree);
        AngleModes.RequiresOnnx(mode).Should().Be(!modelFree);
        AngleModes.UsesSecondarySearchRoi(mode).Should().Be(modelFree);
    }

    [Theory]
    [InlineData(AngleMode.MaskMinAreaRect, true)]
    [InlineData(AngleMode.KeyPointLine, true)]
    [InlineData(AngleMode.MaskTemplate, true)]
    [InlineData(AngleMode.DualCenterLine, false)]
    [InlineData(AngleMode.DualBlobCenterLine, false)]
    [InlineData(AngleMode.DualTemplateCenterLine, false)]
    public void UsesSingleModelSlot(AngleMode mode, bool expected) =>
        AngleModes.UsesSingleModelSlot(mode).Should().Be(expected);

    [Fact]
    public void UsesDualModelSlots_OnlyDualCenterLine() =>
        AngleModes.UsesDualModelSlots(AngleMode.DualCenterLine).Should().BeTrue();
}
