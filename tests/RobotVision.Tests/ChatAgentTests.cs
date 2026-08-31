using System.Runtime.CompilerServices;
using System.Text.Json;
using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class ChatAgentTests
{
    [Fact]
    public async Task Run_ToolThenAnswer_YieldsNoticeAndText()
    {
        var tool = new DelegateChatTool(
            "station_overview",
            "overview",
            JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{}}""")!,
            (_, _) => Task.FromResult(new ChatToolResult("""{"ok":true,"cameraCount":2}""")));
        var registry = CreateRegistry(tool);
        var client = new ScriptClient();
        var agent = new ChatAgent(client, registry, new ChatConfig());

        var events = new List<ChatAgentEvent>();
        await foreach (var ev in agent.RunAsync([new ChatTurn("user", "有几台相机")]))
            events.Add(ev);

        Assert.Contains(events, e => e is ChatToolNotice n && n.Name == "station_overview");
        Assert.Contains(events, e => e is ChatTextDelta t && t.Text.Contains("2", StringComparison.Ordinal));
        Assert.Equal(2, client.Rounds);
    }

    [Fact]
    public async Task Run_ExhaustsToolRounds_EmitsLimitMessage()
    {
        var tool = new DelegateChatTool(
            "station_overview",
            "overview",
            JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{}}""")!,
            (_, _) => Task.FromResult(new ChatToolResult("""{"ok":true}""")));
        var registry = CreateRegistry(tool);
        var client = new AlwaysToolClient();
        var agent = new ChatAgent(client, registry, new ChatConfig { MaxToolRounds = 2 });

        var events = new List<ChatAgentEvent>();
        await foreach (var ev in agent.RunAsync([new ChatTurn("user", "查站况")]))
            events.Add(ev);

        var text = string.Concat(events.OfType<ChatTextDelta>().Select(d => d.Text));
        Assert.Contains("工具调用上限", text, StringComparison.Ordinal);
    }

    private sealed class AlwaysToolClient : ILocalChatClient
    {
        public string? LastError => null;
        public Task<bool> ProbeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public async IAsyncEnumerable<string> CompleteStreamAsync(
            IReadOnlyList<ChatTurn> turns,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<ChatStreamPart> CompletePartsAsync(
            IReadOnlyList<ChatApiMessage> messages,
            IReadOnlyList<ChatToolSpec>? tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = messages;
            _ = tools;
            await Task.Yield();
            yield return new ChatStreamPart(null, [new ChatToolCall("call_1", "station_overview", "{}")], false);
        }
    }

    private static ChatToolRegistry CreateRegistry(IChatTool tool)
    {
        var cfg = new ChatConfig { RequireDangerousActionConfirm = false, AuditEnabled = false };
        return new ChatToolRegistry([tool], new ChatToolAuditStore(new AppConfig { Chat = cfg }), cfg);
    }

    private sealed class ScriptClient : ILocalChatClient
    {
        public int Rounds { get; private set; }
        public string? LastError => null;

        public Task<bool> ProbeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public async IAsyncEnumerable<string> CompleteStreamAsync(
            IReadOnlyList<ChatTurn> turns,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var part in CompletePartsAsync([], null, cancellationToken))
            {
                if (!string.IsNullOrEmpty(part.Content))
                    yield return part.Content;
            }
        }

        public async IAsyncEnumerable<ChatStreamPart> CompletePartsAsync(
            IReadOnlyList<ChatApiMessage> messages,
            IReadOnlyList<ChatToolSpec>? tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = messages;
            _ = tools;
            Rounds++;
            await Task.Yield();
            if (Rounds == 1)
            {
                yield return new ChatStreamPart(null, [new ChatToolCall("call_1", "station_overview", "{}")], false);
                yield break;
            }

            yield return new ChatStreamPart("共有 2 台相机", null, false);
        }
    }
}
