using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Calibration;

public partial class CalibrationPage : Page
{
    public CalibrationPage(CalibrationViewModel viewModel, IDialogService dialogs)
    {
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            if (viewModel.HasUnsavedChanges)
            {
                dialogs.ConfirmDiscard(
                    "标定页有未保存的修改。选择「否」可返回继续编辑（若导航已切换，请手动回到本页）。",
                    "未保存修改");
            }
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) => viewModel.ScheduleRefresh();
    }
}
