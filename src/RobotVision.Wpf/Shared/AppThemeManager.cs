using System.Windows;
using Wpf.Ui.Appearance;

namespace RobotVision.WpfHost.Shared;

/// <summary>应用级深浅主题切换与排版覆盖刷新。</summary>
internal static class AppThemeManager
{
    private static readonly Uri TypographyOverridesUri = new(
        "/RobotVision.Wpf;component/Shared/TypographyOverrides.xaml", UriKind.Relative);

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
    }

    private static void RefreshTooltipChrome(ApplicationTheme theme)
    {
        if (Application.Current?.Resources is not ResourceDictionary root)
            return;

        var dark = root.MergedDictionaries.FirstOrDefault(d => d.Source == DarkTooltipUri);
        if (dark is not null)
            root.MergedDictionaries.Remove(dark);

        var light = root.MergedDictionaries.FirstOrDefault(d => d.Source == LightTooltipUri);
        if (light is not null)
            root.MergedDictionaries.Remove(light);

        var uri = theme == ApplicationTheme.Light ? LightTooltipUri : DarkTooltipUri;
        root.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
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
