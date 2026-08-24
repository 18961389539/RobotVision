using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class CalibrationPage : Page
{
    public CalibrationPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(CalibrationViewModel));
        // 向导保存新档案后回到本页时刷新
        Loaded += (_, _) => (DataContext as CalibrationViewModel)?.Refresh();
    }
}

