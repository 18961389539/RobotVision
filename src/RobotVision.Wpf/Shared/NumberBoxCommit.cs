using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
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

    /// <summary>页面 Loaded 时挂上 Flush；真正离开时断开，避免单例 VM 指到已卸载页面。
    /// 窗口若已 Loaded（DataContext 后绑）则立即挂上。
    /// Unloaded 可能是主题切换误触发：延迟清空，并在仍 Loaded 时重新挂上。</summary>
    public static void Bind(FrameworkElement page, ICommitPendingEdits? target)
    {
        if (target is null)
            return;
        void Attach() => target.FlushPendingEdits = () => Flush(page);
        page.Loaded += (_, _) => Attach();
        page.Unloaded += (_, _) =>
        {
            target.FlushPendingEdits?.Invoke();
            page.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (page.IsLoaded)
                    Attach();
                else
                    target.FlushPendingEdits = null;
            });
        };
        if (page.IsLoaded)
            Attach();
    }

    public static void Commit(this ICommitPendingEdits? target) =>
        target?.FlushPendingEdits?.Invoke();

    /// <summary>
    /// 同时走逻辑树与视觉树。驾驶舱 TabControl 只把选中页放进视觉树，
    /// 未选中 Tab 上的 NumberBox 只能从 TabItem.Content 的逻辑树刷到。
    /// </summary>
    internal static IEnumerable<T> Walk<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null)
            yield break;

        var seen = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!seen.Add(node))
                continue;
            if (node is T match)
                yield return match;

            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
                stack.Push(child);

            if (node is not Visual)
                continue;
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
    }
}
