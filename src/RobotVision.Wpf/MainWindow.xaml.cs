using System.Windows;
using RobotVision.WpfHost.Features.Monitor;
using RobotVision.WpfHost.Shared;
using Wpf.Ui;

namespace RobotVision.WpfHost;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow(ShellViewModel shellViewModel, IPageService pageService)
    {
        InitializeComponent();
        DataContext = shellViewModel;
        Nav.SetPageService(pageService);
        NavigationShortcuts.Register(this, pageType => Nav.Navigate(pageType));
        Loaded += (_, _) => Nav.Navigate(typeof(MonitorPage));
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }
}
