using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.SystemInfo;

public partial class SystemPage : Page
{
    public SystemPage(SystemViewModel viewModel)
    {
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () => viewModel.StopTimer());
        InitializeComponent();
        Loaded += (_, _) => viewModel.StartTimer();
    }
}
