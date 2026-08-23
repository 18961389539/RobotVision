using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Models;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// YoloDotNet（ONNX Runtime）推理引擎实现。
/// 内部持有 Yolo 实例（非线程安全，调用方须经 ModelSession 信号量串行化）。
/// 更换 ExecutionProvider（CPU/CUDA/OpenVINO/DirectML）＝在 <see cref="YoloDotNetEngineFactory"/>
/// 中替换 Provider 类；更换整个框架＝提供新的 IInferenceEngine 实现。
/// </summary>
public sealed class YoloDotNetEngine(Yolo yolo) : IInferenceEngine
{
    public IReadOnlyList<ObjectDetection> RunObjectDetection(SkiaSharp.SKBitmap image, double confidence, double iou) =>
        yolo.RunObjectDetection(image, confidence, iou);

    public IReadOnlyList<Segmentation> RunSegmentation(SkiaSharp.SKBitmap image, double confidence, double pixelConfidence, double iou) =>
        yolo.RunSegmentation(image, confidence, pixelConfidence, iou);

    public IReadOnlyList<PoseEstimation> RunPoseEstimation(SkiaSharp.SKBitmap image, double confidence, double iou) =>
        yolo.RunPoseEstimation(image, confidence, iou);

    public void Dispose() => yolo.Dispose();
}
