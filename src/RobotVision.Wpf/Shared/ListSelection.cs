using System.Collections.ObjectModel;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 列表页「刷新保选」公共骨架：Cameras / Lightings / Models 三个页面共用同一套
/// 「记下选中键 → 清空重建 → 按键找回」模式（ListBox 双向绑定下 Items.Clear() 会把选中置 null），
/// 抽到一处避免三份实现各自漂移（忽略大小写匹配、空白回退第一项这两处细节容易写漏）。
/// </summary>
internal static class ListSelection
{
    /// <summary>重建列表前计算要恢复的选中键：优先用调用方显式指定的 preferKey，否则沿用当前选中项的键。</summary>
    public static string? KeepKey(string? preferKey, string? currentKey) => preferKey ?? currentKey;

    /// <summary>重建列表后按键恢复选中：键为空白回退第一项；否则忽略大小写精确匹配（与配置 Id 惯例一致）。</summary>
    public static TItem? Restore<TItem>(IEnumerable<TItem> items, string? key, Func<TItem, string> keyOf)
        where TItem : class
        => string.IsNullOrWhiteSpace(key)
            ? items.FirstOrDefault()
            : items.FirstOrDefault(i => string.Equals(keyOf(i), key, StringComparison.OrdinalIgnoreCase));
}
