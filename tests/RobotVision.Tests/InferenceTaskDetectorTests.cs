using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

public class InferenceTaskDetectorTests
{
    [Theory]
    [InlineData("yolov8n-seg.onnx", InferenceTask.Segmentation)]
    [InlineData("a01_seg_best.onnx", InferenceTask.Segmentation)]
    [InlineData("yolov8n-pose.onnx", InferenceTask.PoseEstimation)]
    [InlineData("a01_kpt.onnx", InferenceTask.PoseEstimation)]
    [InlineData("yolov8n.onnx", null)]
    public void GuessFromFileName_KnownPatterns(string name, InferenceTask? expected)
    {
        Assert.Equal(expected, InferenceTaskDetector.GuessFromFileName(name));
    }
}
