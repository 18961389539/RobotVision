using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

public sealed class PixelPoseOutputTests
{
    [Fact]
    public void Empty_IsNoTarget()
    {
        Assert.Equal(VisionErrorCode.NoTargetFound, PixelPoseOutput.RejectReason([]));
        Assert.Empty(PixelPoseOutput.UsableOnly([]));
    }

    [Fact]
    public void AllUnusable_IsRefineFailed()
    {
        var poses = new[]
        {
            new PixelPose(1, 2, 3, 0) { Usable = false },
            new PixelPose(4, 5, 6, 0) { Usable = false },
        };
        Assert.Equal(VisionErrorCode.RefineFailed, PixelPoseOutput.RejectReason(poses));
        Assert.Empty(PixelPoseOutput.UsableOnly(poses));
    }

    [Fact]
    public void Mixed_KeepsUsableOnly()
    {
        var ok = new PixelPose(10, 20, 30, 0.9);
        var ng = new PixelPose(1, 1, 0, 0) { Usable = false };
        Assert.Null(PixelPoseOutput.RejectReason([ng, ok]));
        Assert.Equal([ok], PixelPoseOutput.UsableOnly([ng, ok]));
    }

    [Fact]
    public void EnforceExpectedCount_MismatchMarksAllUnusable()
    {
        var poses = new List<PixelPose>
        {
            new(1, 1, 0, 0.9),
            new(2, 2, 0, 0.8),
        };
        PixelPoseOutput.EnforceExpectedCount(poses, expectedCount: 1);
        Assert.All(poses, p => Assert.False(p.Usable));
        Assert.Equal(VisionErrorCode.RefineFailed, PixelPoseOutput.RejectReason(poses));
        Assert.Equal(1019, (int)VisionErrorCode.RefineFailed);
    }

    [Fact]
    public void EnforceExpectedCount_MatchLeavesUsable()
    {
        var poses = new List<PixelPose> { new(1, 1, 0, 0.9), new(2, 2, 0, 0) { Usable = false } };
        PixelPoseOutput.EnforceExpectedCount(poses, expectedCount: 1);
        Assert.True(poses[0].Usable);
        Assert.False(poses[1].Usable);
    }

    [Fact]
    public void EnforceExpectedCount_ZeroSkips()
    {
        var poses = new List<PixelPose> { new(1, 1, 0, 0.9), new(2, 2, 0, 0.8) };
        PixelPoseOutput.EnforceExpectedCount(poses, expectedCount: 0);
        Assert.All(poses, p => Assert.True(p.Usable));
    }

    [Fact]
    public void AllowCoarseFallbackFalse_FallbackPose_IsRefineFailed1019()
    {
        var recipe = new RecipeConfig
        {
            Name = "Product",
            Template = new TemplateOptions { AllowCoarseFallback = false },
        };
        var contour = new[]
        {
            new Point2f(10, 10),
            new Point2f(80, 12),
            new Point2f(78, 40),
            new Point2f(12, 38),
        };
        var hit = SegmentRefineOps.Fallback(contour, 0.91, recipe);
        Assert.False(hit.Pose.Usable);
        Assert.Equal(VisionErrorCode.RefineFailed, PixelPoseOutput.RejectReason([hit.Pose]));
        Assert.Equal(1019, (int)VisionErrorCode.RefineFailed);
    }
}
