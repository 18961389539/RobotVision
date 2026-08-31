using System.Text.Json;
using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class ChatToolSecurityTests
{
    [Fact]
    public void TryParse_InvalidJson_ReturnsError()
    {
        var result = ChatToolArguments.TryParse("{not json");
        Assert.False(result.IsSuccess);
        Assert.Contains("JSON", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_ArrayRoot_ReturnsError()
    {
        var result = ChatToolArguments.TryParse("[1,2]");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void DangerousAction_WithoutConfirm_IsBlocked()
    {
        using var doc = JsonDocument.Parse("""{"action":"stop"}""");
        var check = ChatDangerousActionGuard.Evaluate(
            "tcp_control",
            doc.RootElement,
            "请确认停止 TCP 通信");
        Assert.True(check.IsBlocked);
        Assert.Contains("confirm", check.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DangerousAction_WithConfirmButNoUserIntent_IsBlocked()
    {
        using var doc = JsonDocument.Parse("""{"action":"stop","confirm":true}""");
        var check = ChatDangerousActionGuard.Evaluate("tcp_control", doc.RootElement, "今天合格率多少");
        Assert.True(check.IsBlocked);
    }

    [Fact]
    public void DangerousAction_WithConfirmAndUserIntent_Allows()
    {
        using var doc = JsonDocument.Parse("""{"action":"stop","confirm":true}""");
        var check = ChatDangerousActionGuard.Evaluate("tcp_control", doc.RootElement, "确认停止 TCP 通信");
        Assert.False(check.IsBlocked);
    }

    [Fact]
    public async Task Registry_InvalidArgs_DoesNotInvokeTool()
    {
        var invoked = false;
        var tool = new DelegateChatTool(
            "demo",
            "d",
            JsonSerializer.Deserialize<JsonElement>("""{"type":"object"}""")!,
            (_, _) =>
            {
                invoked = true;
                return Task.FromResult(new ChatToolResult("""{"ok":true}"""));
            });
        var registry = CreateRegistry(tool);
        var result = await registry.InvokeAsync("demo", "{bad", CancellationToken.None);
        Assert.False(invoked);
        Assert.Contains("JSON", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registry_BlockedDangerousAction_WritesAudit()
    {
        var tool = new DelegateChatTool(
            "tcp_control",
            "d",
            JsonSerializer.Deserialize<JsonElement>("""{"type":"object"}""")!,
            (_, _) => Task.FromResult(new ChatToolResult("""{"ok":true}""")));
        var cfg = new ChatConfig { AuditEnabled = true, AuditFolder = Path.Combine(Path.GetTempPath(), "rv-audit-" + Guid.NewGuid()) };
        var registry = CreateRegistry(tool, cfg);
        var result = await registry.InvokeAsync(
            "tcp_control",
            """{"action":"stop","confirm":true}""",
            new ChatToolInvocationContext("今天天气"),
            CancellationToken.None);
        Assert.Contains("blocked", result.Text, StringComparison.OrdinalIgnoreCase);
        var auditFile = Directory.GetFiles(cfg.AuditFolder, "audit-*.jsonl").Single();
        var line = File.ReadAllLines(auditFile).Single();
        Assert.Contains("blocked", line, StringComparison.OrdinalIgnoreCase);
        try { Directory.Delete(cfg.AuditFolder, true); } catch { /* test cleanup */ }
    }

    private static ChatToolRegistry CreateRegistry(IChatTool tool, ChatConfig? cfg = null)
    {
        cfg ??= new ChatConfig { RequireDangerousActionConfirm = true, AuditEnabled = false };
        var app = new AppConfig { Chat = cfg };
        return new ChatToolRegistry([tool], new ChatToolAuditStore(app), cfg);
    }
}
