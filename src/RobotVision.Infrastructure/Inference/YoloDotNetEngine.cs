using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Extensions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// YoloDotNet（ONNX Runtime）推理引擎实现。对外只暴露本程序 DTO 与 VisionImage。
/// </summary>
public sealed class YoloDotNetEngine(Yolo yolo) : IInferenceEngine
{
    public InferenceTask? DetectedTask => YoloDotNetModelTask.Map(yolo.OnnxModel.ModelType);

    public IReadOnlyList<ObjectDetectionResult> RunObjectDetection(
        VisionImage image, double confidence, double iou)
    {
        using var bitmap = ToSkia(image);
        return yolo.RunObjectDetection(bitmap, confidence, iou)
            .Select(d => new ObjectDetectionResult(ToBox(d.BoundingBox), d.Confidence, d.Label.Name))
            .ToArray();
    }

    public IReadOnlyList<InstanceSegmentation> RunSegmentation(
        VisionImage image, double confidence, double pixelConfidence, double iou)
    {
        using var bitmap = ToSkia(image);
        return yolo.RunSegmentation(bitmap, confidence, pixelConfidence, iou)
            .Select(s =>
            {
                var box = ToBox(s.BoundingBox);
                var contour = s.GetContourPoints();
                // YoloDotNet GetContourPoints 为全图像素坐标；DTO 约定 ContourLocal 相对包围盒左上角。
                IReadOnlyList<ImagePoint> local = contour.Length == 0
                    ? []
                    : contour.Select(p => new ImagePoint(p.X - box.X, p.Y - box.Y)).ToArray();
                return new InstanceSegmentation(
                    box,
                    s.Confidence,
                    s.Label.Name,
                    local,
                    s.BitPackedPixelMask ?? []);
            })
            .ToArray();
    }

    public IReadOnlyList<PoseDetectionResult> RunPoseEstimation(
        VisionImage image, double confidence, double iou)
    {
        using var bitmap = ToSkia(image);
        return yolo.RunPoseEstimation(bitmap, confidence, iou)
            .Select(p =>
            {
                IReadOnlyList<KeypointDetection> points = p.KeyPoints is null
                    ? []
                    : p.KeyPoints.Select(k => new KeypointDetection(k.X, k.Y, k.Confidence)).ToArray();
                return new PoseDetectionResult(ToBox(p.BoundingBox), p.Confidence, p.Label.Name, points);
            })
            .ToArray();
    }

    public void Dispose() => yolo.Dispose();

    private static SKBitmap ToSkia(VisionImage image)
    {
        using var mat = VisionImageCv.AsMat(image);
        return MatSkiaConverter.ToSKBitmap(mat);
    }

    private static PixelBox ToBox(SKRectI box) => new(box.Left, box.Top, box.Width, box.Height);
}
