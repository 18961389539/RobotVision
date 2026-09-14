using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Settings;

public partial class SettingsPage : Page
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel, IDialogService dialogs)
    {
        _viewModel = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            viewModel.StopTimer();
            if (viewModel.HasUnsavedChanges)
            {
                dialogs.ConfirmDiscard(
                    "服务设置页有未保存的修改。选择「否」可返回继续编辑（若导航已切换，请手动回到本页）。",
                    "未保存修改");
            }
        });
        InitializeComponent();

        // 失焦即提交 NumberBox 的待写回编辑，让底栏「N 项未保存」不会漏报
        NumberBoxCommit.Bind(this, viewModel, flushOnFocusLoss: true);

        // Ctrl+S 保存：不依赖 InputBindings 的 DataContext 继承，直接用页级 PreviewKeyDown
        PreviewKeyDown += OnPreviewKeyDown;

        Loaded += (_, _) =>
        {
            viewModel.LoadFromRuntime();
            viewModel.StartTimer();
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;

        _viewModel.SaveCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>
    /// 锚点选中 → 滚动到对应分组。校验失败时 ViewModel 也会改选中项，滚动逻辑因此只有这一处。
    /// 延到 Loaded 优先级：错误定位会同时展开折叠的分组，立刻滚动会按旧的布局高度算偏。
    /// </summary>
    private void OnAnchorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not SettingsAnchor anchor)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (FindName($"anchor_{anchor.Key}") is FrameworkElement target)
                target.BringIntoView();
        });
    }
}
