using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// WPF-UI 3.1 NumberBox 用 SetCurrentValue 更新 Value，不会写回绑定源。
/// 输入后立刻点按钮时，ViewModel 仍是旧值（序列号变回 0、阈值/位姿未生效）。
/// </summary>
internal interface ICommitPendingEdits
{
    Action? FlushPendingEdits { get; set; }
}

internal static class NumberBoxCommit
{
    public static void Flush(DependencyObject? root)
    {
        foreach (var box in Walk<NumberBox>(root))
            box.GetBindingExpression(NumberBox.ValueProperty)?.UpdateSource();
    }

    /// <summary>页面 Loaded 时挂上 Flush；Unloaded 时断开，避免单例 VM 指到已卸载页面。</summary>
    public static void Bind(FrameworkElement page, ICommitPendingEdits? target)
    {
        if (target is null)
            return;
        page.Loaded += (_, _) => target.FlushPendingEdits = () => Flush(page);
        page.Unloaded += (_, _) => target.FlushPendingEdits = null;
    }

    public static void Commit(this ICommitPendingEdits? target) =>
        target?.FlushPendingEdits?.Invoke();

    private static IEnumerable<T> Walk<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null)
            yield break;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var nested in Walk<T>(child))
                yield return nested;
        }
    }
}
