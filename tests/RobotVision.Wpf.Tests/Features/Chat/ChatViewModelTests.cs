using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using RobotVision.WpfHost.Features.Chat;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

public sealed class ChatViewModelTests
{
    [Fact]
    public async Task Probe_WhenServerDown_KeepsPageUsable()
    {
        var client = new StubChatClient { ProbeResult = false };
        var vm = new ChatViewModel(client, new ChatConfig(), new NullHtmlPreviewService(), TestLog.Null<ChatViewModel>());
        await vm.ProbeAsync();
        Assert.False(vm.IsReady);
        Assert.Contains("llama-server", vm.Status, StringComparison.Ordinal);
        Assert.Empty(vm.Messages);
    }

    [Fact]
    public async Task Probe_WhenReady_ShowsProfessionalStatus()
    {
        var client = new StubChatClient { ProbeResult = true };
        var vm = new ChatViewModel(client, new ChatConfig(), new NullHtmlPreviewService(), TestLog.Null<ChatViewModel>());
        await vm.ProbeAsync();
        Assert.True(vm.IsReady);
        Assert.Equal(ChatViewModel.ReadyStatus, vm.Status);
    }

    [Fact]
    public async Task Send_AppendsUserAndStreamedAssistant()
    {
        var client = new StubChatClient { Chunks = ["Hello", " world"] };
        var vm = new ChatViewModel(client, new ChatConfig(), new NullHtmlPreviewService(), TestLog.Null<ChatViewModel>());
        vm.Draft = "hi";
        await vm.SendCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal("user", vm.Messages[0].Role);
        Assert.Equal("hi", vm.Messages[0].Text);
        Assert.Equal("assistant", vm.Messages[1].Role);
        Assert.Equal("Hello world", vm.Messages[1].Text);
        Assert.False(vm.IsBusy);
    }

    private sealed class StubChatClient : ILocalChatClient
    {
        public bool ProbeResult { get; set; }
        public string? LastError { get; set; }
        public IReadOnlyList<string> Chunks { get; set; } = [];

        public Task<bool> ProbeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ProbeResult);

        public async IAsyncEnumerable<string> CompleteStreamAsync(
            IReadOnlyList<ChatTurn> turns,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = turns;
            foreach (var chunk in Chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<ChatStreamPart> CompletePartsAsync(
            IReadOnlyList<ChatApiMessage> messages,
            IReadOnlyList<ChatToolSpec>? tools,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = messages;
            _ = tools;
            foreach (var chunk in Chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatStreamPart(chunk, null, false);
                await Task.Yield();
            }
        }
    }
}
