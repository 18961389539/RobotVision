using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.UI.Pages;

public partial class SystemPage : Page
{
    public SystemPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(SystemViewModel));
        // 离开页面停止轮询；再次进入（缓存实例）时重启
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SystemViewModel vm)
            vm.StartTimer();
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SystemViewModel vm)
            vm.StopTimer();
    }
}
