using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using RobotVision.WpfHost;

namespace RobotVision.Wpf.Tests;

/// <summary>暗色主题下 ToolTip 必须用不透明深色底板，避免系统 InfoBrush 浅底配全局浅色字。</summary>
public sealed class ToolTipThemeTests
{
    private static readonly object AppLock = new();

    [Fact]
    public void ToolTip_implicit_style_uses_opaque_dark_background()
    {
        TestInfra.RunSta(() =>
        {
            EnsureAppResources();
            var tooltip = new ToolTip { Content = "关闭（默认）：精修不过门返回 1019" };
            tooltip.UpdateDefaultStyle();

            var bg = tooltip.Background as SolidColorBrush;
            bg.Should().NotBeNull("隐式 ToolTip 样式应给出实心背景");
            AssertDarkOpaque(bg!.Color, "ToolTip.Background");

            var fg = tooltip.Foreground as SolidColorBrush;
            fg.Should().NotBeNull();
            RelativeLuma(fg!.Color).Should().BeGreaterThan(0.7, "提示文字应为浅色");
        });
    }

    [Fact]
    public void ToolTip_template_has_no_light_chrome()
    {
        TestInfra.RunSta(() =>
        {
            EnsureAppResources();
            var tooltip = new ToolTip { Content = "期望件数" };
            tooltip.UpdateDefaultStyle();
            tooltip.Measure(new Size(420, 200));
            tooltip.Arrange(new Rect(tooltip.DesiredSize));
            tooltip.ApplyTemplate();

            var light = FindLightFill(tooltip);
            light.Should().BeNull($"模板内不应残留浅色底板，实际 {light}");
        });
    }

    private static void EnsureAppResources()
    {
        lock (AppLock)
        {
            if (Application.Current is App existing)
            {
                _ = existing.TryFindResource(typeof(ToolTip));
                return;
            }

            if (Application.Current is not null)
                return;

            var app = new App();
            app.InitializeComponent();
        }
    }

    private static string? FindLightFill(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var d = queue.Dequeue();
            if (d is Border border && border.Background is SolidColorBrush brush && IsLight(brush.Color))
                return $"Border {brush.Color}";
            if (d is Control control && control.Background is SolidColorBrush cb && IsLight(cb.Color))
                return $"{control.GetType().Name} {cb.Color}";

            var n = VisualTreeHelper.GetChildrenCount(d);
            for (var i = 0; i < n; i++)
                queue.Enqueue(VisualTreeHelper.GetChild(d, i));
        }

        return null;
    }

    private static void AssertDarkOpaque(Color color, string name)
    {
        color.A.Should().Be(255, $"{name} 必须不透明，避免浅色 Popup 铬透出来");
        RelativeLuma(color).Should().BeLessThan(0.35, $"{name}={color} 应为深色");
    }

    private static bool IsLight(Color color) => color.A > 8 && RelativeLuma(color) > 0.55;

    private static double RelativeLuma(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
