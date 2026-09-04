using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>切角度模式 / 精修方法后裁次模型、清当前方法不用的 Template 字段。</summary>
internal static class RecipeEditorModeCleanup
{
    /// <returns>给操作员看的短句；无需提示时为 null。</returns>
    public static string? Apply(RecipeConfig editor) => Apply(editor, previousAngleMode: null);

    /// <param name="previousAngleMode">切角度模式前的值；双区模式之间切换时用来带走 ROI2。</param>
    public static string? Apply(RecipeConfig editor, AngleMode? previousAngleMode)
    {
        var carrySecondary = previousAngleMode is { } prev
            && AngleModes.UsesSecondarySearchRoi(prev)
            && AngleModes.UsesSecondarySearchRoi(editor.AngleMode)
                ? ReadSecondarySearchRoi(editor, prev)
                : null;

        var trimmed = RecipeModelSlots.TrimToSingleModelSlot(editor);
        var hadImage = !string.IsNullOrEmpty(editor.Template.TemplateImageBase64);
        var hadDual = !string.IsNullOrEmpty(editor.DualTemplate.TemplateABase64)
            || !string.IsNullOrEmpty(editor.DualTemplate.TemplateBBase64);
        editor.Template.ClearUnusedFields(editor.AngleMode);
        editor.DualTemplate.ClearUnusedFields(editor.AngleMode);
        editor.Blob.ClearUnusedFields(editor.AngleMode);
        if (carrySecondary is not null)
            editor.SecondarySearchRoi = carrySecondary;
        var clearedImage = hadImage && string.IsNullOrEmpty(editor.Template.TemplateImageBase64);
        var clearedDual = hadDual && string.IsNullOrEmpty(editor.DualTemplate.TemplateABase64);
        if (!trimmed && !clearedImage && !clearedDual)
            return null;

        var parts = new List<string>(3);
        if (trimmed)
            parts.Add("次模型已从名单移除");
        if (clearedImage)
            parts.Add("示教模板图已清除（当前角度/精修方法不使用）");
        if (clearedDual)
            parts.Add("双模板示教图已清除");
        return string.Join("；", parts);
    }

    private static Roi? ReadSecondarySearchRoi(RecipeConfig editor, AngleMode mode) => mode switch
    {
        AngleMode.DualBlobCenterLine => editor.Blob.SecondaryRoi,
        AngleMode.DualTemplateCenterLine => editor.DualTemplate.SecondaryRoi,
        _ => null,
    };
}
