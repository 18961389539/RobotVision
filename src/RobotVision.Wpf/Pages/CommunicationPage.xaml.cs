using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class CommunicationPage : Page
{
    public CommunicationPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(CommunicationViewModel));
        Loaded += (_, _) => (DataContext as CommunicationViewModel)?.StartTimer();
        Unloaded += (_, _) => (DataContext as CommunicationViewModel)?.StopTimer();
    }
}
