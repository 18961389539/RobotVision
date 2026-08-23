using System.Windows;
using RobotVision.UI.Pages;

namespace RobotVision.UI;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Nav.Navigate(typeof(MonitorPage));
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }
}
