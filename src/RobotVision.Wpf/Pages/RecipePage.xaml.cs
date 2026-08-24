using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class RecipePage : Page
{
    public RecipePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(RecipeViewModel));
        // 再次进入页面时刷新相机下拉（相机管理页增删相机后）
        Loaded += (_, _) => (DataContext as RecipeViewModel)?.RefreshCameras();
    }

    /// <summary>角度模式/精修方法切换：Editor 属性级变更不触发 OnEditorChanged，
    /// 此处通知 ViewModel 刷新按模式显隐的派生属性（IsDualMode/IsTemplateMethod 等）。</summary>
    private void AngleMode_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        (DataContext as RecipeViewModel)?.NotifyAngleModeChanged();
}

