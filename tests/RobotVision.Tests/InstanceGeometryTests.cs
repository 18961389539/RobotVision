using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class InstanceGeometryTests
{
    [Fact]
    public void Accepts_NoTeachWindow_AlwaysTrue()
    {
        var t = new TemplateOptions();
        Assert.True(InstanceGeometry.Accepts(t, 10, 2));
        Assert.True(InstanceGeometry.Accepts(t, 1e6, 0.1));
    }

    [Fact]
    public void Accepts_AreaOutsideRatio_False()
    {
        var t = new TemplateOptions { TeachAreaPx = 1000, AreaRatioLo = 0.55, AreaRatioHi = 1.8 };
        Assert.False(InstanceGeometry.Accepts(t, 1000 * 0.5, 2));
        Assert.False(InstanceGeometry.Accepts(t, 1000 * 2.0, 2));
        Assert.True(InstanceGeometry.Accepts(t, 1000, 2));
    }

    [Fact]
    public void Accepts_AspectOutsideRatio_False()
    {
        var t = new TemplateOptions { TeachAspect = 2.0, AspectRatioLo = 0.7, AspectRatioHi = 1.45 };
        Assert.False(InstanceGeometry.Accepts(t, 100, 2.0 * 0.5));
        Assert.True(InstanceGeometry.Accepts(t, 100, 2.0));
    }

    [Fact]
    public void EnsureRatioDefaults_ZerosBecomeDefaults()
    {
        var t = new TemplateOptions { AreaRatioLo = 0, AreaRatioHi = 0, AspectRatioLo = 0, AspectRatioHi = 0 };
        InstanceGeometry.EnsureRatioDefaults(t);
        Assert.Equal(InstanceGeometry.DefaultAreaRatioLo, t.AreaRatioLo);
        Assert.Equal(InstanceGeometry.DefaultAreaRatioHi, t.AreaRatioHi);
        Assert.Equal(InstanceGeometry.DefaultAspectRatioLo, t.AspectRatioLo);
        Assert.Equal(InstanceGeometry.DefaultAspectRatioHi, t.AspectRatioHi);
    }

    [Fact]
    public void PolygonArea_Rectangle()
    {
        var area = InstanceGeometry.PolygonArea([(0, 0), (10, 0), (10, 4), (0, 4)]);
        Assert.Equal(40, area, 6);
    }

    [Fact]
    public void DeriveRectangleSides_MatchesAreaAndAspect()
    {
        const double area = 281_935.5;
        const double aspect = 2.182741145059986;
        var (longLen, shortLen) = InstanceGeometry.DeriveRectangleSides(area, aspect);
        Assert.True(longLen > shortLen);
        Assert.Equal(area, longLen * shortLen, 1.0);
        Assert.Equal(aspect, longLen / shortLen, 0.01);
    }
}
