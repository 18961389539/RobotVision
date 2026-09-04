using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests;

public sealed class RecipeEditorValidatorTests
{
    [Fact]
    public void TryValidateForTrigger_MaskTemplateWithoutTeach_ReturnsError()
    {
        var recipe = new RecipeConfig
        {
            Name = "A",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["m.onnx"],
            Template = { RefineMethod = SegmentRefineMethod.Template },
        };

        RecipeEditorValidator.TryValidateForTrigger(recipe).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryValidateForTrigger_DualBlob_AllowsEmptyModels()
    {
        var recipe = new RecipeConfig
        {
            Name = "A",
            CameraId = "cam",
            AngleMode = AngleMode.DualBlobCenterLine,
            Models = [],
        };

        RecipeEditorValidator.TryValidateForTrigger(recipe).Should().BeNull();
    }

    [Fact]
    public void TryValidateForTrigger_DualTemplate_WithoutTeach_ReturnsError()
    {
        var recipe = new RecipeConfig
        {
            Name = "A",
            CameraId = "cam",
            AngleMode = AngleMode.DualTemplateCenterLine,
            Models = [],
        };

        RecipeEditorValidator.TryValidateForTrigger(recipe).Should().Contain("示教");
    }

    [Fact]
    public void TryValidateForTrigger_DualTemplate_SecondaryRoiWithoutPrimary_ReturnsError()
    {
        var recipe = new RecipeConfig
        {
            Name = "A",
            CameraId = "cam",
            AngleMode = AngleMode.DualTemplateCenterLine,
            Models = [],
            DualTemplate =
            {
                TemplateABase64 = ".",
                TemplateBBase64 = ".",
                SecondaryRoi = new Roi(0.55, 0.2, 0.4, 0.6),
            },
        };

        RecipeEditorValidator.TryValidateForTrigger(recipe).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryValidateForTrigger_DualBlob_SecondaryRoiWithoutPrimary_ReturnsError()
    {
        var recipe = new RecipeConfig
        {
            Name = "A",
            CameraId = "cam",
            AngleMode = AngleMode.DualBlobCenterLine,
            Models = [],
            Blob = { SecondaryRoi = new Roi(0.55, 0.2, 0.4, 0.6) },
        };

        RecipeEditorValidator.TryValidateForTrigger(recipe).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryValidateForSave_DualCenterLineOneModel_ReturnsError()
    {
        var dir = new TestInfra.TempDir("rv_val");
        try
        {
            var loader = new RecipeLoader(dir.CreateSub("recipes"));
            var recipe = new RecipeConfig
            {
                Name = "A",
                CameraId = "cam",
                AngleMode = AngleMode.DualCenterLine,
                Models = ["m.onnx"],
            };

            RecipeEditorValidator.TryValidateForSave(recipe, loader).Should().Contain("2 个模型");
        }
        finally
        {
            dir.Dispose();
        }
    }

    [Fact]
    public void TryValidateForTrigger_WithReferenceValidator_ReturnsReferenceError()
    {
        var dir = new TestInfra.TempDir("rv_val_ref");
        try
        {
            var loader = new RecipeLoader(dir.CreateSub("recipes"))
            {
                ReferenceValidator = _ => new RecipeReferenceError("相机未注册", VisionErrorCode.CameraNotRegistered),
            };
            var recipe = new RecipeConfig
            {
                Name = "A",
                CameraId = "cam",
                AngleMode = AngleMode.DualBlobCenterLine,
                Models = [],
            };

            RecipeEditorValidator.TryValidateForTrigger(recipe).Should().BeNull();
            RecipeEditorValidator.TryValidateForTrigger(recipe, loader).Should().Contain("相机未注册");
        }
        finally
        {
            dir.Dispose();
        }
    }
}
