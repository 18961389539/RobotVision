using RobotVision.Core.Models;

namespace RobotVision.Core.Recipe;

/// <summary>画面叠加策略：与配方试触发一致，随推理快照一并发布给 UI。</summary>
public sealed record RecipeDisplayHints(
    bool DrawDetectionRoi,
    Roi? DetectionRoi,
    bool ShowRefineDebug,
    Roi? SecondaryBlobRoi = null)
{
    /// <summary>产线简洁模式：只画位姿本体，不画检测 ROI / 精修调试点。</summary>
    public static RecipeDisplayHints Production { get; } = new(false, null, false);

    /// <summary>卡尺 / 形状匹配 / SIFT 等带 <see cref="PoseOverlay.DebugDots"/> 的精修方法。</summary>
    public static bool RefineMethodShowsDebugOverlay(SegmentRefineMethod method) =>
        method is SegmentRefineMethod.CaliperTab
            or SegmentRefineMethod.ShapeMatch
            or SegmentRefineMethod.Sift;

    /// <summary>与配方页「试触发」、监控「对齐配方测试」结果图一致。</summary>
    public static RecipeDisplayHints ForRecipeTest(RecipeConfig recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new(
            DrawDetectionRoi: recipe.Roi is not null,
            DetectionRoi: recipe.Roi,
            ShowRefineDebug: recipe.AngleMode == AngleMode.MaskTemplate
                && RefineMethodShowsDebugOverlay(recipe.Template.RefineMethod),
            SecondaryBlobRoi: recipe.AngleMode == AngleMode.DualBlobCenterLine
                ? recipe.Blob.SecondaryRoi
                : null);
    }
}
