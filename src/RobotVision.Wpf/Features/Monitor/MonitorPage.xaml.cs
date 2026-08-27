using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RobotVision.WpfHost;

namespace RobotVision.WpfHost.Features.Monitor;

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

    private void OnLogListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(LogList, source) is not ListBoxItem)
            return;
        if (LogList.SelectedItem is not LogLine line)
            return;
        try
        {
            Clipboard.SetText(line.ClipboardText);
        }
        catch
        {
            // 剪贴板被其他进程占用时忽略
        }
    }
}
