using OpenCvSharp;
using RobotVision.Core.Inference;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Hosting;

internal sealed class ModelTestService(ModelManager models) : IModelTestService
{
    public IReadOnlyList<ModelFolderTestImageResult> RunFolderTest(
        ModelFolderTestRequest request,
        CancellationToken ct = default)
    {
        var results = new List<ModelFolderTestImageResult>(request.ImagePaths.Count);
        var session = models.Open(request.ModelFile, request.Task);
        session.Run(engine =>
        {
            InferenceTaskValidation.EnsureSupported(engine, request.Task);
            return 0;
        });

        foreach (var path in request.ImagePaths)
        {
            ct.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(path);
            try
            {
                using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
                if (mat.Empty())
                    throw new InvalidOperationException("图片解码失败");

                using var image = VisionImageMat.FromMat(mat, ownsMat: false);
                var detected = session.Run(engine => request.Task switch
                {
                    InferenceTask.ObjectDetection => RunDetections(engine, image, mat, request.Confidence, request.Iou),
                    InferenceTask.Segmentation => RunSegmentations(engine, image, mat, request.Confidence, request.PixelConfidence, request.Iou),
                    InferenceTask.PoseEstimation => RunPoses(engine, image, mat, request.Confidence, request.Iou),
                    _ => 0,
                });
                results.Add(new ModelFolderTestImageResult(fileName, BgraImageBuffer.FromBgrMat(mat), detected, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new ModelFolderTestImageResult(fileName, null, 0, ex.Message));
            }
        }

        return results;
    }

    private static int RunDetections(IInferenceEngine engine, VisionImage image, Mat mat, double confidence, double iou)
    {
        var results = engine.RunObjectDetection(image, confidence, iou);
        InferenceResultOverlay.DrawDetections(mat, results);
        return results.Count;
    }

    private static int RunSegmentations(IInferenceEngine engine, VisionImage image, Mat mat,
        double confidence, double pixelConfidence, double iou)
    {
        var results = engine.RunSegmentation(image, confidence, pixelConfidence, iou);
        InferenceResultOverlay.DrawSegmentations(mat, results);
        return results.Count;
    }

    private static int RunPoses(IInferenceEngine engine, VisionImage image, Mat mat, double confidence, double iou)
    {
        var results = engine.RunPoseEstimation(image, confidence, iou);
        InferenceResultOverlay.DrawPoses(mat, results, confidence);
        return results.Count;
    }
}
