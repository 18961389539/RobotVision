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
            _roiCoordinator?.Unwire();
            _vm.StopDirtyWatch();
        });
        InitializeComponent();
        ParamPanel.DrawRoiRegionRequested += OnDrawRoiRegionClick;
        NumberBoxCommit.Bind(this, _vm);

        Loaded += (_, _) =>
        {
            _vm.RefreshCameras();
            _vm.RefreshStationIds();
            _vm.StartDirtyWatch();
            _vm.RequestTemplateRoiDraw = () => _roiCoordinator?.BeginRoiDraw(template: true);
            _roiCoordinator ??= new RecipePageRoiCoordinator(
                _vm,
                ImageViewport.TestViewerControl,
                ImageViewport.RoiViewerControl);
            _roiCoordinator.Wire();
        };
    }

    private void OnDrawRoiRegionClick(object sender, RoutedEventArgs e) =>
        _roiCoordinator?.BeginRoiDraw(template: false);
}
