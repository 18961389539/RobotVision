using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Calibration;

public partial class CalibrationPage : Page
{
    public CalibrationPage(CalibrationViewModel viewModel)
    {
        ViewModelPageLifetime.Attach(this, viewModel);
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) => viewModel.Refresh();
    }
}
