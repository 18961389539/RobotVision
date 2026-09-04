using System.Collections.Specialized;
using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Monitor;

public partial class MonitorPage : Page
{
    private readonly MonitorViewModel _vm;
    private NotifyCollectionChangedEventHandler? _logsChanged;

    public MonitorPage(MonitorViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            _vm.MonitorActive = false;
            DetachLogSubscription();
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, _vm);
        Loaded += (_, _) =>
        {
            _vm.MonitorActive = true;
            _vm.RefreshCameras();
            DetachLogSubscription();
            AttachLogSubscription();
        };
        DataContextChanged += OnDataContextChanged;
    }

    private ListBox LogList => LogPanel.LogListControl;

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        DetachLogSubscription();
        AttachLogSubscription();
        ScrollLogToEnd();
    }

    private void AttachLogSubscription()
    {
        _logsChanged ??= OnLogsChanged;
        _vm.Logs.CollectionChanged += _logsChanged;
    }

    private void DetachLogSubscription()
    {
        if (_logsChanged is not null)
            _vm.Logs.CollectionChanged -= _logsChanged;
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm.AutoScroll)
            ScrollLogToEnd();
    }

    private void ScrollLogToEnd()
    {
        if (LogList.Items.Count == 0)
            return;
        LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
