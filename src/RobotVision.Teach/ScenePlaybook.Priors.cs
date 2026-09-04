using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Teach;

/// <summary>
/// ScenePlaybook —— 先验存取：从配方编辑器 / 模板政策序 / 结果库健康信号构造 <see cref="RecipePrior"/> 并合并。
/// 只产出推荐先验，不做分类与推荐本身。
/// </summary>
public static partial class ScenePlaybook
{
    /// <summary>打开配方向导时按当前编辑器预填任务约束。期望件数 0 表示不检查，不抬成 1。</summary>
    public static TaskConstraints FromRecipe(RecipeConfig recipe)
    {
        var mask = recipe.AngleMode == AngleMode.MaskTemplate;
        var refine = recipe.Template.RefineMethod;
        var directed = recipe.RotationCompensation == RotationCompensationMode.EccentricTool
                       || recipe.AngleMode is AngleMode.DualCenterLine or AngleMode.KeyPointLine
                           or AngleMode.DualBlobCenterLine
                       || (mask && refine != SegmentRefineMethod.LineFit);
        var teach = !string.IsNullOrEmpty(recipe.Template.TemplateImageBase64)
                    || (mask && TemplateOptions.NeedsTaughtImage(refine));
        return new TaskConstraints(
            directed,
            teach,
            AppearanceVaries: false,
            HasTwoLandmarks: recipe.AngleMode == AngleMode.DualCenterLine,
            UseBlobsWithoutModel: recipe.AngleMode == AngleMode.DualBlobCenterLine,
            ExpectedCount: Math.Clamp(recipe.Template.ExpectedCount, 0, 20));
    }

    public static RecipePrior? FromTemplate(TemplateOptions? template) =>
        template?.RefinePolicyOrder is { Count: > 0 } order ? new RecipePrior(order) : null;

    /// <summary>结果库信号压低当前精修方法；空信号且无政策序则返回 null。</summary>
    public static RecipePrior? FromHealth(
        bool refineFailHigh,
        bool angleBimodal,
        bool scoreDrift,
        SegmentRefineMethod? current,
        IReadOnlyList<SegmentRefineMethod>? policyOrder = null)
    {
        SegmentRefineMethod? down = null;
        var reasons = new List<string>();
        if (current is { } method)
        {
            if (refineFailHigh)
            {
                down = method;
                reasons.Add("1019 精修失败偏高");
            }

            if (angleBimodal)
            {
                down = method;
                reasons.Add("合格角呈 ±180° 双峰");
            }

            if (scoreDrift && TemplateOptions.NeedsTaughtImage(method))
            {
                down = method;
                reasons.Add("精修分相对示教峰下降");
            }
        }

        if (down is null && policyOrder is not { Count: > 0 })
            return null;
        return new RecipePrior(policyOrder is { Count: > 0 } ? policyOrder : null, down, string.Join("；", reasons));
    }

    public static RecipePrior? Merge(params RecipePrior?[] parts)
    {
        IReadOnlyList<SegmentRefineMethod>? order = null;
        SegmentRefineMethod? down = null;
        var reasons = new List<string>();
        foreach (var p in parts)
        {
            if (p is null)
                continue;
            if (p.PolicyOrder is { Count: > 0 })
                order = p.PolicyOrder;
            if (p.Downrank is { } d)
            {
                down = d;
                if (!string.IsNullOrEmpty(p.Reason))
                    reasons.Add(p.Reason);
            }
        }

        if (order is null && down is null)
            return null;
        return new RecipePrior(order, down, string.Join("；", reasons));
    }
}
