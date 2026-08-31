using System.Linq;
using System.Windows;
using System.Windows.Media;
using RobotVision.WpfHost.Shared;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RobotVision.Wpf.Tests;

/// <summary>深浅主题下标题与主文字须与底板对比可读。</summary>
public sealed class PanelTitleThemeTests
{
    [Fact]
    public void Dark_theme_primary_text_brush_is_light()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Dark);
            AssertPrimaryBrushLuma(expectLight: true);
        });
    }

    [Fact]
    public void Light_theme_primary_text_brush_is_dark()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Light);
            AssertPrimaryBrushLuma(expectLight: false);
        });
    }

    [Fact]
    public void NativeTextBlock_in_dark_window_gets_light_foreground()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Dark);
            AssertWindowTextBlockLuma(expectLight: true);
        });
    }

    [Fact]
    public void NativeTextBlock_in_light_window_gets_dark_foreground()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Light);
            AssertWindowTextBlockLuma(expectLight: false);
        });
    }

    [Fact]
    public void UiTextBlock_Subtitle_in_dark_theme_is_light()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Dark);
            AssertUiSubtitleLuma(expectLight: true);
        });
    }

    [Fact]
    public void UiTextBlock_Subtitle_in_light_theme_is_dark()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Light);
            AssertUiSubtitleLuma(expectLight: false);
        });
    }

    [Fact]
    public void SubtitleTextBlockStyle_uses_theme_primary_brush()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Dark);

            var style = Application.Current!.TryFindResource("SubtitleTextBlockStyle") as Style;
            Assert.NotNull(style);
            var fgSetter = style!.Setters
                .OfType<Setter>()
                .FirstOrDefault(s => s.Property == System.Windows.Controls.TextBlock.ForegroundProperty);
            Assert.NotNull(fgSetter);
            Assert.IsType<DynamicResourceExtension>(fgSetter!.Value);
        });
    }

    [Fact]
    public void UiTextBlock_BodyStrong_in_dark_theme_is_light()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AppThemeManager.Apply(ApplicationTheme.Dark);
            var window = new Window { Width = 200, Height = 100 };
            var tb = new TextBlock
            {
                Text = "参数面板",
                FontTypography = FontTypography.BodyStrong,
            };
            window.Content = tb;
            window.Show();
            window.UpdateLayout();
            var fg = tb.Foreground as SolidColorBrush;
            window.Close();
            Assert.True(fg is not null && RelativeLuma(fg.Color) > 0.7,
                $"BodyStrong fg={fg?.Color}");
        });
    }

    private static void AssertPrimaryBrushLuma(bool expectLight)
    {
        var brush = Application.Current!.TryFindResource("TextFillColorPrimaryBrush") as SolidColorBrush;
        Assert.True(brush is not null, "TextFillColorPrimaryBrush missing");
        var luma = RelativeLuma(brush!.Color);
        if (expectLight)
            Assert.True(luma > 0.7, $"dark theme primary={brush.Color}");
        else
            Assert.True(luma < 0.35, $"light theme primary={brush.Color}");
    }

    private static void AssertWindowTextBlockLuma(bool expectLight)
    {
        var window = new Window { Width = 200, Height = 100 };
        var tb = new System.Windows.Controls.TextBlock { Text = "x" };
        window.Content = tb;
        window.Show();
        tb.UpdateDefaultStyle();
        window.UpdateLayout();
        var fg = tb.Foreground as SolidColorBrush;
        window.Close();
        Assert.True(fg is not null);
        var luma = RelativeLuma(fg!.Color);
        if (expectLight)
            Assert.True(luma > 0.7, $"TextBlock fg={fg.Color}");
        else
            Assert.True(luma < 0.35, $"TextBlock fg={fg.Color}");
    }

    private static void AssertUiSubtitleLuma(bool expectLight)
    {
        var window = new Window { Width = 240, Height = 80 };
        var tb = new TextBlock
        {
            Text = "运行状态",
            FontTypography = FontTypography.Subtitle,
        };
        window.Content = tb;
        window.Show();
        window.UpdateLayout();
        var fg = tb.Foreground as SolidColorBrush;
        window.Close();
        Assert.True(fg is not null);
        var luma = RelativeLuma(fg!.Color);
        if (expectLight)
            Assert.True(luma > 0.7, $"Subtitle fg={fg.Color}");
        else
            Assert.True(luma < 0.35, $"Subtitle fg={fg.Color}");
    }

    private static double RelativeLuma(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
