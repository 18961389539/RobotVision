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
            RefineAngleLoDeg = -3,
            RefineAngleHiDeg = 8,
            UseUprightCrop = false,
            UseEdgeMatch = true,
            AllowCoarseFallback = true,
            TeachPeakScore = 0.88,
            HousingEdgePolarity = HousingEdgePolarity.DarkToBright,
            TabPolarity = TabPolarityLock.PlusShortAxis,
            LineFitSubpixel = true,
            LineFitFuzzyMeasure = true,
            ShapeMatchNumLevels = 3,
            ShapeMatchMinContrast = 12,
            ShapeMatchMetric = ShapeMatchMetric.IgnoreLocalPolarity,
            MaxSecondPeakRatio = 0.92,
            NoFlipConstraint = true,
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
        target.RefineAngleLoDeg.Should().Be(-3);
        target.RefineAngleHiDeg.Should().Be(8);
        target.GetRefineAngleWindow().SpanDeg.Should().Be(11);
        target.UseUprightCrop.Should().BeFalse();
        target.UseEdgeMatch.Should().BeTrue();
        target.AllowCoarseFallback.Should().BeTrue();
        target.TeachPeakScore.Should().Be(0.88);
        target.HousingEdgePolarity.Should().Be(HousingEdgePolarity.DarkToBright);
        target.TabPolarity.Should().Be(TabPolarityLock.PlusShortAxis);
        target.LineFitSubpixel.Should().BeTrue();
        target.LineFitFuzzyMeasure.Should().BeTrue();
        target.ShapeMatchNumLevels.Should().Be(3);
        target.ShapeMatchMinContrast.Should().Be(12);
        target.ShapeMatchMetric.Should().Be(ShapeMatchMetric.IgnoreLocalPolarity);
        target.MaxSecondPeakRatio.Should().Be(0.92);
        target.NoFlipConstraint.Should().BeTrue();
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

    [Fact]
    public void EnsureRefineAngleBounds_LegacyRangeOnly_ExpandsSymmetric()
    {
        var t = new TemplateOptions { RefineRangeDeg = 12 };
        t.EnsureRefineAngleBounds();
        t.RefineAngleLoDeg.Should().Be(-12);
        t.RefineAngleHiDeg.Should().Be(12);
        t.RefineRangeDeg.Should().Be(12);
    }

    [Fact]
    public void Json_PersistsAsymmetricAngleBounds()
    {
        var t = new TemplateOptions { RefineAngleLoDeg = -3, RefineAngleHiDeg = 8, MatchThreshold = 0.55 };
        t.EnsureRefineAngleBounds();
        var json = System.Text.Json.JsonSerializer.Serialize(t);
        var back = System.Text.Json.JsonSerializer.Deserialize<TemplateOptions>(json)!;
        back.EnsureRefineAngleBounds();
        back.RefineAngleLoDeg.Should().Be(-3);
        back.RefineAngleHiDeg.Should().Be(8);
        json.Should().Contain("RefineAngleLoDeg");
    }

    [Fact]
    public void Json_LegacyRangeOnly_FillsLoHi()
    {
        const string json = """{"refineRangeDeg":9,"matchThreshold":0.6}""";
        var t = System.Text.Json.JsonSerializer.Deserialize<TemplateOptions>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        t.EnsureRefineAngleBounds();
        t.RefineAngleLoDeg.Should().Be(-9);
        t.RefineAngleHiDeg.Should().Be(9);
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

    [Fact]
    public void ClearUnusedFields_LeavingMaskTemplate_DropsTeachImageAndPolarity()
    {
        var t = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.Template,
            TemplateImageBase64 = new string('Z', 64),
            Roi = new Roi(0.1, 0.2, 0.3, 0.4),
            RefineLine = new RefineLine(0.1, 0.2, 0.8, 0.2, 12),
            TeachPeakScore = 0.9,
            HousingEdgePolarity = HousingEdgePolarity.BrightToDark,
            TabPolarity = TabPolarityLock.PlusShortAxis,
            ExpectedCount = 2,
            MatchThreshold = 0.71,
        };

        t.ClearUnusedFields(AngleMode.DualCenterLine);

        t.TemplateImageBase64.Should().BeEmpty();
        t.Roi.Should().BeNull();
        t.RefineLine.Should().BeNull();
        t.TeachPeakScore.Should().Be(0);
        t.HousingEdgePolarity.Should().Be(HousingEdgePolarity.Auto);
        t.TabPolarity.Should().Be(TabPolarityLock.Auto);
        t.ExpectedCount.Should().Be(2);
        t.MatchThreshold.Should().Be(0.71);
    }

    [Fact]
    public void ClearUnusedFields_LineFit_DropsImageAndFeatureRoi_KeepsRefineLine()
    {
        var t = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.LineFit,
            TemplateImageBase64 = "abc",
            Roi = new Roi(0.1, 0.2, 0.3, 0.4),
            RefineLine = new RefineLine(0.1, 0.2, 0.8, 0.2, 12),
            HousingEdgePolarity = HousingEdgePolarity.DarkToBright,
            TabPolarity = TabPolarityLock.MinusShortAxis,
        };

        t.ClearUnusedFields(AngleMode.MaskTemplate);

        t.TemplateImageBase64.Should().BeEmpty();
        t.Roi.Should().BeNull();
        t.RefineLine.Should().NotBeNull();
        t.HousingEdgePolarity.Should().Be(HousingEdgePolarity.DarkToBright);
        t.TabPolarity.Should().Be(TabPolarityLock.Auto);
    }

    [Fact]
    public void ClearUnusedFields_Sift_KeepsImage_DropsFeatureRoi()
    {
        var t = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.Sift,
            TemplateImageBase64 = "abc",
            Roi = new Roi(0.1, 0.2, 0.3, 0.4),
        };

        t.ClearUnusedFields(AngleMode.MaskTemplate);

        t.TemplateImageBase64.Should().Be("abc");
        t.Roi.Should().BeNull();
    }
}
