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
        Assert.StartsWith("abcd", recipe.ModelSha256[0]);
    }
}
