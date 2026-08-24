using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class CamerasPage : Page
{
    public CamerasPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(CamerasViewModel));
        // 再次进入页面时刷新列表（运行时增删改后导航回来）
        Loaded += (_, _) => (DataContext as CamerasViewModel)?.Refresh();
    }
}
