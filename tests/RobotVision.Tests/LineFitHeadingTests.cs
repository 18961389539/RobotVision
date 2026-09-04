using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>LineFit 有向角判决单测（经 <see cref="LineFitRefine"/> 内部入口）。</summary>
public sealed class LineFitHeadingTests
{
    private static HousingFrame Frame(double cx, double cy, double longLen, double shortLen) =>
        new(new Point2f((float)cx, (float)cy), 0.0, longLen, shortLen);

    private static Mat HalfBrightHalfDark()
    {
        var img = new Mat(60, 200, MatType.CV_8UC1, new Scalar(0));
        img.SubMat(new Rect(0, 0, 100, 60)).SetTo(new Scalar(60));
        img.SubMat(new Rect(100, 0, 100, 60)).SetTo(new Scalar(200));
        return img;
    }

    [Fact]
    public void NoLine_AutoBuildsFromBrightness_ResolvesHeadAtBrightEnd()
    {
        using var gray = HalfBrightHalfDark();
        var r = LineFitRefine.ResolveHeading(gray, Frame(100, 30, 160, 40), 0.0, line: null);
        Assert.True(r.Resolved);
        Assert.Equal(0.0, r.DirectedDeg, 3);
        Assert.True(r.HeadPoint.X > 100);
    }

    [Fact]
    public void NoLine_SymmetricPart_KeepsUndirected()
    {
        using var gray = new Mat(60, 200, MatType.CV_8UC1, new Scalar(128));
        var r = LineFitRefine.ResolveHeading(gray, Frame(100, 30, 160, 40), 0.0, line: null);
        Assert.False(r.Resolved);
        Assert.Equal(0.0, r.DirectedDeg);
        Assert.NotNull(r.Note);
    }

    [Fact]
    public void HeadBrighter_TaughtPositive_ResolvesToZero()
    {
        using var gray = HalfBrightHalfDark();
        var line = new RefineLine(0.22, 0.5, 0.78, 0.5, HeadMinusTailGray: 140);
        var r = LineFitRefine.ResolveHeading(gray, Frame(100, 30, 160, 40), 0.0, line);

        Assert.True(r.Resolved);
        Assert.Equal(0.0, r.DirectedDeg, 3);
        Assert.True(r.HeadPoint.X > 100);
    }

    [Fact]
    public void FlippedPart_HeadDarker_ResolvesTo180()
    {
        using var gray = HalfBrightHalfDark();
        var line = new RefineLine(0.78, 0.5, 0.22, 0.5, HeadMinusTailGray: -140);
        var r = LineFitRefine.ResolveHeading(gray, Frame(100, 30, 160, 40), 0.0, line);

        Assert.True(r.Resolved);
        Assert.Equal(180.0, Math.Abs(r.DirectedDeg), 3);
        Assert.True(r.HeadPoint.X < 100);
    }

    [Fact]
    public void SymmetricPart_Unresolved_KeepsUndirected_WithNote()
    {
        using var gray = new Mat(60, 200, MatType.CV_8UC1, new Scalar(128));
        var line = new RefineLine(0.22, 0.5, 0.78, 0.5, HeadMinusTailGray: 60);
        var r = LineFitRefine.ResolveHeading(gray, Frame(100, 30, 160, 40), 0.0, line);

        Assert.False(r.Resolved);
        Assert.Equal(0.0, r.DirectedDeg);
        Assert.NotNull(r.Note);
    }

    [Fact]
    public void WeakTaughtSignature_Unresolved()
    {
        using var gray = HalfBrightHalfDark();
        var line = new RefineLine(0.22, 0.5, 0.78, 0.5, HeadMinusTailGray: 1.0);
        var r = LineFitRefine.ResolveHeading(gray, Frame(100, 30, 160, 40), 0.0, line);
        Assert.False(r.Resolved);
    }

    [Fact]
    public void VerticalAxis_ResolvesHeadAtBrightEnd()
    {
        using var img = new Mat(200, 60, MatType.CV_8UC1, new Scalar(0));
        img.SubMat(new Rect(0, 0, 60, 100)).SetTo(new Scalar(60));
        img.SubMat(new Rect(0, 100, 60, 100)).SetTo(new Scalar(200));
        var line = new RefineLine(0.5, 0.22, 0.5, 0.78, HeadMinusTailGray: 140);
        var r = LineFitRefine.ResolveHeading(img, Frame(30, 100, 160, 40), 90.0, line);

        Assert.True(r.Resolved);
        Assert.Equal(90.0, r.DirectedDeg, 3);
        Assert.True(r.HeadPoint.Y > 100);
    }
}
