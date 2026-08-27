using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RobotVision.Hosting.Chat;

/// <summary>工具循环：模型要查站内状态时先调工具，再生成最终回复。</summary>
public sealed class ChatAgent
{
    public const int MaxRounds = 8;
    private const int ResultCap = 12000;

    private readonly ILocalChatClient _client;
    private readonly ChatToolRegistry _tools;

    public ChatAgent(ILocalChatClient client, ChatToolRegistry tools)
    {
        _client = client;
        _tools = tools;
    }

    public async IAsyncEnumerable<ChatAgentEvent> RunAsync(
        IReadOnlyList<ChatTurn> turns,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = turns
            .Select(t => new ChatApiMessage(t.Role, t.Content))
            .ToList();
        var specs = _tools.Specs;

        for (var round = 0; round < MaxRounds; round++)
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
                var result = await _tools.InvokeAsync(call.Name, call.Arguments, cancellationToken).ConfigureAwait(false);
                var text = result.Text.Length <= ResultCap ? result.Text : result.Text[..ResultCap] + "…";
                yield return new ChatToolNotice(call.Name, Summarize(text));
                if (!string.IsNullOrWhiteSpace(result.ImagePath) && File.Exists(result.ImagePath))
                {
                    var w = 0;
                    var h = 0;
                    TryReadSize(text, out w, out h);
                    yield return new ChatImageEvent(result.ImagePath, w, h);
                }
                messages.Add(new ChatApiMessage("tool", text, ToolCallId: call.Id, Name: call.Name));
            }
        }
    }

    private static string Summarize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
            {
                var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : json;
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
}
