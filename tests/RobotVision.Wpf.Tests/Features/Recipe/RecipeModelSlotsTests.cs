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
    public void TryCommitUiModels_SingleModelMode_TrimsExtraSlots()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx", "b.onnx"],
        };

        RecipeModelSlots.TryCommitUiModels(editor, "a.onnx", "").Should().BeNull();
        editor.Models.Should().Equal("a.onnx");
    }

    [Fact]
    public void TrimToSingleModelSlot_LeavesDualCenterLineAlone()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.DualCenterLine,
            Models = ["a.onnx", "b.onnx"],
        };

        RecipeModelSlots.TrimToSingleModelSlot(editor).Should().BeFalse();
        editor.Models.Should().Equal("a.onnx", "b.onnx");
    }

    [Fact]
    public void RecipeEditorModeCleanup_DualToMaskTemplate_TrimsAndClearsTeachImage()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx", "b.onnx"],
            Template =
            {
                RefineMethod = SegmentRefineMethod.LineFit,
                TemplateImageBase64 = "abc==",
                HousingEdgePolarity = HousingEdgePolarity.BrightToDark,
            },
        };

        var note = RecipeEditorModeCleanup.Apply(editor);

        editor.Models.Should().Equal("a.onnx");
        editor.Template.TemplateImageBase64.Should().BeEmpty();
        editor.Template.HousingEdgePolarity.Should().Be(HousingEdgePolarity.Auto);
        note.Should().Contain("次模型");
        note.Should().Contain("示教模板图");
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
