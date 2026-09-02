using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 删除设备（相机 / 光源控制器）前的配方引用检查：Cameras 与 Lightings 页面曾各有一份
/// 逐字相同的实现，仅取 Id 的字段不同。这里把「遍历配方 → 单个失败按被引用处理」的
/// fail-safe 语义收敛到一处，两个页面只差一个字段选择器。
/// </summary>
internal static class RecipeReferenceCheck
{
    /// <summary>列出引用了指定设备 Id 的配方名；单个配方读取失败按「被引用」处理（fail-safe：阻止删除，
    /// 宁可误拦也不放行删掉产线在用设备——文件损坏/锁定时不能判定为"无引用"）。</summary>
    public static List<string> FindReferencing(RecipeLoader loader, Func<RecipeConfig, string?> idOf, string id)
        => loader.ListNames()
            .Where(n =>
            {
                try { return string.Equals(idOf(loader.Get(n)), id, StringComparison.Ordinal); }
                catch { return true; }
            })
            .ToList();
}
