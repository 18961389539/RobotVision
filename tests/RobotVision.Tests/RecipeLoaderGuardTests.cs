using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class RecipeLoaderGuardTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rv_guard_" + Guid.NewGuid().ToString("N"));

    public RecipeLoaderGuardTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void Write(string name, string json) =>
        File.WriteAllText(Path.Combine(_folder, name), json);

    [Theory]
    [InlineData("A01")]
    [InlineData("recipe-2")]
    [InlineData("my_recipe_2026")]
    [InlineData("9x9")]
    public void IsValidRecipeName_AcceptsNormalNames(string name)
        => Assert.True(RecipeLoader.IsValidRecipeName(name));

    [Theory]
    [InlineData("..\\..\\windows")]
    [InlineData("../etc/passwd")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a b")]
    [InlineData("")]
    [InlineData("配.方")]
    public void IsValidRecipeName_RejectsPathTraversalAndIllegal(string name)
        => Assert.False(RecipeLoader.IsValidRecipeName(name));

    [Fact]
    public void Get_TraversalName_ThrowsInsteadOfTouchingDisk()
    {
        var loader = new RecipeLoader(_folder);
        // 目录里真实存在一个文件，但通过 .. 引用必须被拒绝
        Write("A01.json", """{ "cameraId": "c", "models": ["m.onnx"] }""");

        Assert.Throws<RecipeNotFoundException>(() => loader.Get("..\\A01"));
        Assert.Throws<RecipeNotFoundException>(() => loader.Get("..."));
        Assert.Throws<RecipeNotFoundException>(() => loader.Get(""));
    }

    [Fact]
    public void Validate_MissingCameraId_Throws()
    {
        var recipe = new RecipeConfig { Name = "T1", Models = ["m.onnx"] };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_EmptyModels_Throws()
    {
        var recipe = new RecipeConfig { Name = "T1", CameraId = "cam", Models = [] };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_DualCenterLineRequiresTwoModels()
    {
        var one = new RecipeConfig
        {
            Name = "T1", CameraId = "cam", AngleMode = AngleMode.DualCenterLine, Models = ["a.onnx"],
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(one));

        var two = new RecipeConfig
        {
            Name = "T1", CameraId = "cam", AngleMode = AngleMode.DualCenterLine, Models = ["a.onnx", "b.onnx"],
        };
        RecipeLoader.Validate(two);
    }

    [Fact]
    public void Validate_ConfidenceOutOfRange_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1", CameraId = "cam", Models = ["m.onnx"], Confidence = 1.5,
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_SameKeypointIndices_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1", CameraId = "cam", AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"], Keypoint = new KeypointOptions { IndexA = 1, IndexB = 1 },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Get_InvalidRecipe_ThrowsAndNotCached()
    {
        Write("BAD.json", """{ "cameraId": "", "models": ["m.onnx"] }""");
        var loader = new RecipeLoader(_folder);

        Assert.Throws<InvalidRecipeException>(() => loader.Get("BAD"));
        Assert.Equal(0, loader.LoadedCount);
    }

    [Fact]
    public void LoadAll_ReportsBadRecipeButStillLoadsGoodOnes()
    {
        Write("GOOD.json", """
            { "cameraId": "cam", "angleMode": "MaskMinAreaRect", "models": ["m.onnx"] }
            """);
        Write("BAD.json", """{ "cameraId": "cam", "models": [] }""");
        var loader = new RecipeLoader(_folder);

        var errors = loader.LoadAll();

        Assert.Single(errors);
        Assert.Equal("BAD", errors[0].Name);
        Assert.Equal(1, loader.LoadedCount);
        Assert.NotNull(loader.Get("GOOD"));
    }

    [Fact]
    public void Validate_MaskTemplate_RequiresExactlyOneModel()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx", "b.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.LineFit },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_MaskTemplate_TemplateMethod_RequiresTaughtImage()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.Template },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_MaskTemplate_CaliperTab_DoesNotRequireTemplate()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.CaliperTab },
        };
        RecipeLoader.Validate(recipe);
    }

    [Fact]
    public void Validate_MaskTemplate_MatchThresholdOutOfRange_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.LineFit, MatchThreshold = 1.5 },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_MaskTemplate_TeachPeakScoreOutOfRange_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.LineFit, TeachPeakScore = 1.2 },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void TemplateOptions_Clone_CopiesPolarityAndTeachPeak()
    {
        var src = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.CaliperTab,
            TeachPeakScore = 0.91,
            MatchThreshold = 0.77,
            HousingEdgePolarity = HousingEdgePolarity.DarkToBright,
            TabPolarity = TabPolarityLock.MinusShortAxis,
            ExpectedCount = 2,
            TeachAreaPx = 1200,
            TeachAspect = 2.4,
        };
        var clone = src.Clone();
        Assert.Equal(0.91, clone.TeachPeakScore);
        Assert.Equal(0.77, clone.MatchThreshold);
        Assert.Equal(HousingEdgePolarity.DarkToBright, clone.HousingEdgePolarity);
        Assert.Equal(TabPolarityLock.MinusShortAxis, clone.TabPolarity);
        Assert.Equal(2, clone.ExpectedCount);
        Assert.Equal(1200, clone.TeachAreaPx);
        Assert.Equal(2.4, clone.TeachAspect);
    }

    [Fact]
    public void MatchThresholdFromTeachPeak_Is85PercentClamped()
    {
        Assert.Equal(0.40, TemplateOptions.MatchThresholdFromTeachPeak(0.2), 2);
        Assert.InRange(TemplateOptions.MatchThresholdFromTeachPeak(0.80), 0.67, 0.69);
        Assert.Equal(0.85, TemplateOptions.MatchThresholdFromTeachPeak(1.0), 2);
        Assert.InRange(TemplateOptions.MatchThresholdFromTeachPeak(0.80, peakSharpness: 0.02), 0.73, 0.76);
    }

    [Fact]
    public void Validate_EccentricTool_WithMinAreaRect_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            StationId = "st1",
            AngleMode = AngleMode.MaskMinAreaRect,
            Models = ["a.onnx"],
            RotationCompensation = RotationCompensationMode.EccentricTool,
        };
        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("偏心", ex.Message);
    }

    [Fact]
    public void Validate_EccentricTool_WithLineFit_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            StationId = "st1",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            RotationCompensation = RotationCompensationMode.EccentricTool,
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.LineFit },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Validate_EccentricTool_WithCaliperTab_Ok()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            StationId = "st1",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            RotationCompensation = RotationCompensationMode.EccentricTool,
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.CaliperTab },
        };
        RecipeLoader.Validate(recipe);
    }

    [Fact]
    public void Validate_MaskTemplate_ExpectedCountOutOfRange_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "T1",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["a.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.CaliperTab, ExpectedCount = 21 },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void HasUndirectedAngle_MinAreaRectAndLineFit()
    {
        Assert.True(RecipeLoader.HasUndirectedAngle(new RecipeConfig { AngleMode = AngleMode.MaskMinAreaRect }));
        Assert.True(RecipeLoader.HasUndirectedAngle(new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.LineFit },
        }));
        Assert.False(RecipeLoader.HasUndirectedAngle(new RecipeConfig
        {
            AngleMode = AngleMode.MaskTemplate,
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.CaliperTab },
        }));
    }
}

