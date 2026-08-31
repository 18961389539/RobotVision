using RobotVision.Core.Models;

namespace RobotVision.Core.Recipe;

/// <summary>画面叠加策略：与配方试触发一致，随推理快照一并发布给 UI。</summary>
public sealed record RecipeDisplayHints(
    bool DrawDetectionRoi,
    Roi? DetectionRoi,
    bool ShowCaliperDebug)
{
    /// <summary>产线简洁模式：只画位姿本体，不画检测 ROI / 卡尺探针。</summary>
    public static RecipeDisplayHints Production { get; } = new(false, null, false);

    /// <summary>与配方页「试触发」结果图一致。</summary>
    public static RecipeDisplayHints ForRecipeTest(RecipeConfig recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new(
            DrawDetectionRoi: recipe.Roi is not null,
            DetectionRoi: recipe.Roi,
            ShowCaliperDebug: recipe.AngleMode == AngleMode.MaskTemplate
                && recipe.Template.RefineMethod == SegmentRefineMethod.CaliperTab);
    }
}
