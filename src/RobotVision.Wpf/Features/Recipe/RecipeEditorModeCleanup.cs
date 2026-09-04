using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>切角度模式 / 精修方法后裁次模型、清当前方法不用的 Template 字段。</summary>
internal static class RecipeEditorModeCleanup
{
    /// <returns>给操作员看的短句；无需提示时为 null。</returns>
    public static string? Apply(RecipeConfig editor)
    {
        var trimmed = RecipeModelSlots.TrimToSingleModelSlot(editor);
        var hadImage = !string.IsNullOrEmpty(editor.Template.TemplateImageBase64);
        editor.Template.ClearUnusedFields(editor.AngleMode);
        var clearedImage = hadImage && string.IsNullOrEmpty(editor.Template.TemplateImageBase64);
        if (!trimmed && !clearedImage)
            return null;

        var parts = new List<string>(2);
        if (trimmed)
            parts.Add("次模型已从名单移除");
        if (clearedImage)
            parts.Add("示教模板图已清除（当前角度/精修方法不使用）");
        return string.Join("；", parts);
    }
}
