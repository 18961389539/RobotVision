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
            }
        };
        Unloaded += (_, _) => (DataContext as AnalysisViewModel)?.StopTimer();
    }
}
