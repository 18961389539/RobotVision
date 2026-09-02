using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Models;

public partial class ModelsPage : Page
{
    public ModelsPage(ModelsViewModel viewModel)
    {
        ViewModelPageLifetime.Attach(this, viewModel);
        InitializeComponent();
        NumberBoxCommit.Bind(this, viewModel);
        Loaded += (_, _) => viewModel.ScheduleRefresh();
    }
}
