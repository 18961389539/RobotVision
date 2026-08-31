using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Analysis;

public partial class AnalysisPage : Page
{
    private readonly AnalysisViewModel _vm;

    public AnalysisPage(AnalysisViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () => _vm.StopTimer());
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _vm.ScheduleRefresh();
            _vm.StartTimer();
            _ = Dispatcher.InvokeAsync(_vm.InvalidatePlots, System.Windows.Threading.DispatcherPriority.Loaded);
        };
    }

    private void OnAnalysisTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        _ = Dispatcher.InvokeAsync(_vm.InvalidatePlots, System.Windows.Threading.DispatcherPriority.Render);
    }
}
