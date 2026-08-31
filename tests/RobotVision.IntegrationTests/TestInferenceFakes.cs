using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.IntegrationTests;

/// <summary>
/// 集成测试共享的假推理引擎:不依赖真实 ONNX 文件(不入库,CI 无模型)。
/// 各任务默认返回空结果(推理"未检出"→ 1007),管线可走通到推理后各错误路径
/// (1015/1014/1012/1004/1007);DetectedTask 默认 null,InferenceTaskValidation 跳过任务校验。
/// 免模型策略(如 DualBlobCenterLine)不经过本引擎,不受影响。
/// </summary>
public sealed class TestInferenceEngine : IInferenceEngine
{
    public InferenceTask? DetectedTask { get; init; }

    public Func<VisionImage, IReadOnlyList<ObjectDetectionResult>>? OnObjectDetection { get; set; }

    public Func<VisionImage, IReadOnlyList<InstanceSegmentation>>? OnSegmentation { get; set; }

    public Func<VisionImage, IReadOnlyList<PoseDetectionResult>>? OnPose { get; set; }

    public int DisposedCount { get; private set; }

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

public sealed class TestInferenceEngineFactory(Func<TestInferenceEngine> create) : IInferenceEngineFactory
{
    public string ActiveDevice => "";

    public bool GpuUnavailable => false;

    public TestInferenceEngine? LastEngine { get; private set; }

    public IInferenceEngine Create(string modelPath)
    {
        LastEngine = create();
        return LastEngine;
    }

    public InferenceTask? DetectTask(string modelPath) => null;
}
