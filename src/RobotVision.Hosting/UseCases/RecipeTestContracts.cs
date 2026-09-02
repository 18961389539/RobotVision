using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Teach;

namespace RobotVision.Hosting;

public sealed record RecipePreviewRequest(
    RecipeConfig Recipe,
    int TimeoutMs,
    bool IncludeTriggerPose,
    double TriggerPoseX,
    double TriggerPoseY,
    double TriggerPoseRz);

/// <summary>试触发结果：<see cref="Frame"/> 由调用方 dispose（WPF 层合成叠加后释放）。</summary>
public sealed record RecipePreviewRunResult(
    VisionResult Result,
    PreviewRunOutcome? Frame,
    string? RefineQualityHint);

public sealed record RecipeTeachTemplateRequest(
    RecipeConfig Recipe,
    RecipePrior? PlaybookPrior,
    string CameraId,
    string LightControllerId,
    LightingConfig? Lighting);

public sealed record RecipeTeachTemplateResult(
    string TemplateImageBase64,
    SegmentRefineAdvice Advice,
    bool FeatureFallback,
    int TemplateWidth,
    int TemplateHeight);

public interface IRecipeTestService
{
    Task<RecipePreviewRunResult> RunPreviewAsync(RecipePreviewRequest request, CancellationToken ct = default);

    Task<RecipeTeachTemplateResult> TeachTemplateAsync(
        RecipeTeachTemplateRequest request, CancellationToken ct = default);
}
