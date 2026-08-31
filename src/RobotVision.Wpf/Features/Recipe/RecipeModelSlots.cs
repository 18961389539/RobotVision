using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>把 UI 主/次模型槽写回 <see cref="RecipeConfig.Models"/>，保存时不静默丢弃第 3 个及以后。</summary>
internal static class RecipeModelSlots
{
    public static string? TryCommitUiModels(RecipeConfig editor, string primary, string secondary)
    {
        WriteSlot(editor.Models, 0, primary);
        if (editor.AngleMode == AngleMode.DualCenterLine)
            WriteSlot(editor.Models, 1, secondary);

        TrimTrailingEmpty(editor.Models);

        return editor.AngleMode switch
        {
            AngleMode.DualCenterLine => GuardExtraModels(editor, maxSlots: 2, modeLabel: "双模型"),
            AngleMode.DualBlobCenterLine => null,
            _ => NormalizeSingleModelSlot(editor, primary),
        };
    }

    private static string? GuardExtraModels(RecipeConfig editor, int maxSlots, string modeLabel)
    {
        if (editor.Models.Count <= maxSlots)
            return null;

        var extras = editor.Models.Skip(maxSlots)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToList();
        if (extras.Count == 0)
        {
            editor.Models = editor.Models.Take(maxSlots).ToList();
            return null;
        }

        return $"{modeLabel}模式最多 {maxSlots} 个模型；配方中还留有：{string.Join("、", extras)}。"
               + "请删掉多余项、改 JSON，或仅在「双 BLOB」模式下保留额外模型后再保存。";
    }

    private static string? NormalizeSingleModelSlot(RecipeConfig editor, string primary)
    {
        var extras = editor.Models
            .Select((model, index) => (model, index))
            .Where(t => t.index > 0 && !string.IsNullOrWhiteSpace(t.model))
            .Select(t => t.model)
            .ToList();
        if (extras.Count > 0)
            return $"当前角度模式只需 1 个模型；配方里还有多余项：{string.Join("、", extras)}。";

        var chosen = string.IsNullOrWhiteSpace(primary)
            ? editor.Models.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? ""
            : primary;
        editor.Models = string.IsNullOrWhiteSpace(chosen) ? [] : [chosen];
        return null;
    }

    private static void WriteSlot(List<string> models, int index, string? value)
    {
        while (models.Count <= index)
            models.Add("");
        models[index] = value ?? "";
    }

    private static void TrimTrailingEmpty(List<string> models)
    {
        while (models.Count > 0 && string.IsNullOrWhiteSpace(models[^1]))
            models.RemoveAt(models.Count - 1);
    }
}
