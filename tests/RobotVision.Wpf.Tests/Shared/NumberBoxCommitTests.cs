using System.Windows.Controls;
using System.Windows.Data;
using FluentAssertions;
using RobotVision.WpfHost.Shared;
using Wpf.Ui.Controls;

namespace RobotVision.Wpf.Tests;

public sealed class NumberBoxCommitTests
{
    private sealed class Target
    {
        public double N { get; set; } = 1;
    }

    [Fact]
    public void Flush_UpdatesNumberBoxInUnselectedTab()
    {
        // 必须跑在 STA 上：构造 NumberBox/TabControl 会经 FrameworkElement.EnsureFrameworkServices
        // → InputManager，非 STA 直接抛「调用线程必须为 STA」。原先直接在测试线程（MTA）构造，
        // 该测试自 233f96f 起一直红着（已在父提交 f31c19b 上复现同样栈），
        // 会让真实回归淹没在背景噪声里。
        TestInfra.RunSta(() =>
        {
            var target = new Target();
            var box = new NumberBox();
            box.SetBinding(NumberBox.ValueProperty, new Binding(nameof(Target.N))
            {
                Source = target,
                Mode = BindingMode.TwoWay,
            });

            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "a", Content = new System.Windows.Controls.TextBlock { Text = "x" } });
            tabs.Items.Add(new TabItem { Header = "b", Content = box });
            tabs.SelectedIndex = 0;

            box.SetCurrentValue(NumberBox.ValueProperty, 7.0);
            target.N.Should().Be(1);

            NumberBoxCommit.Flush(tabs);
            target.N.Should().Be(7);
        });
    }
}
