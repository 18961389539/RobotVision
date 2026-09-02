using RobotVision.Core.Inference;

namespace RobotVision.Hosting;

public sealed record ModelFolderTestRequest(
    string ModelFile,
    InferenceTask Task,
    IReadOnlyList<string> ImagePaths,
    double Confidence,
    double PixelConfidence,
    double Iou);

public sealed record ModelFolderTestImageResult(
    string FileName,
    BgraImageBuffer? Image,
    int DetectionCount,
    string? Error);

public interface IModelTestService
{
    IReadOnlyList<ModelFolderTestImageResult> RunFolderTest(
        ModelFolderTestRequest request,
        CancellationToken ct = default);
}
