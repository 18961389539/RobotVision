using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

/// <summary>卡尺调试叠加：配方测试开、默认关。</summary>
public sealed class OverlayDrawerTests
{
    [Fact]
    public void DrawPoses_DrawDebug_PaintsCaliperLine()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC3, Scalar.All(0));
        OverlayDrawer.DrawPoses(mat, [PoseWithCaliper()], drawDebug: true);
        Assert.True(HasCyanNear(mat, 25, 20), "开启调试时应画出青色卡尺条");
    }

    [Fact]
    public void DrawPoses_Default_DoesNotPaintCaliperLine()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC3, Scalar.All(0));
        OverlayDrawer.DrawPoses(mat, [PoseWithCaliper()]);
        Assert.False(HasCyanNear(mat, 25, 20), "监控默认不应画卡尺调试");
    }

    private static PixelPose PoseWithCaliper() =>
        new(50, 50, 0, 1)
        {
            Overlay = new PoseOverlay
            {
                DebugLines =
                [
                    new OverlayLine(new PixelPoint(10, 20), new PixelPoint(40, 20), OverlayLineKind.Caliper),
                ],
            },
        };

    private static bool HasCyanNear(Mat mat, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var p = mat.At<Vec3b>(y + dy, x + dx);
            if (p.Item0 > 180 && p.Item1 > 180 && p.Item2 < 80)
                return true;
        }

        return false;
    }
}
