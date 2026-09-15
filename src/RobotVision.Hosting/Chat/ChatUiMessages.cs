using System.Text;
using System.Text.Json;

namespace RobotVision.Hosting.Chat;

/// <summary>assistant-ui LocalRuntime 发来的消息 → <see cref="ChatTurn"/>。</summary>
public static class ChatUiMessages
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<ChatTurn> ParseTurns(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
            return [];

        var turns = new List<ChatTurn>();
        foreach (var item in messages.EnumerateArray())
        {
            var role = ReadString(item, "role");
            if (!IsModelRole(role))
                continue;
            var text = ReadContent(item);
            if (text.Length == 0)
                continue;
            turns.Add(new ChatTurn(role, text));
        }

        return turns;
    }

    public static string ToSse(string type, string? text = null, string? name = null, string? detail = null, string? url = null, string? message = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", type);
            if (text is not null)
                writer.WriteString("text", text);
            if (name is not null)
                writer.WriteString("name", name);
            if (detail is not null)
                writer.WriteString("detail", detail);
            if (url is not null)
                writer.WriteString("url", url);
            if (message is not null)
                writer.WriteString("message", message);
            writer.WriteEndObject();
        }

        return "data: " + Encoding.UTF8.GetString(stream.ToArray()) + "\n\n";
    }

    internal static bool IsModelRole(string role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    private static string ReadString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return "";
        return el.GetString() ?? "";
    }

    private static string ReadContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content))
            return "";
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString()?.Trim() ?? "";
        if (content.ValueKind != JsonValueKind.Array)
            return "";

        var parts = new List<string>();
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                var s = part.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    parts.Add(s.Trim());
                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
                continue;
            var type = ReadString(part, "type");
            if (type.Length > 0 && !string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                continue;
            if (part.TryGetProperty("text", out var textEl))
            {
                if (textEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(textEl.GetString()))
                    parts.Add(textEl.GetString()!.Trim());
                else if (textEl.ValueKind == JsonValueKind.Object && textEl.TryGetProperty("value", out var value)
                         && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    parts.Add(value.GetString()!.Trim());
            }
        }

        return string.Join("\n", parts);
    }
}
