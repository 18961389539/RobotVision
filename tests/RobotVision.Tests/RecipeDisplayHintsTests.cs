using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class RecipeDisplayHintsTests
{
    [Fact]
    public void ForRecipeTest_CaliperTab_EnablesCaliperDebug()
    {
        var recipe = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Template = { RefineMethod = SegmentRefineMethod.CaliperTab },
            Roi = new Roi(0.1, 0.2, 0.3, 0.4),
        };

        var hints = RecipeDisplayHints.ForRecipeTest(recipe);

        hints.ShowCaliperDebug.Should().BeTrue();
        hints.DrawDetectionRoi.Should().BeTrue();
        hints.DetectionRoi.Should().NotBeNull();
    }

    [Fact]
    public void ForRecipeTest_Template_DisablesCaliperDebug()
    {
        var recipe = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Template = { RefineMethod = SegmentRefineMethod.Template },
        };

        var hints = RecipeDisplayHints.ForRecipeTest(recipe);

        hints.ShowCaliperDebug.Should().BeFalse();
        hints.DrawDetectionRoi.Should().BeFalse();
    }

    [Fact]
    public void Production_IsMinimal()
    {
        RecipeDisplayHints.Production.ShowCaliperDebug.Should().BeFalse();
        RecipeDisplayHints.Production.DrawDetectionRoi.Should().BeFalse();
        RecipeDisplayHints.Production.DetectionRoi.Should().BeNull();
    }
}
