using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public class OutputOffsetTests
{
    [Fact]
    public void Apply_AddsTranslationAndNormalizesAngle()
    {
        var offset = new OutputOffsetOptions { X = 0.15, Y = -0.2, RzDeg = 2.5 };
        var pose = offset.Apply(new RobotPose(10, 20, 179));

        Assert.Equal(10.15, pose.X, 9);
        Assert.Equal(19.8, pose.Y, 9);
        Assert.Equal(-178.5, pose.AngleDeg, 9);
    }

    [Fact]
    public void Apply_Zero_IsIdentity()
    {
        var pose = new RobotPose(1.1, 2.2, -3.3);
        var same = new OutputOffsetOptions().Apply(pose);

        Assert.Equal(pose.X, same.X);
        Assert.Equal(pose.Y, same.Y);
        Assert.Equal(pose.AngleDeg, same.AngleDeg);
    }

    [Fact]
    public void Validate_RejectsHugeOffset()
    {
        var recipe = new RecipeConfig
        {
            Name = "A01",
            CameraId = "cam",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
            OutputOffset = { X = 101 },
        };
        Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
    }

    [Fact]
    public void Clone_CopiesOffsetAndHashes()
    {
        var recipe = new RecipeConfig
        {
            Name = "A01",
            CameraId = "cam",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
            OutputOffset = { X = 0.1, Y = 0.2, RzDeg = 1 },
            ModelSha256 = ["abcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcd"],
            StationSha256 = "1111111111111111111111111111111111111111111111111111111111111111",
        };
        RecipeLoader.Validate(recipe);
        var clone = recipe.Clone();
        clone.OutputOffset.X = 9;
        clone.ModelSha256[0] = "x";
        Assert.Equal(0.1, recipe.OutputOffset.X);
        Assert.StartsWith("abcd", recipe.ModelSha256[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NullOutputOffset_TreatedAsZero()
    {
        var recipe = new RecipeConfig
        {
            Name = "A01",
            CameraId = "cam",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
            OutputOffset = null!,
        };
        RecipeLoader.Validate(recipe);
        Assert.NotNull(recipe.OutputOffset);
        Assert.True(recipe.OutputOffset.IsZero);
        var clone = recipe.Clone();
        Assert.True(clone.OutputOffset.IsZero);
    }

    [Fact]
    public void SuggestDelta_MedianMinusTeach()
    {
        var teach = new RobotPose(10, 20, 0);
        var ok = new[]
        {
            new RobotPose(10.2, 20.1, 1),
            new RobotPose(10.4, 20.3, 3),
            new RobotPose(10.6, 20.5, 5),
            new RobotPose(10.1, 20.0, 0.5),
            new RobotPose(10.3, 20.2, 2),
            new RobotPose(10.5, 20.4, 4),
            new RobotPose(10.7, 20.6, 6),
            new RobotPose(10.8, 20.8, 7),
        };
        var delta = OutputOffsetOptions.SuggestDelta(teach, ok);
        Assert.NotNull(delta);
        Assert.InRange(delta!.X, 0.3, 0.5);
        Assert.InRange(delta.Y, 0.2, 0.5);
    }

    [Fact]
    public void SuggestDelta_TooFew_ReturnsNull()
    {
        Assert.Null(OutputOffsetOptions.SuggestDelta(new RobotPose(0, 0, 0), [new RobotPose(1, 1, 0)]));
    }

    [Fact]
    public void ApplySuggestedDelta_SecondPassNearZero()
    {
        var offset = new OutputOffsetOptions { X = 0.1, TeachX = 10, TeachY = 20, TeachRzDeg = 0 };
        var teach = new RobotPose(10, 20, 0);
        var ok = Enumerable.Range(0, 8).Select(i => new RobotPose(10.4, 20.2, 1)).ToList();
        var delta = OutputOffsetOptions.SuggestDelta(teach, ok)!;
        offset.ApplySuggestedDelta(delta, teach, ok);
        var again = OutputOffsetOptions.SuggestDelta(
            new RobotPose(offset.TeachX!.Value, offset.TeachY!.Value, offset.TeachRzDeg!.Value), ok);
        Assert.NotNull(again);
        Assert.Equal(0, again!.X, 6);
        Assert.Equal(0, again.Y, 6);
        Assert.Equal(0, again.RzDeg, 6);
    }

    [Fact]
    public void Clone_CopiesTeachOutput()
    {
        var recipe = new RecipeConfig
        {
            Name = "A01",
            CameraId = "cam",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
            OutputOffset = { X = 0.1, TeachX = 12, TeachY = 34, TeachRzDeg = 5 },
        };
        RecipeLoader.Validate(recipe);
        var clone = recipe.Clone();
        clone.OutputOffset.TeachX = 0;
        Assert.Equal(12, recipe.OutputOffset.TeachX);
    }

    [Fact]
    public void ClearTeachOutput_RemovesTeachCoordinates_KeepsDelta()
    {
        var offset = new OutputOffsetOptions { X = 0.2, Y = -0.1, TeachX = 10, TeachY = 20, TeachRzDeg = 3 };
        offset.ClearTeachOutput();

        Assert.False(offset.HasTeachOutput);
        Assert.Equal(0.2, offset.X);
        Assert.Equal(-0.1, offset.Y);
        Assert.Null(offset.TeachX);
    }
}
