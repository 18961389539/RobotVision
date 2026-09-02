using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 为 Page 绑定 ViewModel，并在页面真正离开时释放 Transient VM。
/// </summary>
/// <remarks>
/// 生命周期决策不能只看 <see cref="FrameworkElement.Unloaded"/>：WPF 中该事件会在元素
/// 临时脱离视觉树时误触发（主题切换重建资源字典、重父化等），此时页面仍在导航中，
/// 立即 Dispose 会让 VM 死亡且无法恢复（如 Monitor 页停止接收 FrameProcessed，无异常无日志）。
/// 因此本类把决策延迟到事件批次之后：若元素已重新挂载（<see cref="FrameworkElement.IsLoaded"/>），
/// 判定为误触发，保留 VM；仅对真正的导航离开执行卸载与释放。
/// </remarks>
internal static class ViewModelPageLifetime
{
    public static TViewModel Attach<TViewModel>(
        Page page,
        TViewModel viewModel,
        bool disposeViewModelOnUnload = true,
        Action? onUnloading = null)
        where TViewModel : class
    {
        page.DataContext = viewModel;
        page.Unloaded += OnUnloaded;

        void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            page.Unloaded -= OnUnloaded;

            // 延迟到当前事件批次（同批的 Loaded 也已执行）之后决策，避免误触发误杀 VM。
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (page.IsLoaded)
                {
                    // 误触发（临时脱离后已恢复）：撤销退订，继续守望后续的真实离开。
                    page.Unloaded += OnUnloaded;
                    return;
                }

                onUnloading?.Invoke();
                if (viewModel is IPageUnloadAware unloadAware)
                    unloadAware.OnPageUnloading();
                if (disposeViewModelOnUnload && viewModel is IDisposable disposable)
                    disposable.Dispose();
            });
        }

        return viewModel;
    }
}
