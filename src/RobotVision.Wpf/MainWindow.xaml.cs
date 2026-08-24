using System.Windows;
using RobotVision.WpfHost.Pages;

namespace RobotVision.WpfHost;

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
