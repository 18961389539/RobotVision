using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using RobotVision.WpfHost.Shared;
using Wpf.Ui.Appearance;

namespace RobotVision.Wpf.Tests;

/// <summary>ToolTip 在深浅主题下均须不透明且前景/背景对比清晰。</summary>
public sealed class ToolTipThemeTests
{
    [Fact]
    public void Dark_theme_tool_tip_uses_opaque_panel_and_light_text()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Dark);
            var tooltip = CreateStyledToolTip();

            var bg = tooltip.Background as SolidColorBrush;
            bg.Should().NotBeNull();
            bg!.Color.A.Should().Be(255);
            RelativeLuma(bg.Color).Should().BeLessThan(0.35);

            var fg = tooltip.Foreground as SolidColorBrush;
            fg.Should().NotBeNull();
            RelativeLuma(fg!.Color).Should().BeGreaterThan(0.7);
        });
    }

    [Fact]
    public void Light_theme_tool_tip_uses_opaque_panel_and_dark_text()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Light);
            var tooltip = CreateStyledToolTip();

            var bg = tooltip.Background as SolidColorBrush;
            bg.Should().NotBeNull();
            bg!.Color.A.Should().Be(255);
            RelativeLuma(bg.Color).Should().BeGreaterThan(0.55);

            var fg = tooltip.Foreground as SolidColorBrush;
            fg.Should().NotBeNull();
            RelativeLuma(fg!.Color).Should().BeLessThan(0.35);
        });
    }

    private static ToolTip CreateStyledToolTip()
    {
        var tooltip = new ToolTip { Content = "期望件数" };
        tooltip.UpdateDefaultStyle();
        tooltip.ApplyTemplate();
        return tooltip;
    }

    private static double RelativeLuma(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
