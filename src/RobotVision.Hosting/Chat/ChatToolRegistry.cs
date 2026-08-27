using System.Text.Json;

namespace RobotVision.Hosting.Chat;

public sealed class ChatToolRegistry
{
    private readonly Dictionary<string, IChatTool> _tools;

    public ChatToolRegistry(IEnumerable<IChatTool> tools)
    {
        _tools = new Dictionary<string, IChatTool>(StringComparer.Ordinal);
        foreach (var tool in tools)
            _tools[tool.Name] = tool;
    }

    public IReadOnlyList<ChatToolSpec> Specs =>
        _tools.Values.Select(t => new ChatToolSpec(t.Name, t.Description, t.Parameters)).ToList();

    public async Task<ChatToolResult> InvokeAsync(string name, string arguments, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return new ChatToolResult(JsonSerializer.Serialize(new { ok = false, error = $"未知工具: {name}" }));
        try
        {
            return await tool.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ChatToolResult(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
        }
    }
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
