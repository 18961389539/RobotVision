using System.Text.Json;
using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class RecipeSecondarySearchRoiTests
{
    [Fact]
    public void SecondarySearchRoi_RoutesByAngleMode()
    {
        var roi = new Roi(0.5, 0.2, 0.4, 0.5);
        var blob = new RecipeConfig { AngleMode = AngleMode.DualBlobCenterLine };
        blob.SecondarySearchRoi = roi;
        blob.Blob.SecondaryRoi.Should().Be(roi);
        blob.DualTemplate.SecondaryRoi.Should().BeNull();

        var dual = new RecipeConfig { AngleMode = AngleMode.DualTemplateCenterLine };
        dual.SecondarySearchRoi = roi;
        dual.DualTemplate.SecondaryRoi.Should().Be(roi);
        dual.Blob.SecondaryRoi.Should().BeNull();
    }

    [Fact]
    public void SecondarySearchRoi_IsNotSerializedAtRoot()
    {
        var recipe = new RecipeConfig
        {
            Name = "T",
            CameraId = "cam",
            AngleMode = AngleMode.DualBlobCenterLine,
            SecondarySearchRoi = new Roi(0.5, 0.2, 0.4, 0.5),
        };

        var json = JsonSerializer.Serialize(recipe);
        json.Should().NotContain("SecondarySearchRoi");
        json.Should().Contain("SecondaryRoi");
    }
}
