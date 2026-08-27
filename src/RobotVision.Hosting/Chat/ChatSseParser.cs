using System.Text.Json;

namespace RobotVision.Hosting.Chat;

public readonly record struct SseToolCallDelta(int Index, string? Id, string? Name, string? Arguments);

public readonly record struct SseParseResult(
    bool Ok,
    bool Finished,
    string? Content,
    string? FinishReason,
    IReadOnlyList<SseToolCallDelta>? ToolDeltas);

/// <summary>解析 llama-server / OpenAI 的 SSE 行（data: {...}）。</summary>
public static class ChatSseParser
{
    public static bool TryConsume(string line, out string? content, out bool finished)
    {
        var parsed = TryParse(line);
        content = parsed.Content;
        finished = parsed.Finished;
        if (!parsed.Ok)
            return false;
        if (parsed.Finished)
            return true;
        return parsed.Content is not null;
    }

    public static SseParseResult TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return default;

        var payload = line[5..].Trim();
        if (payload.Length == 0)
            return default;
        if (payload == "[DONE]")
            return new SseParseResult(true, true, null, "stop", null);

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return default;
            var first = choices[0];
            string? finish = null;
            if (first.TryGetProperty("finish_reason", out var fr) && fr.ValueKind == JsonValueKind.String)
                finish = fr.GetString();

            if (!first.TryGetProperty("delta", out var delta))
                return new SseParseResult(true, false, null, finish, null);

            string? content = null;
            if (delta.TryGetProperty("content", out var node) && node.ValueKind == JsonValueKind.String)
                content = node.GetString();

            List<SseToolCallDelta>? tools = null;
            if (delta.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
            {
                tools = [];
                foreach (var item in tcs.EnumerateArray())
                {
                    var index = item.TryGetProperty("index", out var idx) && idx.TryGetInt32(out var n) ? n : 0;
                    string? id = item.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String
                        ? idNode.GetString() : null;
                    string? name = null;
                    string? args = null;
                    if (item.TryGetProperty("function", out var fn))
                    {
                        if (fn.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String)
                            name = nameNode.GetString();
                        if (fn.TryGetProperty("arguments", out var argNode) && argNode.ValueKind == JsonValueKind.String)
                            args = argNode.GetString();
                    }
                    tools.Add(new SseToolCallDelta(index, id, name, args));
                }
            }

            var ok = content is not null || tools is { Count: > 0 } || finish is not null;
            return ok
                ? new SseParseResult(true, false, content, finish, tools)
                : default;
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
