using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using RobotVision.WpfHost;

namespace RobotVision.WpfHost.Features.Analysis;

public partial class AnalysisPage : Page
{
    public AnalysisPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(AnalysisViewModel));
        Loaded += (_, _) =>
        {
            if (DataContext is AnalysisViewModel vm)
            {
                _ = vm.RefreshAsync();
                vm.StartTimer();
                _ = Dispatcher.InvokeAsync(vm.InvalidatePlots, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };
        Unloaded += (_, _) => (DataContext as AnalysisViewModel)?.StopTimer();
    }

    private void OnAnalysisTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || DataContext is not AnalysisViewModel vm)
            return;
        _ = Dispatcher.InvokeAsync(vm.InvalidatePlots, System.Windows.Threading.DispatcherPriority.Render);
    }
}
