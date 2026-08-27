namespace RobotVision.Hosting.Chat;

/// <summary>需要时拉起 llama-server，再走 OpenAI 兼容 HTTP。</summary>
public sealed class ManagedLlamaChatClient : ILocalChatClient
{
    private readonly LlamaServerHost _host;
    private readonly OpenAiChatClient _inner;
    private readonly ChatConfig _cfg;

    public ManagedLlamaChatClient(LlamaServerHost host, OpenAiChatClient inner, ChatConfig cfg)
    {
        _host = host;
        _inner = inner;
        _cfg = cfg;
    }

    public string? LastError { get; private set; }

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        try
        {
            if (_cfg.AutoStart)
                await _host.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            var ready = await _inner.ProbeAsync(cancellationToken).ConfigureAwait(false);
            if (!ready)
                LastError = _host.LastError ?? $"未连接到 {_inner.Endpoint}";
            return ready;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public async IAsyncEnumerable<string> CompleteStreamAsync(
        IReadOnlyList<ChatTurn> turns,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_cfg.AutoStart)
            await _host.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var chunk in _inner.CompleteStreamAsync(turns, cancellationToken).ConfigureAwait(false))
            yield return chunk;
    }

    public async IAsyncEnumerable<ChatStreamPart> CompletePartsAsync(
        IReadOnlyList<ChatApiMessage> messages,
        IReadOnlyList<ChatToolSpec>? tools,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_cfg.AutoStart)
            await _host.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var part in _inner.CompletePartsAsync(messages, tools, cancellationToken).ConfigureAwait(false))
            yield return part;
    }
}
