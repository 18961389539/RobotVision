using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class ModelsPage : Page
{
    public ModelsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(ModelsViewModel));
        // 模型目录可能被外部更新：再次进入页面刷新文件与会话状态
        Loaded += (_, _) => (DataContext as ModelsViewModel)?.Refresh();
    }
}

