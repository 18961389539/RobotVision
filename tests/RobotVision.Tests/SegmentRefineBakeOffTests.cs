using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

public sealed class SegmentRefineBakeOffTests
{
    [Fact]
    public void TabPart_CaliperBeatsLineFit()
    {
        using var img = new Mat(360, 480, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(img, new Point(130, 152), new Point(350, 208), new Scalar(80, 80, 80), -1);
        Cv2.Rectangle(img, new Point(220, 208), new Point(260, 226), new Scalar(30, 30, 30), -1);
        using var mask = new Mat(360, 480, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Point(130, 152), new Point(350, 208), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Point(220, 208), new Point(260, 226), Scalar.All(255), -1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();

        var candidates = SegmentRefineBakeOff.Run(img, contour);
        var caliper = candidates.Single(c => c.Method == SegmentRefineMethod.CaliperTab);
        Assert.True(caliper.Ok, caliper.Note);
        var winner = SegmentRefineBakeOff.PickWinner(candidates);
        Assert.NotNull(winner);
        Assert.Equal(SegmentRefineMethod.CaliperTab, winner.Method);
    }

    [Fact]
    public void SmoothRectangle_LineFitWinsWhenCaliperHasNoTab()
    {
        using var img = new Mat(200, 320, MatType.CV_8UC3, new Scalar(200, 200, 200));
        Cv2.Rectangle(img, new Point(40, 70), new Point(280, 130), new Scalar(190, 190, 190), -1);
        Point2f[] contour =
        [
            new(40, 70), new(280, 70), new(280, 130), new(40, 130),
        ];
        var candidates = SegmentRefineBakeOff.Run(img, contour);
        var winner = SegmentRefineBakeOff.PickWinner(candidates);
        if (winner is null)
            return;
        Assert.Equal(SegmentRefineMethod.LineFit, winner.Method);
        Assert.False(winner.Directed);
    }

    [Fact]
    public void PickWinner_HighScoreOverridesPolicyWhenGapLarge()
    {
        var candidates = new[]
        {
            new SegmentRefineCandidate(SegmentRefineMethod.CentroidHoleLine, true, true, 0.50, "hole"),
            new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.92, "tpl"),
            new SegmentRefineCandidate(SegmentRefineMethod.LineFit, true, false, 0.80, "line"),
        };
        var winner = SegmentRefineBakeOff.PickWinner(candidates);
        Assert.Equal(SegmentRefineMethod.Template, winner!.Method);
    }

    [Fact]
    public void PickWinner_CloseScoresKeepPolicyOrder()
    {
        var candidates = new[]
        {
            new SegmentRefineCandidate(SegmentRefineMethod.CentroidHoleLine, true, true, 0.80, "hole"),
            new SegmentRefineCandidate(SegmentRefineMethod.CaliperTab, true, true, 0.84, "cal"),
        };
        var winner = SegmentRefineBakeOff.PickWinner(candidates);
        Assert.Equal(SegmentRefineMethod.CentroidHoleLine, winner!.Method);
    }
}
