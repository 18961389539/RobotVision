using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class ChatUiMessagesTests
{
    [Fact]
    public void ParseTurns_ReadsStringContent()
    {
        var turns = ChatUiMessages.ParseTurns("""{"messages":[{"role":"user","content":"今日合格率"}]}""");
        Assert.Single(turns);
        Assert.Equal("user", turns[0].Role);
        Assert.Equal("今日合格率", turns[0].Content);
    }

    [Fact]
    public void ParseTurns_ReadsAssistantUiTextParts()
    {
        var turns = ChatUiMessages.ParseTurns(
            """{"messages":[{"role":"user","content":[{"type":"text","text":"相机能否取图"}]}]}""");
        Assert.Equal("相机能否取图", turns[0].Content);
    }

    [Fact]
    public void ParseTurns_SkipsSystemAndEmpty()
    {
        var turns = ChatUiMessages.ParseTurns(
            """{"messages":[{"role":"system","content":"x"},{"role":"assistant","content":""},{"role":"user","content":"hi"}]}""");
        Assert.Single(turns);
        Assert.Equal("hi", turns[0].Content);
    }

    [Fact]
    public void ToSse_WritesDataLine()
    {
        var sse = ChatUiMessages.ToSse("text", text: "abc");
        Assert.StartsWith("data: {", sse, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"text\"", sse, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"abc\"", sse, StringComparison.Ordinal);
        Assert.EndsWith("\n\n", sse, StringComparison.Ordinal);
    }
}
