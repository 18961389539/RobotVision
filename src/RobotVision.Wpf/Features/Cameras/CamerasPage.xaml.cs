using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using RobotVision.WpfHost;

namespace RobotVision.WpfHost.Features.Cameras;

public partial class CamerasPage : Page
{
    public CamerasPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(CamerasViewModel));
        // 再次进入页面时刷新列表（运行时增删改后导航回来）
        Loaded += (_, _) => (DataContext as CamerasViewModel)?.Refresh();
        // 离开页面必须停预览：ViewModel 是进程级单例，预览定时器不随页面销毁，
        // 不停会在后台持续 Grab 与产线取图争用相机
        Unloaded += (_, _) => (DataContext as CamerasViewModel)?.StopPreview();
    }

    private void EditType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        (DataContext as CamerasViewModel)?.OnEditTypeSelectionChanged();
    }
}
