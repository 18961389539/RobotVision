using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 删除设备（相机 / 光源控制器）前的配方引用检查：Cameras 与 Lightings 页面曾各有一份
/// 逐字相同的实现，仅取 Id 的字段不同。这里把「遍历配方 → 单个失败按未引用处理」的
/// 容错语义收敛到一处，两个页面只差一个字段选择器。
/// </summary>
internal static class RecipeReferenceCheck
{
    /// <summary>列出引用了指定设备 Id 的配方名；单个配方读取失败按未引用处理（不阻断删除流程）。</summary>
    public static List<string> FindReferencing(RecipeLoader loader, Func<RecipeConfig, string?> idOf, string id)
        => loader.ListNames()
            .Where(n =>
            {
                try { return string.Equals(idOf(loader.Get(n)), id, StringComparison.Ordinal); }
                catch { return false; }
            })
            .ToList();
}
