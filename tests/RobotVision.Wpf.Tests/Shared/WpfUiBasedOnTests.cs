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

            var textStyle = Application.Current!.TryFindResource(typeof(System.Windows.Controls.TextBlock)) as Style;
            var body = Application.Current.TryFindResource("BodyTextBlockStyle") as Style;
            body.Should().NotBeNull();
            ChainContains(textStyle!, body!).Should().BeTrue("TextBlock 应 BasedOn BodyTextBlockStyle");
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
