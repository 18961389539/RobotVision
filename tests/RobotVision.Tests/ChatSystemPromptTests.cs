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
        Assert.Contains("站内工艺助手", prompt);
        Assert.Contains("光模块", prompt);
        Assert.Contains("query_results", prompt);
        Assert.Contains("1018", prompt);
        Assert.Contains("禁止编造", prompt);
        Assert.Contains("web_search", prompt);
        Assert.Contains("web_fetch", prompt);
    }

    [Fact]
    public void Resolve_EmptyFallsBackToDefault_AndStampsLocalClock()
    {
        var now = new DateTimeOffset(2026, 8, 27, 21, 8, 0, TimeSpan.FromHours(8));
        var resolved = ChatSystemPrompt.Resolve(null, now);
        Assert.StartsWith(ChatSystemPrompt.Default.Trim(), resolved);
        Assert.Contains("2026-08-27 21:08", resolved);
        Assert.Contains("训练数据", resolved);

        Assert.Contains("2026-08-27", ChatSystemPrompt.Resolve("  ", now));
        Assert.StartsWith("自定义", ChatSystemPrompt.Resolve(" 自定义 ", now));
        Assert.Contains("2026-08-27", ChatSystemPrompt.Resolve(" 自定义 ", now));
    }

    [Fact]
    public void ChatConfig_UsesStationIdentity()
    {
        Assert.Equal(ChatSystemPrompt.Default, new ChatConfig().SystemPrompt);
    }
}
