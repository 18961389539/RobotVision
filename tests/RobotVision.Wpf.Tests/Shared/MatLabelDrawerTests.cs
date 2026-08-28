using FluentAssertions;
using OpenCvSharp;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

public sealed class MatLabelDrawerTests
{
    [Fact]
    public void DrawBaseline_RendersChinese_NotQuestionMarks()
    {
        using var mat = new Mat(80, 160, MatType.CV_8UC3, new Scalar(200, 200, 200));
        MatLabelDrawer.DrawBaseline(mat, new Point(12, 24), "建议", 13);

        var dark = 0;
        var light = 0;
        for (var y = 0; y < 40; y++)
        for (var x = 0; x < 80; x++)
        {
            var p = mat.At<Vec3b>(y, x);
            if (p.Item0 < 60 && p.Item1 < 60 && p.Item2 < 60)
                dark++;
            if (p.Item0 > 200 && p.Item1 > 200 && p.Item2 > 200)
                light++;
        }

        dark.Should().BeGreaterThan(20, "应有深色标签底");
        light.Should().BeGreaterThan(20, "应有浅色笔画");
    }
}
