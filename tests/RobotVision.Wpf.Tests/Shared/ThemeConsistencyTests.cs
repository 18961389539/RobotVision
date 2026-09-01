using System.Windows;
using FluentAssertions;
using RobotVision.WpfHost.Shared;
using Wpf.Ui.Appearance;
using Xunit;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 主题切换一致性回归防护：
/// TypographyOverrides.xaml 的样式 setter 全部为 {DynamicResource}，主题跟随已由其保证，
/// 切主题时若重建该字典，会让 App.xaml 中 BasedOn="{StaticResource BodyTextBlockStyle}" 的
/// 引用指向被移除的旧实例（内容等价但引用错位）。本测试断言引用始终与当前生效实例一致。
/// </summary>
public sealed class ThemeConsistencyTests
{
    [Fact]
    public void Apply_KeepsStaticResourceChainAlignedWithCurrentInstance()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            var app = Application.Current!;

            // 切到浅色再切回深色：覆盖"切换后回原主题"场景
            AppThemeManager.Apply(ApplicationTheme.Light);
            AppThemeManager.Apply(ApplicationTheme.Dark);

            var appStyle = app.TryFindResource("AppLightTextBlockStyle") as Style;
            appStyle.Should().NotBeNull();
            var body = app.TryFindResource("BodyTextBlockStyle") as Style;
            body.Should().NotBeNull();

            // 修复目标：StaticResource 链与当前生效的 BodyTextBlockStyle 是同一实例。
            // 修复前 RefreshTypographyOverrides 重建字典，此处 ReferenceEquals 必然失败。
            appStyle!.BasedOn.Should().BeSameAs(body);
        });
    }

    [Fact]
    public void Apply_TogglesPaletteAndRestoresDarkSemanticBrushes()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            var app = Application.Current!;

            AppThemeManager.Apply(ApplicationTheme.Light);
            var lightText = app.TryFindResource("Brush.AppTextPrimary") as System.Windows.Media.SolidColorBrush;
            lightText.Should().NotBeNull();
            lightText!.Color.R.Should().BeLessThan(100); // 浅色主题文字为深色 (#1F242B)

            AppThemeManager.Apply(ApplicationTheme.Dark);
            var darkText = app.TryFindResource("Brush.AppTextPrimary") as System.Windows.Media.SolidColorBrush;
            darkText.Should().NotBeNull();
            darkText!.Color.R.Should().BeGreaterThan(200); // 深色主题文字为亮色 (#E8EDF4)
        });
    }
}
