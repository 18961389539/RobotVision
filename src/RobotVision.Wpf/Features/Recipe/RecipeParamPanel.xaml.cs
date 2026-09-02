using System.Windows;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Features.Recipe;

public partial class RecipeParamPanel : UserControl
{
    public RecipeParamPanel() => InitializeComponent();

    public event RoutedEventHandler? DrawRoiRegionRequested;

    private void OnDrawRoiRegionClick(object sender, RoutedEventArgs e) =>
        DrawRoiRegionRequested?.Invoke(this, e);
}
