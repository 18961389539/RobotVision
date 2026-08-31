namespace RobotVision.Hosting.Chat;

public interface ILocalChatClient
{
    string? LastError { get; }

    Task<bool> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>纯文本流式补全（无工具）。</summary>
    IAsyncEnumerable<string> CompleteStreamAsync(
        IReadOnlyList<ChatTurn> turns,
        CancellationToken cancellationToken = default);

    /// <summary>带 tools 的流式补全；tool_calls 在本轮结束时一次给出。</summary>
    IAsyncEnumerable<ChatStreamPart> CompletePartsAsync(
        IReadOnlyList<ChatApiMessage> messages,
        IReadOnlyList<ChatToolSpec>? tools,
        CancellationToken cancellationToken = default);
}

public readonly record struct ChatTurn(string Role, string Content, IReadOnlyList<string>? ImagePaths = null);
