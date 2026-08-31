using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Geometry;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

/// <summary>分割轮廓坐标：ContourLocal 须相对包围盒，绘制/策略层会加 box 原点还原全图坐标。</summary>
public class SegmentationContourCoordinateTests
{
    [Fact]
    public void ContourLocal_MinusBoxOrigin_RestoresGlobalWhenAddedBack()
    {
        var box = new PixelBox(100, 80, 200, 150);
        const int globalX = 150;
        const int globalY = 120;

        // YoloDotNetEngine 归一化逻辑
        var local = new ImagePoint(globalX - box.X, globalY - box.Y);
        Assert.Equal(50, local.X);
        Assert.Equal(40, local.Y);

        // ModelTestOverlay / 策略层还原全图坐标
        Assert.Equal(globalX, local.X + box.X);
        Assert.Equal(globalY, local.Y + box.Y);
    }

    [Fact]
    public void InstanceSegmentation_ContourLocal_IsBoxRelative_NotGlobal()
    {
        var box = new PixelBox(10, 20, 100, 80);
        var seg = new InstanceSegmentation(
            box, 0.9, "x",
            [new ImagePoint(30, 40), new ImagePoint(60, 70)],
            []);

        foreach (var p in seg.ContourLocal)
        {
            Assert.InRange(p.X, 0, box.Width);
            Assert.InRange(p.Y, 0, box.Height);
        }
    }

    /// <summary>
    /// 修复前：YoloDotNet 全图轮廓被当作 ContourLocal，策略层再加 box 原点 →
    /// 像素位姿整体偏移 (box.X, box.Y)，机器人坐标随之错误。
    /// </summary>
    [Fact]
    public void WrongGlobalContourAsLocal_ShiftsPixelPose()
    {
        var box = new PixelBox(100, 80, 200, 150);
        // 目标在全图 (120,100)-(180,160) 附近
        ImagePoint[] global = [
            new(120, 100), new(180, 100), new(180, 160), new(120, 160),
        ];
        ImagePoint[] correctLocal = global.Select(p => new ImagePoint(p.X - box.X, p.Y - box.Y)).ToArray();

        var correctCenter = PoseCenterFromContour(box, correctLocal);
        var wrongCenter = PoseCenterFromContour(box, global); // bug：全图坐标当局部存

        Assert.Equal(box.X, wrongCenter.X - correctCenter.X, 1);
        Assert.Equal(box.Y, wrongCenter.Y - correctCenter.Y, 1);
    }

    private static ImagePoint PoseCenterFromContour(PixelBox box, ImagePoint[] contourLocal)
    {
        var points = new Point2f[contourLocal.Length];
        for (var i = 0; i < contourLocal.Length; i++)
            points[i] = new Point2f((float)(contourLocal[i].X + box.Left), (float)(contourLocal[i].Y + box.Top));
        var (center, _) = MinAreaRectGeometry.LongAxis(points);
        return center;
    }
}
