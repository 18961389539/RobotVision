using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class ChatSystemPromptTests
{
    [Fact]
    public void Default_IdentifiesStationAndForbidsFabrication()
    {
        var prompt = ChatSystemPrompt.Default;
        Assert.Contains("站内工艺助手", prompt, StringComparison.Ordinal);
        Assert.Contains("光模块", prompt, StringComparison.Ordinal);
        Assert.Contains("query_results", prompt, StringComparison.Ordinal);
        Assert.Contains("1018", prompt, StringComparison.Ordinal);
        Assert.Contains("禁止编造", prompt, StringComparison.Ordinal);
        Assert.Contains("confirm:true", prompt, StringComparison.Ordinal);
        Assert.Contains("run_recipe", prompt, StringComparison.Ordinal);
        Assert.Contains("web_search", prompt, StringComparison.Ordinal);
        Assert.Contains("web_fetch", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_EmptyFallsBackToDefault_AndStampsLocalClock()
    {
        var now = new DateTimeOffset(2026, 8, 27, 21, 8, 0, TimeSpan.FromHours(8));
        var resolved = ChatSystemPrompt.Resolve(null, now);
        Assert.StartsWith(ChatSystemPrompt.Default.Trim(), resolved, StringComparison.Ordinal);
        Assert.Contains("2026-08-27 21:08", resolved, StringComparison.Ordinal);
        Assert.Contains("训练数据", resolved, StringComparison.Ordinal);

        Assert.Contains("2026-08-27", ChatSystemPrompt.Resolve("  ", now), StringComparison.Ordinal);
        Assert.StartsWith("自定义", ChatSystemPrompt.Resolve(" 自定义 ", now), StringComparison.Ordinal);
        Assert.Contains("2026-08-27", ChatSystemPrompt.Resolve(" 自定义 ", now), StringComparison.Ordinal);
    }

    [Fact]
    public void ChatConfig_UsesStationIdentity()
    {
        Assert.Equal(ChatSystemPrompt.Default, new ChatConfig().SystemPrompt);
    }
}
