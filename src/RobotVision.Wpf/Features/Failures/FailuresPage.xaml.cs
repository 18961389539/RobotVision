using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Failures;

public partial class FailuresPage : Page
{
    public FailuresPage(FailuresViewModel viewModel)
    {
        ViewModelPageLifetime.Attach(this, viewModel);
        InitializeComponent();
        Loaded += (_, _) => viewModel.ScheduleRefresh();
    }
}
