using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Lightings;

public partial class LightingsPage : Page
{
    public LightingsPage(LightingsViewModel viewModel)
    {
        ViewModelPageLifetime.Attach(this, viewModel);
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) => viewModel.Refresh();
    }
}
