using OpenCvSharp;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

public sealed class LineFitRefineLineBuilderTests
{
    [Fact]
    public void TryBuildRefineLine_BrightRightEnd_HeadAtPlusLongAxis()
    {
        using var gray = new Mat(60, 200, MatType.CV_8UC1, new Scalar(60));
        gray.SubMat(new Rect(100, 0, 100, 60)).SetTo(new Scalar(200));
        var housing = new HousingFrame(new Point2f(100, 30), 0, 160, 40);

        var line = LineFitRefine.TryBuildRefineLine(gray, housing, 0);

        Assert.NotNull(line);
        Assert.True(line!.HasReliableSignature);
        Assert.True(line.HeadMinusTailGray > 0);
        Assert.True(line.X2 > line.X1);
    }

    [Fact]
    public void TryBuildRefineLine_Symmetric_ReturnsNull()
    {
        using var gray = new Mat(60, 200, MatType.CV_8UC1, new Scalar(128));
        var housing = new HousingFrame(new Point2f(100, 30), 0, 160, 40);
        Assert.Null(LineFitRefine.TryBuildRefineLine(gray, housing, 0));
    }
}
