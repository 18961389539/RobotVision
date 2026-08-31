using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class RecipeDisplayHintsTests
{
    [Theory]
    [InlineData(SegmentRefineMethod.CaliperTab)]
    [InlineData(SegmentRefineMethod.ShapeMatch)]
    [InlineData(SegmentRefineMethod.Sift)]
    public void ForRecipeTest_RefineWithDebugOverlay_EnablesRefineDebug(SegmentRefineMethod refine)
    {
        var recipe = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Template = { RefineMethod = refine },
            Roi = new Roi(0.1, 0.2, 0.3, 0.4),
        };

        var hints = RecipeDisplayHints.ForRecipeTest(recipe);

        hints.ShowRefineDebug.Should().BeTrue();
        hints.DrawDetectionRoi.Should().BeTrue();
        hints.DetectionRoi.Should().NotBeNull();
    }

    [Fact]
    public void ForRecipeTest_Template_DisablesRefineDebug()
    {
        var recipe = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Template = { RefineMethod = SegmentRefineMethod.Template },
        };

        var hints = RecipeDisplayHints.ForRecipeTest(recipe);

        hints.ShowRefineDebug.Should().BeFalse();
        hints.DrawDetectionRoi.Should().BeFalse();
    }

    [Fact]
    public void Production_IsMinimal()
    {
        RecipeDisplayHints.Production.ShowRefineDebug.Should().BeFalse();
        RecipeDisplayHints.Production.DrawDetectionRoi.Should().BeFalse();
        RecipeDisplayHints.Production.DetectionRoi.Should().BeNull();
    }

    [Theory]
    [InlineData(SegmentRefineMethod.CaliperTab, true)]
    [InlineData(SegmentRefineMethod.ShapeMatch, true)]
    [InlineData(SegmentRefineMethod.Sift, true)]
    [InlineData(SegmentRefineMethod.Template, false)]
    [InlineData(SegmentRefineMethod.LineFit, false)]
    public void RefineMethodShowsDebugOverlay_MatchesExpectation(SegmentRefineMethod refine, bool expected) =>
        RecipeDisplayHints.RefineMethodShowsDebugOverlay(refine).Should().Be(expected);
}
