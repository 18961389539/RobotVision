using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Cameras;

public partial class CamerasPage : Page
{
    public CamerasPage(CamerasViewModel viewModel)
    {
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () => viewModel.StopPreview());
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) => viewModel.ScheduleRefresh();
    }
}
