using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace RobotVision.WpfHost.Features.Recipe;

public partial class RefineMethodDetailsWindow : FluentWindow
{
    private bool _accepted;

    public RefineMethodDetailsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RefineMethodDetailsViewModel oldVm)
            oldVm.RequestClose -= OnRequestClose;
        if (e.NewValue is RefineMethodDetailsViewModel newVm)
            newVm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose()
    {
        if (DataContext is RefineMethodDetailsViewModel vm)
            _accepted = vm.AcceptedByUser;
        try
        {
            DialogResult = _accepted;
        }
        catch (InvalidOperationException)
        {
            // 非模态打开时忽略
        }

        Close();
    }

    private void Polarity_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        (DataContext as RefineMethodDetailsViewModel)?.NotifyPolarityHintChanged();

    /// <summary>精修范围/永不翻转变更 → 刷新角度窗提示（延迟到绑定提交后）。</summary>
    private void OnRangeOrNoFlipChanged(object sender, EventArgs e)
    {
        if (DataContext is not RefineMethodDetailsViewModel vm)
            return;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => vm.NotifyMethodUiChanged());
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is not RefineMethodDetailsViewModel vm)
            return;
        vm.RequestClose -= OnRequestClose;
        vm.DetachHostForClose();
        if (!_accepted)
            vm.RestoreSnapshot();
    }
}
