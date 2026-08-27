using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using RobotVision.WpfHost;

namespace RobotVision.WpfHost.Features.Chat;

public partial class ChatPage : Page
{
    public ChatPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(ChatViewModel));
        Loaded += (_, _) =>
        {
            if (DataContext is not ChatViewModel vm)
                return;
            vm.Messages.CollectionChanged += OnMessagesChanged;
            _ = vm.ProbeAsync();
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is ChatViewModel vm)
                vm.Messages.CollectionChanged -= OnMessagesChanged;
        };
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is ChatViewModel vm && vm.Messages.Count > 0)
            MessageList.ScrollIntoView(vm.Messages[^1]);
    }
}
