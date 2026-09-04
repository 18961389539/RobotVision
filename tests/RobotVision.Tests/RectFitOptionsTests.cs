using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.Vision;using Xunit;

namespace RobotVision.Tests;

public sealed class RectFitOptionsTests
{
    [Theory]
    [InlineData(HousingEdgePolarity.Auto, false, false, RectEdgePolarity.Any, RectEdgeMeasureMode.Sharp, 2, false)]
    [InlineData(HousingEdgePolarity.BrightToDark, false, false, RectEdgePolarity.BrightToDark, RectEdgeMeasureMode.Sharp, 2, false)]
    [InlineData(HousingEdgePolarity.DarkToBright, true, false, RectEdgePolarity.DarkToBright, RectEdgeMeasureMode.Fuzzy, 2, false)]
    [InlineData(HousingEdgePolarity.DarkToBright, true, true, RectEdgePolarity.DarkToBright, RectEdgeMeasureMode.Fuzzy, 2, true)]
    public void ForLineFit_MapsTemplateFields(
        HousingEdgePolarity edge,
        bool fuzzy,
        bool constrainTeachSize,
        RectEdgePolarity expectedPolarity,
        RectEdgeMeasureMode expectedMode,
        int expectedClip,
        bool expectFixedSize)
    {
        var template = new TemplateOptions
        {
            HousingEdgePolarity = edge,
            LineFitFuzzyMeasure = fuzzy,
            LineFitConstrainTeachSize = constrainTeachSize,
            TeachAreaPx = 281_935.5,
            TeachAspect = 2.182741145059986,
        };

        var options = RectFitOptions.ForLineFit(template);

        options.EdgePolarity.Should().Be(expectedPolarity);
        options.EdgeMeasureMode.Should().Be(expectedMode);
        options.ClipEndPoints.Should().Be(expectedClip);
        if (expectFixedSize)
        {
            var (longLen, shortLen) = InstanceGeometry.DeriveRectangleSides(
                template.TeachAreaPx, template.TeachAspect);
            options.Constraints.FixedLongLenPx.Should().BeApproximately(longLen, 0.5);
            options.Constraints.FixedShortLenPx.Should().BeApproximately(shortLen, 0.5);
        }
        else
        {
            options.Constraints.FixedLongLenPx.Should().BeNull();
            options.Constraints.FixedShortLenPx.Should().BeNull();
        }
    }
}
