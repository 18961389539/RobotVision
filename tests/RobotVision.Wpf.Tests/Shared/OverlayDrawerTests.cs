using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
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

    [Fact]
    public void DrawPoses_MatchWindow_PaintsGoldBox()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC3, Scalar.All(0));
        var window = PoseOverlay.TemplateMatchWindow(40, 40, 0, 30, 20);
        OverlayDrawer.DrawPoses(mat, [new PixelPose(40, 40, 0, 0.9)
        {
            Overlay = new PoseOverlay { MatchWindow = window },
        }]);
        Assert.True(HasGoldNear(mat, 25, 30), "匹配窗应为金色框");
    }

    private static bool HasGoldNear(Mat mat, int x, int y)
    {
        for (var dy = -3; dy <= 3; dy++)
        for (var dx = -3; dx <= 3; dx++)
        {
            var xx = x + dx;
            var yy = y + dy;
            if ((uint)xx >= (uint)mat.Width || (uint)yy >= (uint)mat.Height)
                continue;
            var p = mat.At<Vec3b>(yy, xx);
            if (p.Item2 > 180 && p.Item1 > 140 && p.Item0 < 80)
                return true;
        }

        return false;
    }

    [Fact]
    public void DrawPoses_DrawDebug_PaintsShapeInlierDot()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC3, Scalar.All(0));
        OverlayDrawer.DrawPoses(mat, [PoseWithShapeDots()], drawDebug: true);
        Assert.True(HasCyanNear(mat, 30, 30), "开启调试时应画出青色形状命中点");
    }

    [Fact]
    public void DrawPoses_Default_DoesNotPaintShapeDot()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC3, Scalar.All(0));
        OverlayDrawer.DrawPoses(mat, [PoseWithShapeDots()]);
        Assert.False(HasCyanNear(mat, 30, 30), "默认不应画形状调试点");
    }

    private static PixelPose PoseWithShapeDots() =>
        new(50, 50, 0, 1)
        {
            Overlay = new PoseOverlay
            {
                DebugDots =
                [
                    new OverlayDot(new PixelPoint(30, 30), OverlayDotKind.Inlier),
                ],
            },
        };

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

    [Fact]
    public void DrawPoses_Unusable_PaintsNgLabel()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC3, Scalar.All(0));
        OverlayDrawer.DrawPoses(mat, [new PixelPose(40, 40, 0, 0.12) { Usable = false }]);
        Assert.True(HasOrangeRedNear(mat, 58, 14), "不可用位姿应标 NG（橙红字）");
    }

    [Fact]
    public void DrawPoses_ScoreLabel_DoesNotCoverPeak()
    {
        using var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.All(200));
        OverlayDrawer.DrawPoses(mat, [new PixelPose(60, 60, 0, 1.0)]);
        Assert.False(IsMostlyDark(mat, 60, 60), "分数标签不应盖住匹配峰十字");
    }

    [Fact]
    public void DrawPoses_SegmentScore_PaintsOnBoundingBox()
    {
        using var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.All(200));
        OverlayDrawer.DrawPoses(mat, [new PixelPose(50, 60, 0, 0.91)
        {
            SegmentScore = 0.77,
            Overlay = new PoseOverlay
            {
                Boxes = [new PixelRect(10, 30, 80, 50)],
            },
        }]);
        Assert.True(HasDarkNear(mat, 14, 16), "分割分应写在外接矩形上方");
    }

    [Fact]
    public void DrawNormalizedRoi_PaintsOrangeBox()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        OverlayDrawer.DrawNormalizedRoi(mat, new Roi(0.1, 0.2, 0.3, 0.4), "特征");
        Assert.True(CountOrange(mat) > 8, "特征 ROI 应为橙色框");
    }

    [Fact]
    public void DrawNormalizedRoi_GoldLabel_UsesDarkBackingNotGoldText()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC3, new Scalar(220, 220, 220));
        OverlayDrawer.DrawNormalizedRoi(mat, new Roi(0.2, 0.3, 0.4, 0.3), "建议", Scalar.Gold);
        Assert.True(HasDarkNear(mat, 20, 28), "金框标签应有深色底");
        Assert.True(HasLightTextNear(mat, 20, 28), "金框标签应为浅色字");
    }

    private static int CountOrange(Mat mat)
    {
        var n = 0;
        for (var y = 0; y < mat.Height; y++)
        for (var x = 0; x < mat.Width; x++)
        {
            var p = mat.At<Vec3b>(y, x);
            if (p.Item2 > 120 && p.Item0 < 80 && p.Item1 > 80)
                n++;
        }

        return n;
    }

    private static bool HasOrangeNear(Mat mat, int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        for (var dx = -2; dx <= 2; dx++)
        {
            var xx = x + dx;
            var yy = y + dy;
            if ((uint)xx >= (uint)mat.Width || (uint)yy >= (uint)mat.Height)
                continue;
            var p = mat.At<Vec3b>(yy, xx);
            if (p.Item2 > 120 && p.Item0 < 80 && p.Item1 > 80)
                return true;
        }

        return false;
    }

    private static bool IsMostlyDark(Mat mat, int x, int y)
    {
        var dark = 0;
        var n = 0;
        for (var dy = -3; dy <= 3; dy++)
        for (var dx = -3; dx <= 3; dx++)
        {
            var xx = x + dx;
            var yy = y + dy;
            if ((uint)xx >= (uint)mat.Width || (uint)yy >= (uint)mat.Height)
                continue;
            n++;
            var p = mat.At<Vec3b>(yy, xx);
            if (p.Item0 < 50 && p.Item1 < 50 && p.Item2 < 50)
                dark++;
        }

        return n > 0 && dark >= n / 2;
    }

    private static bool HasOrangeRedNear(Mat mat, int x, int y)
    {
        for (var dy = -8; dy <= 8; dy++)
        for (var dx = -8; dx <= 8; dx++)
        {
            var xx = x + dx;
            var yy = y + dy;
            if ((uint)xx >= (uint)mat.Width || (uint)yy >= (uint)mat.Height)
                continue;
            var p = mat.At<Vec3b>(yy, xx);
            if (p.Item2 > 150 && p.Item1 < 120 && p.Item0 < 80)
                return true;
        }

        return false;
    }

    private static bool HasDarkNear(Mat mat, int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        for (var dx = -4; dx <= 4; dx++)
        {
            var xx = x + dx;
            var yy = y + dy;
            if ((uint)xx >= (uint)mat.Width || (uint)yy >= (uint)mat.Height)
                continue;
            var p = mat.At<Vec3b>(yy, xx);
            if (p.Item0 < 60 && p.Item1 < 60 && p.Item2 < 60)
                return true;
        }

        return false;
    }

    private static bool HasLightTextNear(Mat mat, int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        for (var dx = -4; dx <= 4; dx++)
        {
            var xx = x + dx;
            var yy = y + dy;
            if ((uint)xx >= (uint)mat.Width || (uint)yy >= (uint)mat.Height)
                continue;
            var p = mat.At<Vec3b>(yy, xx);
            if (p.Item0 > 200 && p.Item1 > 200 && p.Item2 > 200)
                return true;
        }

        return false;
    }

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
