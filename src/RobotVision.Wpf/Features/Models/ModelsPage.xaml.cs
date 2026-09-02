using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Models;

public partial class ModelsPage : Page
{
    public ModelsPage(ModelsViewModel viewModel, IDialogService dialogs)
    {
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            if (viewModel.HasUnsavedChanges)
            {
                dialogs.ConfirmDiscard(
                    "模型页有未保存的测试偏好设置。选择「否」可返回继续编辑（若导航已切换，请手动回到本页）。",
                    "未保存修改");
            }
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) => viewModel.ScheduleRefresh();
    }
}
