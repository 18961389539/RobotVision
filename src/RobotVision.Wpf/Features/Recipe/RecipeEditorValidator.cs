using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方编辑器前置校验（试触发 / 保存），与产线 <see cref="RecipeLoader.Validate"/> 口径一致。</summary>
internal static class RecipeEditorValidator
{
    public static string? TryValidateForTrigger(RecipeConfig recipe, RecipeLoader? loader = null)
    {
        var probe = recipe.Clone();
        if (string.IsNullOrWhiteSpace(probe.Name))
            probe.Name = "preview";
        try
        {
            RecipeLoader.Validate(probe);
            loader?.ValidateReferences(probe);
            return null;
        }
        catch (InvalidRecipeException ex)
        {
            return ex.Message;
        }
    }

    public static string? TryValidateForSave(RecipeConfig recipe, RecipeLoader loader, bool requireName = true)
    {
        if (requireName && string.IsNullOrWhiteSpace(recipe.Name))
            return "请先填写配方名称";
        if (requireName && !RecipeLoader.IsValidRecipeName(recipe.Name))
            return "名称只允许字母、数字、下划线、中划线（长度 ≤ 64）";

        try
        {
            var probe = recipe.Clone();
            if (string.IsNullOrWhiteSpace(probe.Name))
                probe.Name = "_draft_";
            RecipeLoader.Validate(probe);
            loader.ValidateReferences(probe);
            return null;
        }
        catch (InvalidRecipeException ex)
        {
            return ex.Message;
        }
    }
}
