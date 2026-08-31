using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

public sealed class SegmentRefineAdvisorTests
{
    [Fact]
    public void SlenderTabPart_RecommendsCaliper()
    {
        using var img = new Mat(360, 480, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(img, new Point(130, 152), new Point(350, 208), new Scalar(80, 80, 80), -1);
        Cv2.Rectangle(img, new Point(220, 208), new Point(260, 226), new Scalar(30, 30, 30), -1);
        using var mask = new Mat(360, 480, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Point(130, 152), new Point(350, 208), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Point(220, 208), new Point(260, 226), Scalar.All(255), -1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        Assert.True(contours.Length > 0);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();

        var advice = SegmentRefineAdvisor.Analyze(img, contour);
        Assert.True(
            advice.Recommended is SegmentRefineMethod.CaliperTab or SegmentRefineMethod.Template,
            $"带凸起细长件不应推荐直线拟合，实际 {advice.Recommended}：{advice.Summary}");
    }

    [Fact]
    public void SmoothRectangle_RecommendsLineFit()
    {
        using var img = new Mat(200, 320, MatType.CV_8UC3, new Scalar(200, 200, 200));
        Cv2.Rectangle(img, new Point(40, 70), new Point(280, 130), new Scalar(190, 190, 190), -1);
        Point2f[] contour =
        [
            new(40, 70), new(280, 70), new(280, 130), new(40, 130),
        ];
        var advice = SegmentRefineAdvisor.Analyze(img, contour);
        Assert.True(
            advice.Recommended is SegmentRefineMethod.LineFit or SegmentRefineMethod.CaliperTab,
            $"弱纹理矩形不应推荐模板，实际 {advice.Recommended}");
        Assert.False(advice.RecommendEdgeMatch);
    }

    [Fact]
    public void OverlayBatch_UsesPlaybackWinnerAndKeepsSeedPolarity()
    {
        var seed = new SegmentRefineAdvice(
            SegmentRefineMethod.LineFit, false, false, 1.2, 0, 0, 0, 0, "seed")
        {
            TabPolarity = TabPolarityLock.MinusShortAxis,
        };
        IReadOnlyList<SegmentRefineCandidate> agg =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.80, "38/39 过门，均分 0.80"),
            new(SegmentRefineMethod.Template, true, true, 0.40, "10/39 过门"),
        ];
        var advice = SegmentRefineAdvisor.OverlayBatch(seed, agg, 35, 39);
        Assert.Equal(SegmentRefineMethod.CaliperTab, advice.Recommended);
        Assert.Equal(TabPolarityLock.MinusShortAxis, advice.TabPolarity);
        Assert.Contains("回放 35/39 检出", advice.Summary);
        Assert.Contains("采用回放胜出", advice.Summary);
        Assert.Equal(agg, advice.Candidates);
    }

    [Fact]
    public void OverlayBatch_UntaughtKeepsPrintedTextureHeuristic()
    {
        var scene = new SceneDescriptor(SceneKind.PrintedTexture, LightingClass.BrightField,
            1.8, 0.3, 5.2, 0.15, false, 0, 900, "纹理");
        var seed = new SegmentRefineAdvice(
            SegmentRefineMethod.Template, true, true, 1.8, 5.2, 0.15, 0, 0, "seed")
        {
            Scene = scene,
        };
        IReadOnlyList<SegmentRefineCandidate> agg =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.80, "38/39 过门，均分 0.80"),
            new(SegmentRefineMethod.Template, false, true, 0, "未示教", Skipped: true),
        ];
        var advice = SegmentRefineAdvisor.OverlayBatch(seed, agg, 35, 39);
        Assert.Equal(SegmentRefineMethod.Template, advice.Recommended);
        Assert.Contains("尚未示教", advice.Summary);
    }
}
