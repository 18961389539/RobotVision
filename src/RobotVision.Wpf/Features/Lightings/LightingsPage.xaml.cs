using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Lightings;

public partial class LightingsPage : Page
{
    public LightingsPage(LightingsViewModel viewModel, IDialogService dialogs)
    {
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            if (viewModel.HasUnsavedChanges)
            {
                dialogs.ConfirmDiscard(
                    "光源页有未保存的编辑参数。选择「否」可返回继续编辑（若导航已切换，请手动回到本页）。",
                    "未保存修改");
            }
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) => viewModel.ScheduleRefresh();
    }
}
