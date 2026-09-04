using FluentAssertions;
using RobotVision.Core.Models;
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
    public void TryCommitUiModels_DualTemplate_PreservesThirdModel()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.DualTemplateCenterLine,
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
        note.Should().Contain("次模型");
        note.Should().Contain("示教模板图");
    }

    [Fact]
    public void RecipeEditorModeCleanup_LeavesDualTemplate_ClearsTaughtImages()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.MaskMinAreaRect,
            Models = ["a.onnx"],
            DualTemplate =
            {
                TemplateABase64 = "aaa==",
                TemplateBBase64 = "bbb==",
                SecondaryRoi = new Roi(0.5, 0.2, 0.4, 0.5),
            },
        };

        var note = RecipeEditorModeCleanup.Apply(editor);

        editor.DualTemplate.TemplateABase64.Should().BeEmpty();
        editor.DualTemplate.TemplateBBase64.Should().BeEmpty();
        editor.DualTemplate.SecondaryRoi.Should().BeNull();
        note.Should().Contain("双模板");
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

    [Fact]
    public void TryCommitUiModels_ModelFree_DoesNotWriteUiSlots()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.DualTemplateCenterLine,
            Models = ["leftover.onnx"],
        };

        RecipeModelSlots.TryCommitUiModels(editor, "ui.onnx", "b.onnx").Should().BeNull();
        editor.Models.Should().Equal("leftover.onnx");
    }

    [Fact]
    public void RecipeEditorModeCleanup_DualBlobToDualTemplate_CarriesSecondaryRoi()
    {
        var roi = new Roi(0.55, 0.2, 0.4, 0.5);
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.DualBlobCenterLine,
            Blob = { SecondaryRoi = roi },
        };

        editor.AngleMode = AngleMode.DualTemplateCenterLine;
        RecipeEditorModeCleanup.Apply(editor, AngleMode.DualBlobCenterLine);

        editor.SecondarySearchRoi.Should().Be(roi);
        editor.DualTemplate.SecondaryRoi.Should().Be(roi);
        editor.Blob.SecondaryRoi.Should().BeNull();
    }

    [Fact]
    public void RecipeEditorModeCleanup_LeavingDualBlob_ClearsSecondaryRoi()
    {
        var editor = new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            Blob = { SecondaryRoi = new Roi(0.5, 0.2, 0.4, 0.5) },
        };

        RecipeEditorModeCleanup.Apply(editor, AngleMode.DualBlobCenterLine);

        editor.Blob.SecondaryRoi.Should().BeNull();
        editor.SecondarySearchRoi.Should().BeNull();
    }
}
