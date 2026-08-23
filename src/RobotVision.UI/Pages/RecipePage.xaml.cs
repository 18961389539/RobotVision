using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.UI.Pages;

public partial class RecipePage : Page
{
    public RecipePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(RecipeViewModel));
        // 再次进入页面时刷新相机下拉（相机管理页增删相机后）
        Loaded += (_, _) => (DataContext as RecipeViewModel)?.RefreshCameras();
    }
}

