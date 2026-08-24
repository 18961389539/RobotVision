using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class FailuresPage : Page
{
    public FailuresPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(FailuresViewModel));
        // 产线持续产生失败现场：再次进入页面自动刷新到最新
        Loaded += (_, _) => { if (DataContext is FailuresViewModel vm) _ = vm.RefreshAsync(); };
    }
}

