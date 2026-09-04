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
    }
}
