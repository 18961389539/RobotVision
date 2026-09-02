using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Teach;

namespace RobotVision.Hosting;

public sealed record RecipeSetupAnalysisRequest(
    RecipeConfig Editor,
    TaskConstraints Constraints,
    RecipePrior? Prior,
    bool UseBlobsWithoutModel,
    bool ScoreAllPlayback,
    string CameraId);

public sealed class RecipeSetupAnalysisResult : IDisposable
{
    public SceneDescriptor? Scene { get; init; }
    public IReadOnlyList<SegmentRefineCandidate> BakeOff { get; init; } = [];
    public HousingEdgePolarity Edge { get; init; }
    public TabPolarityLock Tab { get; init; }
    public Roi? FeatureRoi { get; init; }
    public IReadOnlyList<FeatureRoiCandidate> FeatureRanks { get; init; } = [];
    public BgraImageBuffer? Preview { get; set; }
    public int Detected { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = "";
    public double Confidence { get; init; }
    public SegmentRefineAdvice? Locks { get; init; }
    public int InstanceCount { get; init; }
    public bool CountUnstable { get; init; }
    public IReadOnlyDictionary<SceneKind, int>? SceneVotes { get; init; }
    public IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>>? PerFrame { get; init; }
    public IReadOnlyList<int>? InstanceCounts { get; init; }

    public void Dispose()
    {
    }
}

public interface IRecipeSetupAnalysisService
{
    RecipeSetupAnalysisResult AnalyzeGrab(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null);

    RecipeSetupAnalysisResult AnalyzePlayback(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null);
}
