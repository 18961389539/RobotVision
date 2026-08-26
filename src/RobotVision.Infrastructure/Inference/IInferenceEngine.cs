using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// 推理引擎抽象：隔离具体推理框架（当前为 YoloDotNet/ONNX Runtime）。
/// 输入输出均为本程序 DTO / <see cref="VisionImage"/>，策略层与 UI 不引用 YoloDotNet 或 Skia。
/// 实现必须线程安全地被调用方串行化（ModelSession 内信号量）。
/// </summary>
public interface IInferenceEngine : IDisposable
{
    /// <summary>从 ONNX 元数据推断的模型任务；null 表示未识别（如分类/OBB）。</summary>
    InferenceTask? DetectedTask { get; }

    IReadOnlyList<ObjectDetectionResult> RunObjectDetection(
        VisionImage image, double confidence = 0.25, double iou = 0.45);

    IReadOnlyList<InstanceSegmentation> RunSegmentation(
        VisionImage image, double confidence = 0.25, double pixelConfidence = 0.5, double iou = 0.45);

    IReadOnlyList<PoseDetectionResult> RunPoseEstimation(
        VisionImage image, double confidence = 0.25, double iou = 0.45);
}

/// <summary>
/// 推理引擎工厂：按模型路径创建引擎实例的扩展点。
/// </summary>
public interface IInferenceEngineFactory
{
    IInferenceEngine Create(string modelPath);

    /// <summary>加载模型并读取任务类型（随即释放引擎）。</summary>
    InferenceTask? DetectTask(string modelPath) => InferenceTaskDetector.Detect(this, modelPath);
}
