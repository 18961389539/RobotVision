using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

public sealed class AdaptiveCannyTests
{
    [Fact]
    public void WeakContrast_ProducesEdges()
    {
        using var gray = new Mat(80, 120, MatType.CV_8UC1, Scalar.All(128));
        Cv2.Rectangle(gray, new Point(20, 20), new Point(100, 60), Scalar.All(150), -1);
        var (low, high) = MaskTemplateMatcher.AdaptiveCannyThresholds(gray);
        Assert.True(high > low);
        Assert.True(low < 60, $"弱对比度低阈应低于固定 60，实际 {low}");

        using var bgr = new Mat();
        Cv2.CvtColor(gray, bgr, ColorConversionCodes.GRAY2BGR);
        using var edges = MaskTemplateMatcher.ToEdgeMap(bgr);
        using var grayE = new Mat();
        Cv2.CvtColor(edges, grayE, ColorConversionCodes.BGR2GRAY);
        var density = Cv2.CountNonZero(grayE) / (double)(grayE.Rows * grayE.Cols);
        Assert.True(density > 0.002, $"弱对比度边缘密度过低: {density:0.0000}");
    }

    [Fact]
    public void StrongContrast_DoesNotFlood()
    {
        using var bgr = new Mat(80, 120, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(bgr, new Point(15, 15), new Point(105, 65), new Scalar(20, 20, 20), -1);
        using var edges = MaskTemplateMatcher.ToEdgeMap(bgr);
        using var grayE = new Mat();
        Cv2.CvtColor(edges, grayE, ColorConversionCodes.BGR2GRAY);
        var density = Cv2.CountNonZero(grayE) / (double)(grayE.Rows * grayE.Cols);
        Assert.InRange(density, 0.01, 0.25);
    }
}
