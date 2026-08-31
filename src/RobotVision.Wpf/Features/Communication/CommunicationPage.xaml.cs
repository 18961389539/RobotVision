using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Communication;

public partial class CommunicationPage : Page
{
    public CommunicationPage(CommunicationViewModel viewModel)
    {
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () => viewModel.StopTimer());
        InitializeComponent();
        Loaded += (_, _) => viewModel.StartTimer();
    }
}
