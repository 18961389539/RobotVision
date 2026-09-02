using System.Windows.Controls;
using ViewerControl = ImageViewer.Controls.ImageViewer;

namespace RobotVision.WpfHost.Features.Recipe;

public partial class RecipeImageViewport : UserControl
{
    public RecipeImageViewport() => InitializeComponent();

    public ViewerControl TestViewerControl => TestViewer;

    public ViewerControl RoiViewerControl => RoiViewer;
}
