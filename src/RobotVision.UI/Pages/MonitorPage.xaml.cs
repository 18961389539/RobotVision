using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace RobotVision.UI.Pages;

public partial class MonitorPage : Page
{
    private MainViewModel? _viewModel;

    public MonitorPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(MainViewModel));
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            SetMonitorActive(true);
            // 相机管理页增删相机后回到监控页，刷新相机下拉
            (DataContext as MainViewModel)?.RefreshCameras();
            // 页面可能被导航缓存复用：每次进入都重新订阅日志滚动
            AttachLogSubscription();
        };
        Unloaded += (_, _) =>
        {
            SetMonitorActive(false);
            DetachLogSubscription();
            // 不 Dispose Viewer：页面被导航缓存时再次进入会使用已释放控件导致崩溃；
            // ImageViewer 的非托管资源随进程退出回收（调试台可接受）
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
