using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

public class InferenceTaskValidationTests
{
    private sealed class StubEngine(InferenceTask? detected) : IInferenceEngine
    {
        public InferenceTask? DetectedTask => detected;

        public IReadOnlyList<ObjectDetectionResult> RunObjectDetection(VisionImage image, double confidence = 0.25, double iou = 0.45) => [];

        public IReadOnlyList<InstanceSegmentation> RunSegmentation(
            VisionImage image, double confidence = 0.25, double pixelConfidence = 0.5, double iou = 0.45) => [];

        public IReadOnlyList<PoseDetectionResult> RunPoseEstimation(
            VisionImage image, double confidence = 0.25, double iou = 0.45) => [];

        public void Dispose() { }
    }

    [Fact]
    public void EnsureSupported_MatchingTask_DoesNotThrow()
    {
        using var engine = new StubEngine(InferenceTask.Segmentation);
        InferenceTaskValidation.EnsureSupported(engine, InferenceTask.Segmentation);
    }

    [Fact]
    public void EnsureSupported_Mismatch_ThrowsModelNotAvailable()
    {
        using var engine = new StubEngine(InferenceTask.Segmentation);
        var ex = Assert.Throws<VisionException>(
            () => InferenceTaskValidation.EnsureSupported(engine, InferenceTask.PoseEstimation));
        Assert.Equal(VisionErrorCode.ModelNotAvailable, ex.ErrorCode);
        Assert.Contains("分割", ex.Message, StringComparison.Ordinal);
        Assert.Contains("关键点", ex.Message, StringComparison.Ordinal);
    }
}
