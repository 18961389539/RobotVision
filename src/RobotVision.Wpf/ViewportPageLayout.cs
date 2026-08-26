using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RobotVision.WpfHost;

/// <summary>
/// 全屏图像页布局辅助：撑满导航内容区、禁用 NavigationView 外层滚动、
/// 将滚轮限制在参数悬浮窗内的 ScrollViewer。
/// </summary>
public static class ViewportPageLayout
{
    public static readonly DependencyProperty FillViewportProperty =
        DependencyProperty.RegisterAttached(
            "FillViewport",
            typeof(bool),
            typeof(ViewportPageLayout),
            new PropertyMetadata(false, OnFillViewportChanged));

    public static void SetFillViewport(DependencyObject element, bool value) =>
        element.SetValue(FillViewportProperty, value);

    public static bool GetFillViewport(DependencyObject element) =>
        (bool)element.GetValue(FillViewportProperty);

    public static readonly DependencyProperty IsolateWheelProperty =
        DependencyProperty.RegisterAttached(
            "IsolateWheel",
            typeof(bool),
            typeof(ViewportPageLayout),
            new PropertyMetadata(false, OnIsolateWheelChanged));

    public static void SetIsolateWheel(DependencyObject element, bool value) =>
        element.SetValue(IsolateWheelProperty, value);

    public static bool GetIsolateWheel(DependencyObject element) =>
        (bool)element.GetValue(IsolateWheelProperty);

    private static readonly DependencyProperty OuterScrollStateProperty =
        DependencyProperty.RegisterAttached(
            "OuterScrollState",
            typeof(ScrollBarVisibility?),
            typeof(ViewportPageLayout));

    private static void OnFillViewportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Page page) return;
        if (e.NewValue is true)
        {
            page.Loaded += OnPageLoaded;
            page.Unloaded += OnPageUnloaded;
        }
        else
        {
            page.Loaded -= OnPageLoaded;
            page.Unloaded -= OnPageUnloaded;
            RestoreOuterScroll(page);
        }
    }

    private static void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Page page) return;
        page.Dispatcher.BeginInvoke(() => SuppressOuterScroll(page), DispatcherPriority.Loaded);
    }

    private static void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Page page) return;
        RestoreOuterScroll(page);
    }

    private static void SuppressOuterScroll(Page page)
    {
        var outer = FindOuterScrollViewer(page);
        if (outer is null)
            return;

        if (page.ReadLocalValue(OuterScrollStateProperty) == DependencyProperty.UnsetValue)
            page.SetValue(OuterScrollStateProperty, outer.VerticalScrollBarVisibility);

        outer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        outer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private static void RestoreOuterScroll(Page page)
    {
        var outer = FindOuterScrollViewer(page);
        if (outer is null)
            return;

        if (page.ReadLocalValue(OuterScrollStateProperty) is ScrollBarVisibility prev)
        {
            outer.VerticalScrollBarVisibility = prev;
            page.ClearValue(OuterScrollStateProperty);
        }
    }

    /// <summary>NavigationView 外层包裹 Page 的 ScrollViewer（不是页面内参数栏的）。</summary>
    private static ScrollViewer? FindOuterScrollViewer(DependencyObject start)
    {
        var parent = VisualTreeHelper.GetParent(start);
        while (parent is not null)
        {
            if (parent is ScrollViewer sv)
                return sv;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private static void OnIsolateWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;
        if (e.NewValue is true)
            sv.PreviewMouseWheel += OnIsolateWheel;
        else
            sv.PreviewMouseWheel -= OnIsolateWheel;
    }

    private static void OnIsolateWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (e.Delta > 0)
            sv.LineUp();
        else
            sv.LineDown();
        e.Handled = true;
    }
}
