using System.Text.Json;

namespace RobotVision.Hosting.Chat;

/// <summary>发给 llama-server 的消息（含 tool 角色）。</summary>
public sealed record ChatApiMessage(
    string Role,
    string? Content,
    string? ToolCallId = null,
    string? Name = null,
    IReadOnlyList<ChatToolCall>? ToolCalls = null);

public sealed record ChatToolCall(string Id, string Name, string Arguments);

public sealed record ChatToolSpec(string Name, string Description, JsonElement Parameters);

/// <summary>流式一块：文本增量，或本轮结束时的完整 tool_calls。</summary>
public readonly record struct ChatStreamPart(
    string? Content,
    IReadOnlyList<ChatToolCall>? ToolCalls,
    bool Finished);

public sealed record ChatToolResult(string Text, string? ImagePath = null);

public interface IChatTool
{
    string Name { get; }
    string Description { get; }
    JsonElement Parameters { get; }
    Task<ChatToolResult> InvokeAsync(string argumentsJson, CancellationToken cancellationToken);
}

public interface IChatLogSource
{
    IReadOnlyList<ChatLogLine> Recent(int max);
}

public readonly record struct ChatLogLine(string Time, string Level, string Category, string Message);

public abstract record ChatAgentEvent;

public sealed record ChatTextDelta(string Text) : ChatAgentEvent;

public sealed record ChatToolNotice(string Name, string Detail) : ChatAgentEvent;

public sealed record ChatImageEvent(string Path, int Width, int Height) : ChatAgentEvent;
