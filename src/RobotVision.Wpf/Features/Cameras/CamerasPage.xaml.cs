using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Cameras;

public partial class CamerasPage : Page
{
    private readonly CamerasViewModel _vm;

    public CamerasPage(CamerasViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () => _vm.StopPreview());
        InitializeComponent();
        NumberBoxCommit.Bind(this, _vm);
        Loaded += (_, _) => _vm.Refresh();
    }

    private void EditType_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _vm.OnEditTypeSelectionChanged();
}
