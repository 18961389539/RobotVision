using FluentAssertions;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests.Features.Recipe;

public sealed class RecipeEditorUiRefreshTests
{
    [Fact]
    public void PropertyNames_IncludesEditorModeHintsAndViewportChrome()
    {
        var names = RecipeEditorUiRefresh.PropertyNames;

        names.Should().Contain(nameof(RecipeViewModel.IsMaskTemplateMode));
        names.Should().Contain(nameof(RecipeViewModel.PolarityLockHint));
        names.Should().Contain(nameof(RecipeViewModel.HasAnyImage));
        names.Should().Contain(nameof(RecipeViewModel.ShowTestImageViewer));
        names.Should().Contain(nameof(RecipeViewModel.ShowRoiImageViewer));
        names.Should().Contain(nameof(RecipeViewModel.VisibleRecipes));
        names.Should().Contain(nameof(RecipeViewModel.RecipeHealthHint));
        names.Should().Contain(nameof(RecipeViewModel.FeatureGrabOriginHint));
        names.Should().OnlyHaveUniqueItems();
    }
}
