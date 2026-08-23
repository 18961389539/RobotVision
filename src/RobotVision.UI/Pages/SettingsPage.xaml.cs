using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace RobotVision.UI.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(SettingsViewModel));
        // 进入页面：重新同步运行时配置（丢弃未保存残留、反映外部改动），再启动状态轮询
        Loaded += (_, _) =>
        {
            (DataContext as SettingsViewModel)?.LoadFromRuntime();
            (DataContext as SettingsViewModel)?.StartTimer();
        };
        Unloaded += (_, _) =>
        {
            var vm = DataContext as SettingsViewModel;
            vm?.StopTimer();
            // 未保存修改提示（不阻止导航：导航已发生，仅提醒避免静默丢失）
            if (vm?.HasUnsavedChanges == true)
                MessageBox.Show("服务设置页有未保存的修改，已丢失。如需保留请返回后点击「保存并应用」。",
                    "未保存修改", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
    }
}
