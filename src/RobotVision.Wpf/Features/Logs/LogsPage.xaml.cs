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
