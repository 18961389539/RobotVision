using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

public sealed class FrameOverlayComposerTests
{
    [Fact]
    public void Compose_WithDetectionRoi_PaintsLimeBox()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        using var image = VisionImageCv.FromMat(mat, ownsMat: false);
        var hints = new RecipeDisplayHints(
            DrawDetectionRoi: true,
            DetectionRoi: new Roi(0.1, 0.1, 0.5, 0.5),
            ShowRefineDebug: false);

        FrameOverlayComposer.Compose(image, [], hints);

        Assert.True(CountLime(mat) > 8, "应画出检测 ROI 绿框");
    }

    [Fact]
    public void Compose_ProductionHints_SkipsDetectionRoi()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        using var image = VisionImageCv.FromMat(mat, ownsMat: false);

        FrameOverlayComposer.Compose(image, [], RecipeDisplayHints.Production);

        Assert.Equal(0, CountLime(mat));
    }

    private static int CountLime(Mat mat)
    {
        var n = 0;
        for (var y = 0; y < mat.Height; y++)
        for (var x = 0; x < mat.Width; x++)
        {
            var p = mat.At<Vec3b>(y, x);
            if (p.Item1 > 180 && p.Item0 < 80 && p.Item2 < 80)
                n++;
        }

        return n;
    }
}
