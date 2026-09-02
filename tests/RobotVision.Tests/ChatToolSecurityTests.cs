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
    public void SetCamera_ExposureChange_WithoutConfirm_IsBlocked()
    {
        // P1-7 修复前：非 unregister 的 set_camera（改曝光/增益→落盘+热下发）零护栏
        using var doc = JsonDocument.Parse("""{"camera_id":"CAM1","exposure_us":2000,"gain":20}""");
        var check = ChatDangerousActionGuard.Evaluate("set_camera", doc.RootElement, "把 1 号相机曝光改成 2000");
        Assert.True(check.IsBlocked);
        Assert.Contains("confirm", check.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetCamera_ExposureChange_WithConfirmAndUserIntent_Allows()
    {
        using var doc = JsonDocument.Parse("""{"camera_id":"CAM1","exposure_us":2000,"confirm":true}""");
        var check = ChatDangerousActionGuard.Evaluate("set_camera", doc.RootElement, "确认把 CAM1 曝光改成 2000");
        Assert.False(check.IsBlocked);
    }

    [Fact]
    public void ChattyChitChat_WithSingleCharIntentWord_DoesNotBypass()
    {
        // 回归防护：单字「改/停」曾出现在 IntentKeywords，用户闲聊「曝光改天再看」
        //（含「改」+ 点名 CAM1）即可放行 set_camera。双字词(修改/停止/确认)才构成确认意图。
        using var doc = JsonDocument.Parse("""{"camera_id":"CAM1","exposure_us":2000,"confirm":true}""");
        var check = ChatDangerousActionGuard.Evaluate("set_camera", doc.RootElement, "CAM1 曝光改天再看吧");
        Assert.True(check.IsBlocked, "闲聊含「改」字不应视为确认意图");
    }

    [Fact]
    public void ClearInhibit_VagueConfirm_WithoutNamingRecipe_IsBlocked()
    {
        // P1-7 修复前：targets 硬编码「联锁/解除/1018」，用户一句「确认解除」即通过
        using var doc = JsonDocument.Parse("""{"confirm":true}""");
        var check = ChatDangerousActionGuard.Evaluate("clear_inhibit", doc.RootElement, "确认解除");
        Assert.True(check.IsBlocked);
    }

    [Fact]
    public void ClearInhibit_NamedRecipe_WithConfirmAndIntent_Allows()
    {
        using var doc = JsonDocument.Parse("""{"recipe":"alpha","confirm":true}""");
        var check = ChatDangerousActionGuard.Evaluate("clear_inhibit", doc.RootElement, "确认解除配方 alpha");
        Assert.False(check.IsBlocked);
    }

    [Fact]
    public void ClearInhibit_All_RequiresExplicitAllKeyword()
    {
        // recipe 为空=解除全部联锁，必须说「全部/所有」+ 意图词才放行
        using var doc = JsonDocument.Parse("""{"confirm":true}""");
        var check = ChatDangerousActionGuard.Evaluate("clear_inhibit", doc.RootElement, "确认解除全部联锁");
        Assert.False(check.IsBlocked);

        var vague = ChatDangerousActionGuard.Evaluate("clear_inhibit", doc.RootElement, "确认解除 1018");
        Assert.True(vague.IsBlocked, "未点名配方时提及硬编码编号不再作为对象匹配");
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
