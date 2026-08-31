using OpenCvSharp;
using RobotVision.Teach;
using Xunit;

namespace RobotVision.Tests;

public sealed class TeachTemplateFusionTests
{
    [Fact]
    public void Blend_MedianKillsSaltNoise()
    {
        using var a = new Mat(16, 16, MatType.CV_8UC3, new Scalar(80, 80, 80));
        using var b = a.Clone();
        using var c = a.Clone();
        var idx = c.GetGenericIndexer<Vec3b>();
        idx[8, 8] = new Vec3b(255, 255, 255);
        using var blend = TeachTemplateFusion.Blend([a, b, c]);
        var p = blend.At<Vec3b>(8, 8);
        Assert.True(p.Item0 < 120, "中位融合应滤掉单帧椒盐");
    }

    [Fact]
    public void SameTarget_RejectsDifferentSizeAndContent()
    {
        using var a = new Mat(32, 48, MatType.CV_8UC3, new Scalar(200, 200, 200));
        Cv2.Rectangle(a, new Point(4, 4), new Point(20, 28), new Scalar(10, 10, 10), -1);
        using var b = new Mat(32, 48, MatType.CV_8UC3, new Scalar(200, 200, 200));
        Cv2.Circle(b, new Point(36, 16), 8, new Scalar(10, 10, 10), -1);
        Assert.True(TeachTemplateFusion.SameTarget(a, a));
        Assert.False(TeachTemplateFusion.SameTarget(a, b));
    }

    [Fact]
    public void Median_OddAndEven()
    {
        Assert.Equal(2, TeachTemplateFusion.Median([1, 3, 2]));
        Assert.Equal(2.5, TeachTemplateFusion.Median([1, 4, 2, 3]));
    }
}
