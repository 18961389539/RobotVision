using System.Diagnostics;
using System.Text.Json;

namespace RobotVision.Hosting.Chat;

public sealed class ChatToolRegistry
{
    private readonly Dictionary<string, IChatTool> _tools;
    private readonly ChatToolAuditStore _audit;
    private readonly ChatConfig _cfg;

    public ChatToolRegistry(IEnumerable<IChatTool> tools, ChatToolAuditStore audit, ChatConfig cfg)
    {
        _tools = new Dictionary<string, IChatTool>(StringComparer.Ordinal);
        foreach (var tool in tools)
            _tools[tool.Name] = tool;
        _audit = audit;
        _cfg = cfg;
    }

    public IReadOnlyList<ChatToolSpec> Specs =>
        _tools.Values.Select(t => new ChatToolSpec(t.Name, t.Description, t.Parameters)).ToList();

    public Task<ChatToolResult> InvokeAsync(
        string name,
        string arguments,
        CancellationToken cancellationToken) =>
        InvokeAsync(name, arguments, context: null, cancellationToken);

    public async Task<ChatToolResult> InvokeAsync(
        string name,
        string arguments,
        ChatToolInvocationContext? context,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var userSnippet = ChatToolAuditStore.TruncateUser(context?.LastUserMessage);
        ChatToolResult result;

        if (!_tools.TryGetValue(name, out var tool))
        {
            result = Fail($"未知工具: {name}");
            Record(name, arguments, userSnippet, started, "unknown_tool", result.Text);
            return result;
        }

        var parsed = ChatToolArguments.TryParse(arguments);
        if (!parsed.IsSuccess)
        {
            result = Fail(parsed.Error!);
            Record(name, arguments, userSnippet, started, "invalid_args", result.Text, parsed.Error);
            return result;
        }

        using (parsed.Document)
        {
            if (_cfg.RequireDangerousActionConfirm)
            {
                var guard = ChatDangerousActionGuard.Evaluate(name, parsed.Document!.RootElement, context?.LastUserMessage);
                if (guard.IsBlocked)
                {
                    result = Blocked(guard.Reason!);
                    Record(name, arguments, userSnippet, started, "blocked", result.Text, guard.Reason);
                    return result;
                }
            }
        }

        try
        {
            result = await tool.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
            var outcome = ChatToolAuditStore.OutcomeFromResult(result.Text);
            Record(name, arguments, userSnippet, started, outcome, result.Text);
            return result;
        }
        catch (OperationCanceledException)
        {
            Record(name, arguments, userSnippet, started, "cancelled", "", "cancelled");
            throw;
        }
        catch (Exception ex)
        {
            result = Fail(ex.Message);
            Record(name, arguments, userSnippet, started, "exception", result.Text, ex.Message);
            return result;
        }
    }

    private void Record(
        string tool,
        string arguments,
        string userSnippet,
        long startedTicks,
        string outcome,
        string resultText,
        string? error = null)
    {
        var durationMs = (long)((Stopwatch.GetTimestamp() - startedTicks) * 1000.0 / Stopwatch.Frequency);
        _audit.Record(new ChatToolAuditEntry(
            DateTimeOffset.Now,
            tool,
            ChatToolAuditStore.TruncateArguments(arguments),
            outcome,
            error ?? ExtractError(resultText),
            durationMs,
            userSnippet));
    }

    private static string? ExtractError(string resultText)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultText);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();
        }
        catch (JsonException)
        {
            // ignore
        }
        return null;
    }

    private static ChatToolResult Fail(string error) =>
        new(JsonSerializer.Serialize(new { ok = false, error }));

    private static ChatToolResult Blocked(string error) =>
        new(JsonSerializer.Serialize(new { ok = false, blocked = true, error }));
}

internal sealed class DelegateChatTool(
    string name,
    string description,
    JsonElement parameters,
    Func<string, CancellationToken, Task<ChatToolResult>> invoke) : IChatTool
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public JsonElement Parameters { get; } = parameters;
    public Task<ChatToolResult> InvokeAsync(string argumentsJson, CancellationToken cancellationToken) =>
        invoke(argumentsJson, cancellationToken);
}
