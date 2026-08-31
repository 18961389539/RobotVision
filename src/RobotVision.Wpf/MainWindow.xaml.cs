using System.Windows;
using RobotVision.WpfHost.Features.Monitor;
using Wpf.Ui;

namespace RobotVision.WpfHost;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow(MainViewModel shellViewModel, IPageService pageService)
    {
        InitializeComponent();
        DataContext = shellViewModel;
        Nav.SetPageService(pageService);
        Loaded += (_, _) => Nav.Navigate(typeof(MonitorPage));
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }
}
