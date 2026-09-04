using System.Windows;
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

    private void EditType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CamerasViewModel vm)
            vm.OnEditTypeSelectionChanged();
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is { } menu)
        {
            menu.PlacementTarget = btn;
            menu.IsOpen = true;
        }
    }
}
