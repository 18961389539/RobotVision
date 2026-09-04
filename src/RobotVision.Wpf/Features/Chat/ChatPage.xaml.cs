using System.Collections.Specialized;
using System.Windows.Controls;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Chat;

public partial class ChatPage : Page
{
    private readonly ChatViewModel _vm;
    private NotifyCollectionChangedEventHandler? _messagesChanged;

    public ChatPage(ChatViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            if (_messagesChanged is not null)
                _vm.Messages.CollectionChanged -= _messagesChanged;
        });
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _messagesChanged ??= OnMessagesChanged;
            // 先解绑再绑定，避免页面在常驻 VM 生命周期下反复 Loaded 时累积重复处理器。
            _vm.Messages.CollectionChanged -= _messagesChanged;
            _vm.Messages.CollectionChanged += _messagesChanged;
            _vm.ScheduleProbe();
        };
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm.Messages.Count > 0)
            MessageList.ScrollIntoView(_vm.Messages[^1]);
    }
}
