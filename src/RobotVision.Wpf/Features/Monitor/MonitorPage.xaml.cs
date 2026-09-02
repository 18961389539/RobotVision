using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
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
        catch (Exception ex)
        {
            // 剪贴板被其他进程占用时忽略，但留痕便于排查
            System.Diagnostics.Trace.TraceWarning("[Monitor] 复制日志到剪贴板失败: {0}", ex.Message);
        }
    }
}
