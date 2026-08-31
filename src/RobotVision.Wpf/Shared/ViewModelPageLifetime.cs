using System.Windows;
using System.Windows.Controls;

namespace RobotVision.WpfHost.Shared;

/// <summary>为 Page 绑定 ViewModel，并在 Unloaded 时释放 Transient VM。</summary>
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
            onUnloading?.Invoke();
            if (disposeViewModelOnUnload && viewModel is IDisposable disposable)
                disposable.Dispose();
        }

        return viewModel;
    }
}
