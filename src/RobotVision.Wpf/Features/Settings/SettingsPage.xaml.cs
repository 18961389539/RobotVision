using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Settings;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel, IDialogService dialogs)
    {
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            viewModel.StopTimer();
            if (viewModel.HasUnsavedChanges)
            {
                dialogs.ShowWarning(
                    "服务设置页有未保存的修改，已丢失。如需保留请返回后点击「保存并应用」。",
                    "未保存修改");
            }
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) =>
        {
            viewModel.LoadFromRuntime();
            viewModel.StartTimer();
        };
    }
}
