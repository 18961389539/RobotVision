using SkiaSharp;
using YoloDotNet.Models;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// 推理引擎抽象：隔离具体推理框架（当前为 YoloDotNet/ONNX Runtime）。
/// 三种任务（检测/分割/关键点）的入口与 YoloDotNet 的 Yolo 类签名一致，
/// 因此策略层与 UI 的调用点（session.Run(y => y.RunXxx(...))）零改动。
/// 更换推理后端（CPU→CUDA/OpenVINO/DirectML，或换框架）＝提供新的
/// <see cref="IInferenceEngineFactory"/> 实现，策略层与核心管线不感知。
/// 实现必须线程安全地被调用方串行化（ModelSession 内信号量）。
/// </summary>
public interface IInferenceEngine : IDisposable
{
    /// <summary>目标检测：返回检测框 + 置信度。默认参数与 YoloDotNet Yolo 类一致。</summary>
    IReadOnlyList<ObjectDetection> RunObjectDetection(SkiaSharp.SKBitmap image, double confidence = 0.25, double iou = 0.45);

    /// <summary>实例分割：返回掩码 + 检测框 + 置信度。默认参数与 YoloDotNet Yolo 类一致。</summary>
    IReadOnlyList<Segmentation> RunSegmentation(SkiaSharp.SKBitmap image, double confidence = 0.25, double pixelConfidence = 0.5, double iou = 0.45);

    /// <summary>姿态估计（关键点）：返回关键点 + 检测框 + 置信度。默认参数与 YoloDotNet Yolo 类一致。</summary>
    IReadOnlyList<PoseEstimation> RunPoseEstimation(SkiaSharp.SKBitmap image, double confidence = 0.25, double iou = 0.45);
}

/// <summary>
/// 推理引擎工厂：按模型路径创建引擎实例的扩展点。
/// 默认实现 <see cref="YoloDotNetEngineFactory"/>（CPU）。更换 GPU/框架时
/// 提供新实现并替换 DI 注册，ModelManager 与策略层零改动。
/// </summary>
public interface IInferenceEngineFactory
{
    /// <summary>按模型文件路径创建引擎。模型文件不存在/加载失败应抛异常（调用方转 VisionException）。</summary>
    IInferenceEngine Create(string modelPath);
}
