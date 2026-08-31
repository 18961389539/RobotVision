using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RobotVision.Hosting.Chat;

/// <summary>工具循环：模型要查站内状态时先调工具，再生成最终回复。</summary>
public sealed class ChatAgent
{
    public const int DefaultMaxRounds = 8;
    private const int ResultCap = 12000;

    private readonly ILocalChatClient _client;
    private readonly ChatToolRegistry _tools;
    private readonly ChatConfig _cfg;

    public ChatAgent(ILocalChatClient client, ChatToolRegistry tools, ChatConfig cfg)
    {
        _client = client;
        _tools = tools;
        _cfg = cfg;
    }

    public async IAsyncEnumerable<ChatAgentEvent> RunAsync(
        IReadOnlyList<ChatTurn> turns,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var composed = ChatHistoryComposer.Compose(
            turns,
            _cfg.ContextSize,
            _cfg.MaxTokens,
            _cfg.HistoryTokenBudget);
        var messages = composed
            .Select(t => new ChatApiMessage(t.Role, t.Content, ImagePaths: t.ImagePaths))
            .ToList();
        var specs = _tools.Specs;
        var maxRounds = _cfg.MaxToolRounds > 0 ? _cfg.MaxToolRounds : DefaultMaxRounds;
        var lastUserMessage = FindLastUserMessage(turns);

        for (var round = 0; round < maxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<ChatToolCall>? calls = null;
            await foreach (var part in _client.CompletePartsAsync(messages, specs, cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(part.Content))
                    yield return new ChatTextDelta(part.Content);
                if (part.ToolCalls is { Count: > 0 })
                    calls = part.ToolCalls.ToList();
            }

            if (calls is not { Count: > 0 })
                yield break;

            messages.Add(new ChatApiMessage("assistant", Content: null, ToolCalls: calls));
            foreach (var call in calls)
            {
                yield return new ChatToolNotice(call.Name, "执行中…");
                var result = await _tools.InvokeAsync(
                    call.Name,
                    call.Arguments,
                    new ChatToolInvocationContext(lastUserMessage),
                    cancellationToken).ConfigureAwait(false);
                var text = result.Text.Length <= ResultCap ? result.Text : result.Text[..ResultCap] + "…";
                string? imagePath = null;
                if (!string.IsNullOrWhiteSpace(result.ImagePath) && File.Exists(result.ImagePath))
                {
                    imagePath = result.ImagePath;
                    text = ChatImageContext.EnrichToolText(text, imagePath);
                    var w = 0;
                    var h = 0;
                    TryReadSize(result.Text, out w, out h);
                    yield return new ChatImageEvent(imagePath, w, h);
                }

                yield return new ChatToolNotice(call.Name, Summarize(text));
                messages.Add(new ChatApiMessage(
                    "tool",
                    text,
                    ToolCallId: call.Id,
                    Name: call.Name,
                    ImagePaths: imagePath is null ? null : [imagePath]));
            }
        }

        yield return new ChatTextDelta(
            $"\n\n（已达工具调用上限 {maxRounds} 轮，请缩小问题范围或分步提问。）");
    }

    private static string Summarize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
            {
                var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : json;
                if (doc.RootElement.TryGetProperty("blocked", out var blocked)
                    && blocked.ValueKind is JsonValueKind.True)
                    return "已拦截: " + err;
                return "失败: " + err;
            }
            if (doc.RootElement.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String)
                return "已保存 " + path.GetString();
            if (doc.RootElement.TryGetProperty("count", out var count))
                return "count=" + count.ToString();
        }
        catch (JsonException)
        {
            // 非 JSON 原样截断
        }
        return json.Length <= 160 ? json : json[..160] + "…";
    }

    private static void TryReadSize(string json, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("width", out var w) && w.TryGetInt32(out var wi))
                width = wi;
            if (doc.RootElement.TryGetProperty("height", out var h) && h.TryGetInt32(out var hi))
                height = hi;
        }
        catch (JsonException)
        {
            // ignore
        }
    }

    private static string? FindLastUserMessage(IReadOnlyList<ChatTurn> turns)
    {
        for (var i = turns.Count - 1; i >= 0; i--)
        {
            if (string.Equals(turns[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                return turns[i].Content;
        }
        return null;
    }
}
