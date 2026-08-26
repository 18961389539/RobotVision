using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class MonitorPage : Page
{
    private MainViewModel? _viewModel;

    public MonitorPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DataContext = App.Services.GetRequiredService(typeof(MainViewModel));
        NumberBoxCommit.Bind(this, DataContext as MainViewModel);
        Loaded += (_, _) =>
        {
            SetMonitorActive(true);
            (DataContext as MainViewModel)?.RefreshCameras();
            DetachLogSubscription();
            AttachLogSubscription();
        };
        Unloaded += (_, _) =>
        {
            SetMonitorActive(false);
            DetachLogSubscription();
        };
    }

    private void SetMonitorActive(bool value)
    {
        if (DataContext is MainViewModel vm)
            vm.MonitorActive = value;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachLogSubscription();
        _viewModel = DataContext as MainViewModel;
        AttachLogSubscription();
        ScrollLogToEnd();
    }

    private void AttachLogSubscription()
    {
        if (_viewModel is null)
            return;
        _viewModel.Logs.CollectionChanged += OnLogsChanged;
    }

    private void DetachLogSubscription()
    {
        if (_viewModel is not null)
            _viewModel.Logs.CollectionChanged -= OnLogsChanged;
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel?.AutoScroll == true)
            ScrollLogToEnd();
    }

    private void ScrollLogToEnd()
    {
        if (LogList.Items.Count == 0)
            return;
        LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
