using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Pages;

public partial class LogsPage : Page
{
    public LogsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(LogsViewModel));
        // 页面可能被导航缓存复用：订阅/定时器统一在 Loaded 建立、Unloaded 拆除
        Loaded += (_, _) =>
        {
            if (DataContext is LogsViewModel vm)
            {
                vm.Rows.CollectionChanged += OnRowsChanged;
                vm.StartTimer();
            }
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is LogsViewModel vm)
            {
                vm.Rows.CollectionChanged -= OnRowsChanged;
                vm.StopTimer();
            }
        };
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is LogsViewModel { FollowTail: true } vm && vm.Rows.Count > 0)
            EntryList.ScrollIntoView(vm.Rows[^1]);
    }
}
