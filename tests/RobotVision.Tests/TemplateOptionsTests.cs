using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class TemplateOptionsTests
{
    [Fact]
    public void CopyTo_CopiesAllEditableFields()
    {
        var source = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.ShapeMatch,
            TemplateImageBase64 = "abc",
            Roi = new Roi(0.1, 0.2, 0.3, 0.4),
            MatchThreshold = 0.71,
            RefineRangeDeg = 9,
            UseUprightCrop = false,
            UseEdgeMatch = true,
            AllowCoarseFallback = true,
            TeachPeakScore = 0.88,
            HousingEdgePolarity = HousingEdgePolarity.DarkToBright,
            TabPolarity = TabPolarityLock.PlusShortAxis,
            ExpectedCount = 2,
            RefinePolicyOrder = [SegmentRefineMethod.CaliperTab, SegmentRefineMethod.Template],
            TeachAreaPx = 1200,
            TeachAspect = 1.8,
            AreaRatioLo = 0.55,
            AreaRatioHi = 1.7,
            AspectRatioLo = 0.8,
            AspectRatioHi = 1.3,
        };
        var target = new TemplateOptions { MatchThreshold = 0.2 };

        source.CopyTo(target);

        target.RefineMethod.Should().Be(SegmentRefineMethod.ShapeMatch);
        target.TemplateImageBase64.Should().Be("abc");
        target.Roi.Should().BeEquivalentTo(new Roi(0.1, 0.2, 0.3, 0.4));
        target.MatchThreshold.Should().Be(0.71);
        target.RefineRangeDeg.Should().Be(9);
        target.UseUprightCrop.Should().BeFalse();
        target.UseEdgeMatch.Should().BeTrue();
        target.AllowCoarseFallback.Should().BeTrue();
        target.TeachPeakScore.Should().Be(0.88);
        target.HousingEdgePolarity.Should().Be(HousingEdgePolarity.DarkToBright);
        target.TabPolarity.Should().Be(TabPolarityLock.PlusShortAxis);
        target.ExpectedCount.Should().Be(2);
        target.RefinePolicyOrder.Should().Equal(SegmentRefineMethod.CaliperTab, SegmentRefineMethod.Template);
        target.TeachAreaPx.Should().Be(1200);
        target.TeachAspect.Should().Be(1.8);
        target.AreaRatioLo.Should().Be(0.55);
        target.AreaRatioHi.Should().Be(1.7);
        target.AspectRatioLo.Should().Be(0.8);
        target.AspectRatioHi.Should().Be(1.3);
    }

    [Fact]
    public void Clone_WithoutTemplateImage_SkipsBase64Copy()
    {
        var source = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.Template,
            TemplateImageBase64 = new string('Z', 4096),
            MatchThreshold = 0.66,
        };

        var copy = source.Clone(includeTemplateImage: false);

        copy.MatchThreshold.Should().Be(0.66);
        copy.TemplateImageBase64.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.4, 0.1, true)]
    [InlineData(0.1, 0.4, true)]
    [InlineData(0.3, 0.3, false)]
    [InlineData(0.2, 0.1, false)]
    public void IsFlatFeatureRoi_UsesAspectThreshold(double w, double h, bool expected)
    {
        TemplateOptions.IsFlatFeatureRoi(new Roi(0.1, 0.1, w, h)).Should().Be(expected);
    }

    [Fact]
    public void IsFlatFeatureRoi_NullOrDegenerate_IsFalse()
    {
        TemplateOptions.IsFlatFeatureRoi(null).Should().BeFalse();
        TemplateOptions.IsFlatFeatureRoi(new Roi(0, 0, 0, 0.5)).Should().BeFalse();
    }
}
