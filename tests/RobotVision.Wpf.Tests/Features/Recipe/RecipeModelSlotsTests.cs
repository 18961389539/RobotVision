using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests;

public sealed class RecipeModelSlotsTests
{
    [Fact]
    public void TryCommitUiModels_DualBlob_PreservesThirdModel()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.DualBlobCenterLine,
            Models = ["a.onnx", "b.onnx", "c.onnx"],
        };

        RecipeModelSlots.TryCommitUiModels(editor, "a.onnx", "b.onnx").Should().BeNull();
        editor.Models.Should().Equal("a.onnx", "b.onnx", "c.onnx");
    }

    [Fact]
    public void TryCommitUiModels_DualCenterLine_BlocksThirdModel()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.DualCenterLine,
            Models = ["a.onnx", "b.onnx", "c.onnx"],
        };

        var error = RecipeModelSlots.TryCommitUiModels(editor, "a.onnx", "b.onnx");
        error.Should().NotBeNullOrEmpty();
        editor.Models.Should().Equal("a.onnx", "b.onnx", "c.onnx");
    }

    [Fact]
    public void TryCommitUiModels_SingleModelMode_BlocksExtraSlots()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx", "b.onnx"],
        };

        var error = RecipeModelSlots.TryCommitUiModels(editor, "a.onnx", "");
        error.Should().Contain("只需 1 个模型");
        editor.Models.Should().Equal("a.onnx", "b.onnx");
    }

    [Fact]
    public void TryCommitUiModels_SingleModelMode_NormalizesToPrimary()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.MaskMinAreaRect,
            Models = ["a.onnx"],
        };

        RecipeModelSlots.TryCommitUiModels(editor, "a.onnx", "").Should().BeNull();
        editor.Models.Should().Equal("a.onnx");
    }
}
