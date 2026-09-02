using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Vision.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>LineFit 有向角判决（示教基准线消 180°）单测。</summary>
public sealed class LineFitHeadingTests
{
    private static HousingFrame Frame(double cx, double cy, double longLen, double shortLen) =>
        new(new Point2f((float)cx, (float)cy), 0.0, longLen, shortLen);

    // 长轴水平（undirected=0）：右端亮、左端暗的合成条。中心 (100,30)。
    private static Mat HalfBrightHalfDark()
    {
        var img = new Mat(60, 200, MatType.CV_8UC1, new Scalar(0));
        img.SubMat(new Rect(0, 0, 100, 60)).SetTo(new Scalar(60));
        img.SubMat(new Rect(100, 0, 100, 60)).SetTo(new Scalar(200));
        return img;
    }

    [Fact]
    public void NoLine_KeepsUndirected()
    {
        using var gray = HalfBrightHalfDark();
        var r = LineFitHeading.Resolve(gray, Frame(100, 30, 160, 40), 0.0, line: null);
        Assert.False(r.Resolved);
        Assert.Equal(0.0, r.DirectedDeg);
    }

    [Fact]
    public void HeadBrighter_TaughtPositive_ResolvesToZero()
    {
        using var gray = HalfBrightHalfDark();
        // 示教：头(P2)在亮端 → head − tail > 0
        var line = new RefineLine(0.22, 0.5, 0.78, 0.5, HeadMinusTailGray: 140);
        var r = LineFitHeading.Resolve(gray, Frame(100, 30, 160, 40), 0.0, line);

        Assert.True(r.Resolved);
        Assert.Equal(0.0, r.DirectedDeg, 3);
        Assert.True(r.HeadPoint.X > 100); // 头落在亮端（+x）
    }

    [Fact]
    public void FlippedPart_HeadDarker_ResolvesTo180()
    {
        using var gray = HalfBrightHalfDark();
        // 示教：头(P2)在暗端 → head − tail < 0；当前实测仍是 +端亮，判为翻转 180°
        var line = new RefineLine(0.78, 0.5, 0.22, 0.5, HeadMinusTailGray: -140);
        var r = LineFitHeading.Resolve(gray, Frame(100, 30, 160, 40), 0.0, line);

        Assert.True(r.Resolved);
        Assert.Equal(180.0, Math.Abs(r.DirectedDeg), 3);
        Assert.True(r.HeadPoint.X < 100); // 头落在暗端（−x）
    }

    [Fact]
    public void SymmetricPart_Unresolved_KeepsUndirected_WithNote()
    {
        using var gray = new Mat(60, 200, MatType.CV_8UC1, new Scalar(128));
        var line = new RefineLine(0.22, 0.5, 0.78, 0.5, HeadMinusTailGray: 60);
        var r = LineFitHeading.Resolve(gray, Frame(100, 30, 160, 40), 0.0, line);

        Assert.False(r.Resolved);
        Assert.Equal(0.0, r.DirectedDeg);
        Assert.NotNull(r.Note);
    }

    [Fact]
    public void WeakTaughtSignature_Unresolved()
    {
        using var gray = HalfBrightHalfDark();
        var line = new RefineLine(0.22, 0.5, 0.78, 0.5, HeadMinusTailGray: 1.0); // < MinFlipContrastGray
        var r = LineFitHeading.Resolve(gray, Frame(100, 30, 160, 40), 0.0, line);
        Assert.False(r.Resolved);
    }

    [Fact]
    public void VerticalAxis_ResolvesHeadAtBrightEnd()
    {
        // 长轴竖直（undirected=90）：下端亮、上端暗。
        using var img = new Mat(200, 60, MatType.CV_8UC1, new Scalar(0)); // H=200,W=60
        img.SubMat(new Rect(0, 0, 60, 100)).SetTo(new Scalar(60));   // 上半暗
        img.SubMat(new Rect(0, 100, 60, 100)).SetTo(new Scalar(200)); // 下半亮
        var line = new RefineLine(0.5, 0.22, 0.5, 0.78, HeadMinusTailGray: 140); // 头在亮端
        var r = LineFitHeading.Resolve(img, Frame(30, 100, 160, 40), 90.0, line);

        Assert.True(r.Resolved);
        Assert.Equal(90.0, r.DirectedDeg, 3); // dir=(cos90,sin90)=(0,1) → +端在下（亮）
        Assert.True(r.HeadPoint.Y > 100);
    }
}
