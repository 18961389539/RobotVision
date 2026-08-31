using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Tests;

/// <summary>
/// 策略测试共享的可配置假推理引擎与工厂。
/// DetectedTask 默认 null（InferenceTaskValidation 对未知任务跳过校验），
/// 通过委托注入各任务返回结果；DisposedCount 记录释放。
/// </summary>
internal sealed class FakeInferenceEngine : IInferenceEngine
{
    public InferenceTask? DetectedTask { get; init; }

    public Func<VisionImage, IReadOnlyList<ObjectDetectionResult>>? OnObjectDetection { get; set; }

    public Func<VisionImage, IReadOnlyList<InstanceSegmentation>>? OnSegmentation { get; set; }

    public Func<VisionImage, IReadOnlyList<PoseDetectionResult>>? OnPose { get; set; }

    public int DisposedCount;

    public IReadOnlyList<ObjectDetectionResult> RunObjectDetection(
        VisionImage image, double confidence = 0.25, double iou = 0.45) =>
        OnObjectDetection?.Invoke(image) ?? [];

    public IReadOnlyList<InstanceSegmentation> RunSegmentation(
        VisionImage image, double confidence = 0.25, double pixelConfidence = 0.5, double iou = 0.45) =>
        OnSegmentation?.Invoke(image) ?? [];

    public IReadOnlyList<PoseDetectionResult> RunPoseEstimation(
        VisionImage image, double confidence = 0.25, double iou = 0.45) =>
        OnPose?.Invoke(image) ?? [];

    public void Dispose() => DisposedCount++;
}

internal sealed class FakeInferenceEngineFactory(Func<FakeInferenceEngine> create) : IInferenceEngineFactory
{
    public string ActiveDevice => "";

    public bool GpuUnavailable => false;

    public FakeInferenceEngine? LastEngine { get; private set; }

    public IInferenceEngine Create(string modelPath)
    {
        LastEngine = create();
        return LastEngine;
    }

    public InferenceTask? DetectTask(string modelPath) =>
        InferenceTaskDetector.Detect(this, modelPath);
}
