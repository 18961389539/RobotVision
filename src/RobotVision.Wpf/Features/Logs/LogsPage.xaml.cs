using System.Collections.Specialized;
using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Logs;

public partial class LogsPage : Page
{
    private readonly LogsViewModel _vm;
    private NotifyCollectionChangedEventHandler? _rowsChanged;

    public LogsPage(LogsViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            if (_rowsChanged is not null)
                _vm.Rows.CollectionChanged -= _rowsChanged;
            _vm.StopTimer();
        });
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _rowsChanged ??= OnRowsChanged;
            // 先解绑再绑定，避免页面在常驻 VM 生命周期下反复 Loaded 时累积重复处理器。
            _vm.Rows.CollectionChanged -= _rowsChanged;
            _vm.Rows.CollectionChanged += _rowsChanged;
            _vm.StartTimer();
        };
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm.FollowTail && _vm.Rows.Count > 0)
            EntryList.ScrollIntoView(_vm.Rows[^1]);
    }
}
