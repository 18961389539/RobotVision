using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests;

public sealed class RecipeCompareTests
{
  private static string HugeTemplate => new('A', 512 * 1024);

    [Fact]
    public void Same_IgnoresTemplateImageInJson_ButComparesTemplateString()
    {
        var left = Sample("r1", HugeTemplate, 0.55);
        var right = left.Clone();
        RecipeCompare.Same(left, right).Should().BeTrue();

        right.Template.MatchThreshold = 0.56;
        RecipeCompare.Same(left, right).Should().BeFalse();

        right = left.Clone();
        right.Template.TemplateImageBase64 = HugeTemplate + "x";
        RecipeCompare.Same(left, right).Should().BeFalse();
    }

    [Fact]
    public void BodyFingerprint_DoesNotEmbedTemplateImage()
    {
        var recipe = Sample("r1", HugeTemplate, 0.5);
        var fingerprint = RecipeCompare.BodyFingerprint(recipe);
        fingerprint.Should().NotContain(HugeTemplate);
        fingerprint.Should().Contain("MatchThreshold");
    }

    [Fact]
    public void GrabOriginChanged_DetectsMethodAndFeatureRoi()
    {
        var a = Sample("r1", "img", 0.5);
        var b = a.Clone();
        RecipeCompare.GrabOriginChanged(a, b).Should().BeFalse();

        b.Template.RefineMethod = SegmentRefineMethod.CaliperTab;
        RecipeCompare.GrabOriginChanged(a, b).Should().BeTrue();

        b = a.Clone();
        b.Template.Roi = new RobotVision.Core.Models.Roi(0.1, 0.2, 0.4, 0.05);
        RecipeCompare.GrabOriginChanged(a, b).Should().BeTrue();
    }

    private static RecipeConfig Sample(string name, string templateImage, double matchThreshold) => new()
    {
        Name = name,
        CameraId = "cam",
        AngleMode = AngleMode.MaskTemplate,
        Models = ["m.onnx"],
        Template = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.Template,
            TemplateImageBase64 = templateImage,
            MatchThreshold = matchThreshold,
        },
    };
}
