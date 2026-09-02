using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

public sealed class SegmentRefineRuntimeRegistryTests
{
    [Fact]
    public void Default_ContainsAllBuiltInMethods()
    {
        var registry = SegmentRefineRuntimeRegistry.Default;
        Assert.Equal(
        [
            SegmentRefineMethod.Template,
            SegmentRefineMethod.LineFit,
            SegmentRefineMethod.CentroidHoleLine,
            SegmentRefineMethod.CaliperTab,
            SegmentRefineMethod.Sift,
            SegmentRefineMethod.ShapeMatch,
        ], registry.Methods);
    }

    [Fact]
    public void Get_UnknownMethod_FallsBackToLineFit()
    {
        var registry = SegmentRefineRuntimeRegistry.CreateDefault();
        var runtime = registry.Get((SegmentRefineMethod)99);
        Assert.Equal(SegmentRefineMethod.LineFit, runtime.Method);
    }
}
