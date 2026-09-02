using System.Windows.Controls;

namespace RobotVision.WpfHost.Features.Cameras;

public partial class CameraParamPanel : UserControl
{
    public CameraParamPanel() => InitializeComponent();

    private void EditType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CamerasViewModel vm)
            vm.OnEditTypeSelectionChanged();
    }
}
