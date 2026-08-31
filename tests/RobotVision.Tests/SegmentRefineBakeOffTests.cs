using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Teach;
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

        [Fact]
        public void PickWinner_SiftRanksAfterTemplate()
        {
            var candidates = new[]
            {
                new SegmentRefineCandidate(SegmentRefineMethod.Sift, true, true, 0.90, "sift"),
                new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.88, "tpl"),
            };
            var winner = SegmentRefineBakeOff.PickWinner(candidates);
            Assert.Equal(SegmentRefineMethod.Template, winner!.Method);
        }

        [Fact]
        public void PickWinner_ShapeMatchRanksAfterTemplate_BeforeSift()
        {
            var shapeBeatsSift = SegmentRefineBakeOff.PickWinner(
            [
                new SegmentRefineCandidate(SegmentRefineMethod.Sift, true, true, 0.90, "sift"),
                new SegmentRefineCandidate(SegmentRefineMethod.ShapeMatch, true, true, 0.88, "shape"),
            ]);
            Assert.Equal(SegmentRefineMethod.ShapeMatch, shapeBeatsSift!.Method);

            var templateBeatsShape = SegmentRefineBakeOff.PickWinner(
            [
                new SegmentRefineCandidate(SegmentRefineMethod.ShapeMatch, true, true, 0.90, "shape"),
                new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.88, "tpl"),
            ]);
        Assert.Equal(SegmentRefineMethod.Template, templateBeatsShape!.Method);
    }

    [Fact]
    public void Aggregate_PassRateTimesMeanOkScore()
    {
        IReadOnlyList<SegmentRefineCandidate> frame1 =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.80, "a"),
            new(SegmentRefineMethod.Template, true, true, 0.95, "b"),
        ];
        IReadOnlyList<SegmentRefineCandidate> frame2 =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.80, "a"),
            new(SegmentRefineMethod.Template, false, true, 0, "miss"),
        ];
        var agg = SegmentRefineBakeOff.Aggregate([frame1, frame2]);
        var caliper = agg.Single(c => c.Method == SegmentRefineMethod.CaliperTab);
        Assert.True(caliper.Ok);
        Assert.Equal(0.80, caliper.Score, 2);
        Assert.Contains("2/2", caliper.Note, StringComparison.Ordinal);
        var template = agg.Single(c => c.Method == SegmentRefineMethod.Template);
        Assert.Equal(0.475, template.Score, 3);
        Assert.Contains("1/2", template.Note, StringComparison.Ordinal);
        Assert.Equal(SegmentRefineMethod.CaliperTab, SegmentRefineBakeOff.PickWinner(agg)!.Method);
    }

    [Fact]
    public void Aggregate_UnstableDirectedAngle_LowersScore()
    {
        IReadOnlyList<SegmentRefineCandidate> a =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.90, "0", 0),
        ];
        IReadOnlyList<SegmentRefineCandidate> b =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.90, "180", 180),
        ];
        var flip = SegmentRefineBakeOff.Aggregate([a, b]).Single();
        var stable = SegmentRefineBakeOff.Aggregate(
        [
            [new SegmentRefineCandidate(SegmentRefineMethod.CaliperTab, true, true, 0.90, "0", 1)],
            [new SegmentRefineCandidate(SegmentRefineMethod.CaliperTab, true, true, 0.90, "1", 2)],
        ]).Single();
        Assert.True(stable.Score > flip.Score + 0.2,
            $"稳 {stable.Score:0.00} 应明显高于翻面 {flip.Score:0.00}（{flip.Note}）");
        Assert.Contains("角σ", flip.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Aggregate_StoresAngleStdDegWhenMultipleAngles()
    {
        var agg = SegmentRefineBakeOff.Aggregate(
        [
            [new SegmentRefineCandidate(SegmentRefineMethod.CaliperTab, true, true, 0.90, "0", 0)],
            [new SegmentRefineCandidate(SegmentRefineMethod.CaliperTab, true, true, 0.90, "1", 1)],
        ]).Single();
        Assert.True(double.IsFinite(agg.AngleStdDeg));
        Assert.True(agg.AngleStdDeg < 2, $"稳角 σ={agg.AngleStdDeg:0.00}");
    }

    [Fact]
    public void PickWinner_UnstablePolicyYieldsToStableRunnerUp()
    {
        var winner = SegmentRefineBakeOff.PickWinner(
        [
            new(SegmentRefineMethod.CentroidHoleLine, true, true, 0.80, "翻", 0, 45),
            new(SegmentRefineMethod.CaliperTab, true, true, 0.78, "稳", 180, 1.2),
        ]);
        Assert.Equal(SegmentRefineMethod.CaliperTab, winner!.Method);
    }

    [Fact]
    public void PickWinner_StablePolicyKeepsOrderWhenStdClose()
    {
        var winner = SegmentRefineBakeOff.PickWinner(
        [
            new(SegmentRefineMethod.CentroidHoleLine, true, true, 0.80, "hole", 0, 1.5),
            new(SegmentRefineMethod.CaliperTab, true, true, 0.84, "cal", 1, 1.2),
        ]);
        Assert.Equal(SegmentRefineMethod.CentroidHoleLine, winner!.Method);
    }

    [Fact]
    public void Aggregate_MissedFramesCountInDenominator()
    {
        IReadOnlyList<SegmentRefineCandidate> hit =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.90, "ok"),
        ];
        var agg = SegmentRefineBakeOff.Aggregate([hit, [], []]);
        var caliper = agg.Single();
        Assert.Equal(0.30, caliper.Score, 2);
        Assert.False(caliper.Ok);
        Assert.Contains("1/3", caliper.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void PickWinner_SiftHighRawScoreWithoutStd_DoesNotBeatTemplate()
    {
        var winner = SegmentRefineBakeOff.PickWinner(
        [
            new SegmentRefineCandidate(SegmentRefineMethod.Sift, true, true, 0.95, "sift"),
            new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.70, "tpl"),
        ]);
        Assert.Equal(SegmentRefineMethod.Template, winner!.Method);
    }

    [Fact]
    public void PickWinner_SiftBeatsTemplateWhenBothHaveBatchStd()
    {
        var winner = SegmentRefineBakeOff.PickWinner(
        [
            new SegmentRefineCandidate(SegmentRefineMethod.Sift, true, true, 0.90, "sift", 1, 1.2),
            new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.70, "tpl", 0, 5.0),
        ]);
        Assert.Equal(SegmentRefineMethod.Sift, winner!.Method);
    }

    [Fact]
    public void PickWinner_CustomPolicyOrder_PrefersSift()
    {
        var winner = SegmentRefineBakeOff.PickWinner(
        [
            new SegmentRefineCandidate(SegmentRefineMethod.Sift, true, true, 0.88, "sift"),
            new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.90, "tpl"),
        ], policyOrder: [SegmentRefineMethod.Sift, SegmentRefineMethod.Template]);
        Assert.Equal(SegmentRefineMethod.Sift, winner!.Method);
    }

    [Fact]
    public void PickWinner_DownrankBlocksScoreOverride()
    {
        var winner = SegmentRefineBakeOff.PickWinner(
        [
            new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.92, "tpl"),
            new SegmentRefineCandidate(SegmentRefineMethod.CaliperTab, true, true, 0.70, "cal"),
        ], downrank: SegmentRefineMethod.Template);
        Assert.Equal(SegmentRefineMethod.CaliperTab, winner!.Method);
    }

    [Fact]
    public void Aggregate_Empty_ReturnsEmpty()
    {
        Assert.Empty(SegmentRefineBakeOff.Aggregate([]));
    }
}
