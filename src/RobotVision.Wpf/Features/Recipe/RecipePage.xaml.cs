using System.Windows;
using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

public partial class RecipePage : Page
{
    private readonly RecipeViewModel _vm;
    private RecipePageRoiCoordinator? _roiCoordinator;

    public RecipePage(RecipeViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            _vm.RequestTemplateRoiDraw = null;
            _vm.RequestDetectionRoiDraw = null;
            _vm.RequestSecondaryRoiDraw = null;
            _roiCoordinator?.Unwire();
            _vm.StopDirtyWatch();
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, _vm);

        Loaded += (_, _) =>
        {
            _vm.ScheduleListRefresh();
            _vm.RefreshCameras();
            _vm.RefreshStationIds();
            _vm.StartDirtyWatch();
            _vm.RequestTemplateRoiDraw = () => _roiCoordinator?.BeginRoiDraw(RecipeRoiDrawKind.Template);
            _vm.RequestDetectionRoiDraw = () => _roiCoordinator?.BeginRoiDraw(RecipeRoiDrawKind.Detection);
            _vm.RequestSecondaryRoiDraw = () => _roiCoordinator?.BeginRoiDraw(RecipeRoiDrawKind.SecondaryBlob);
            _roiCoordinator ??= new RecipePageRoiCoordinator(
                _vm,
                ImageViewport.TestViewerControl,
                ImageViewport.RoiViewerControl);
            _roiCoordinator.Wire();
        };
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return;
        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}
