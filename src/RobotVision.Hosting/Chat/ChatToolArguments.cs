using System.Text.Json;

namespace RobotVision.Hosting.Chat;

public readonly struct ChatToolArgsParseResult
{
    private ChatToolArgsParseResult(bool isSuccess, JsonDocument? document, string? error)
    {
        IsSuccess = isSuccess;
        Document = document;
        Error = error;
    }

    public bool IsSuccess { get; }
    public JsonDocument? Document { get; }
    public string? Error { get; }

    public static ChatToolArgsParseResult Ok(JsonDocument document) => new(true, document, null);

    public static ChatToolArgsParseResult Fail(string error) => new(false, null, error);
}

/// <summary>工具参数 JSON 解析；非法 JSON 返回明确错误，不静默变成空对象。</summary>
public static class ChatToolArguments
{
    public static ChatToolArgsParseResult TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ChatToolArgsParseResult.Ok(JsonDocument.Parse("{}"));

        try
        {
            using var probe = JsonDocument.Parse(json);
            if (probe.RootElement.ValueKind != JsonValueKind.Object)
                return ChatToolArgsParseResult.Fail("工具参数必须是 JSON 对象。");
        }
        catch (JsonException ex)
        {
            return ChatToolArgsParseResult.Fail($"工具参数 JSON 无效：{ex.Message}");
        }

        try
        {
            return ChatToolArgsParseResult.Ok(JsonDocument.Parse(json));
        }
        catch (JsonException ex)
        {
            return ChatToolArgsParseResult.Fail($"工具参数 JSON 无效：{ex.Message}");
        }
    }

    public static bool GetBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node))
            return false;
        return node.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(node.GetString(), out var b) && b,
            JsonValueKind.Number => node.TryGetInt32(out var n) && n != 0,
            _ => false,
        };
    }

    public static string GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node))
            return "";
        return node.ValueKind switch
        {
            JsonValueKind.String => node.GetString() ?? "",
            JsonValueKind.Number => node.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "",
        };
    }

    public static bool HasProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out var node)
        && node.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    public static int PropertyCount(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object ? root.EnumerateObject().Count() : 0;
}
