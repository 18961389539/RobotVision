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
                dialogs.ConfirmDiscard(
                    "服务设置页有未保存的修改。选择「否」可返回继续编辑（若导航已切换，请手动回到本页）。",
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
