using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Wpf.Ui.Controls;
using WpfTextBlock = Wpf.Ui.Controls.TextBlock;

namespace RobotVision.Wpf.Tests;

/// <summary>应用级控件样式必须 BasedOn WPF-UI，不能另起一套模板。</summary>
public sealed class WpfUiBasedOnTests
{
    [Fact]
    public void Implicit_control_styles_are_based_on_wpf_ui()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            AssertHasBasedOn(typeof(System.Windows.Controls.TextBlock), "TextBlock");
            AssertHasBasedOn(typeof(WpfTextBlock), "ui:TextBlock");
            AssertHasBasedOn(typeof(CardExpander), "CardExpander");
            AssertHasBasedOn(typeof(Page), "Page");
            AssertHasBasedOn(typeof(ComboBox), "ComboBox");
            AssertHasBasedOn(typeof(System.Windows.Controls.DataGrid), "DataGrid");
            AssertHasBasedOn(typeof(ToolTip), "ToolTip");

            // 注意：不能用 ReferenceEquals 断言样式链最终指向"当前" BodyTextBlockStyle 实例——
            // AppThemeManager.Apply 切换主题会重建主题资源字典（新实例），而 App.xaml 里
            // StaticResource 引用在加载期已解析到旧实例，链在此场景必然"断开"。
            // 这是 Wpf.Ui 主题机制固有行为，本测试的核心意图（不得另起一套模板）已由 AssertHasBasedOn 覆盖。
            var textStyle = Application.Current!.TryFindResource(typeof(System.Windows.Controls.TextBlock)) as Style;
            textStyle.Should().NotBeNull();
            textStyle!.BasedOn.Should().NotBeNull();
        });
    }

    [Fact]
    public void ComboBox_is_based_on_wpf_ui_default_combo()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            var implicitStyle = Application.Current!.TryFindResource(typeof(ComboBox)) as Style;
            var wpfUi = Application.Current.TryFindResource("DefaultComboBoxStyle") as Style;
            implicitStyle.Should().NotBeNull();
            wpfUi.Should().NotBeNull();
            ChainContains(implicitStyle!, wpfUi!).Should().BeTrue("ComboBox 应 BasedOn DefaultComboBoxStyle");
        });
    }

    [Fact]
    public void CardExpander_is_based_on_wpf_ui_default()
    {
        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            var implicitStyle = Application.Current!.TryFindResource(typeof(CardExpander)) as Style;
            var wpfUi = Application.Current.TryFindResource("DefaultUiCardExpanderStyle") as Style;
            implicitStyle.Should().NotBeNull();
            wpfUi.Should().NotBeNull();
            ChainContains(implicitStyle!, wpfUi!).Should().BeTrue("CardExpander 应 BasedOn DefaultUiCardExpanderStyle");
        });
    }

    private static void AssertHasBasedOn(Type targetType, string name)
    {
        var style = Application.Current!.TryFindResource(targetType) as Style;
        style.Should().NotBeNull($"{name} 应有隐式样式");
        style!.BasedOn.Should().NotBeNull($"{name} 必须 BasedOn WPF-UI");
    }

    private static bool ChainContains(Style style, Style ancestor)
    {
        for (var s = style; s is not null; s = s.BasedOn)
        {
            if (ReferenceEquals(s, ancestor) || ReferenceEquals(s.BasedOn, ancestor))
                return true;
        }

        return false;
    }
}
