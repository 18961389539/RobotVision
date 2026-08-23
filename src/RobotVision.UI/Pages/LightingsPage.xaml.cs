using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.UI.Pages;

public partial class LightingsPage : Page
{
    public LightingsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(LightingsViewModel));
        // 再次进入页面时刷新列表（运行时增删改后导航回来）
        Loaded += (_, _) => (DataContext as LightingsViewModel)?.Refresh();
    }
}
