using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using RobotVision.WpfHost;

namespace RobotVision.WpfHost.Features.Lightings;

public partial class LightingsPage : Page
{
    public LightingsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(LightingsViewModel));
        NumberBoxCommit.Bind(this, DataContext as LightingsViewModel);
        // 再次进入页面时刷新列表（运行时增删改后导航回来）
        Loaded += (_, _) => (DataContext as LightingsViewModel)?.Refresh();
    }
}
