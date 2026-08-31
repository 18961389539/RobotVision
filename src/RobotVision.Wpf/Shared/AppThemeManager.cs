using System.Windows;
using Wpf.Ui.Appearance;

namespace RobotVision.WpfHost.Shared;

/// <summary>应用级深浅主题切换与排版覆盖刷新。</summary>
internal static class AppThemeManager
{
    private static readonly Uri TypographyOverridesUri = new(
        "/RobotVision.Wpf;component/Shared/TypographyOverrides.xaml", UriKind.Relative);

    private static readonly Uri DarkPaletteUri = new(
        "/RobotVision.Wpf;component/Shared/DarkPaletteResources.xaml", UriKind.Relative);

    private static readonly Uri LightPaletteUri = new(
        "/RobotVision.Wpf;component/Shared/LightPaletteResources.xaml", UriKind.Relative);

    private static readonly Uri LightTooltipUri = new(
        "/RobotVision.Wpf;component/Shared/LightTooltipResources.xaml", UriKind.Relative);

    private static readonly Uri DarkTooltipUri = new(
        "/RobotVision.Wpf;component/Shared/DarkTooltipResources.xaml", UriKind.Relative);

    public static ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Dark;

    public static bool IsLight =>
        CurrentTheme == ApplicationTheme.Light;

    public static void Apply(string? uiTheme)
    {
        var theme = string.Equals(uiTheme, "Light", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;
        Apply(theme);
    }

    public static void Apply(ApplicationTheme theme)
    {
        CurrentTheme = theme;
        ApplicationThemeManager.Apply(theme);
        RefreshTypographyOverrides();
        RefreshTooltipChrome(theme);
        RefreshPalette(theme);
    }

    /// <summary>按主题二选一加载语义画刷字典（Brush.App*）；DynamicResource 引用随切换即时更新。</summary>
    private static void RefreshPalette(ApplicationTheme theme)
    {
        if (Application.Current?.Resources is not ResourceDictionary root)
            return;

        SwapMergedDictionary(root, DarkPaletteUri, LightPaletteUri, theme == ApplicationTheme.Light);
    }

    /// <summary>移除字典组中另一主题的版本，加载目标主题版本。</summary>
    private static void SwapMergedDictionary(ResourceDictionary root, Uri darkUri, Uri lightUri, bool useLight)
    {
        var dark = root.MergedDictionaries.FirstOrDefault(d => d.Source == darkUri);
        if (dark is not null)
            root.MergedDictionaries.Remove(dark);

        var light = root.MergedDictionaries.FirstOrDefault(d => d.Source == lightUri);
        if (light is not null)
            root.MergedDictionaries.Remove(light);

        root.MergedDictionaries.Add(new ResourceDictionary { Source = useLight ? lightUri : darkUri });
    }

    private static void RefreshTooltipChrome(ApplicationTheme theme)
    {
        if (Application.Current?.Resources is not ResourceDictionary root)
            return;

        SwapMergedDictionary(root, DarkTooltipUri, LightTooltipUri, theme == ApplicationTheme.Light);
    }

    public static void RefreshTypographyOverrides()
    {
        if (Application.Current?.Resources is not ResourceDictionary root)
            return;

        var existing = root.MergedDictionaries
            .FirstOrDefault(d => d.Source == TypographyOverridesUri);
        if (existing is not null)
            root.MergedDictionaries.Remove(existing);

        root.MergedDictionaries.Add(new ResourceDictionary { Source = TypographyOverridesUri });
    }
}
