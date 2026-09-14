using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace RobotVision.WpfHost.Shared;

internal static class UiDispatch
{
    public static void Begin(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}

/// <summary>非空字符串 → Visible；空/null → Collapsed（表单预警行用）。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>非 null → Visible；null → Collapsed（结果图/预览图占位用）。</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>false → Visible；true → Collapsed（ROI 比例回退输入在取到像素参考帧后隐藏）。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

/// <summary>true → false；false → true（busy 时禁用按钮用）。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>
/// 字段级校验错误着色：true（该字段正是当前出错项）→ 错误红；否则 → 常规文本/描边色。
/// 配合 ViewModel 的索引器绑定使用，例如 <c>Foreground="{Binding [MaxConcurrent], Converter=...}"</c>，
/// 使「保存失败」能直接落到具体标签上，而不是只给一句笼统提示。
/// <c>ConverterParameter="border"</c> 时返回控件描边色（用于 TextBox 边框）。
/// </summary>
public sealed class FieldErrorBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var error = value is true;
        if (error)
            return Lookup("SystemFillColorCriticalBrush", Brushes.IndianRed);

        return parameter as string == "border"
            ? Lookup("ControlStrokeColorDefaultBrush", Brushes.Gray)
            : Lookup("TextFillColorPrimaryBrush", Brushes.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Lookup(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;
}

/// <summary>true → Primary；false → Secondary。用于「有未保存改动时把保存按钮提亮」。</summary>
public sealed class BoolToControlAppearanceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ControlAppearance.Primary : ControlAppearance.Secondary;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
