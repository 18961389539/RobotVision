using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class ChatSseParserTests
{
    [Fact]
    public void TryConsume_DeltaContent_ReturnsText()
    {
        var ok = ChatSseParser.TryConsume(
            """data: {"choices":[{"delta":{"content":"你好"}}]}""",
            out var content, out var finished);
        Assert.True(ok);
        Assert.False(finished);
        Assert.Equal("你好", content);
    }

    [Fact]
    public void TryConsume_Done_SetsFinished()
    {
        var ok = ChatSseParser.TryConsume("data: [DONE]", out var content, out var finished);
        Assert.True(ok);
        Assert.True(finished);
        Assert.Null(content);
    }

    [Fact]
    public void TryConsume_NullContentDelta_ReturnsFalse()
    {
        Assert.False(ChatSseParser.TryConsume(
            """data: {"choices":[{"delta":{"role":"assistant","content":null}}]}""",
            out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("event: message")]
    [InlineData("data: {\"choices\":[]}")]
    public void TryConsume_Unrelated_ReturnsFalse(string line)
    {
        Assert.False(ChatSseParser.TryConsume(line, out _, out _));
    }

    [Fact]
    public void TryParse_ToolCallDelta_ReadsNameAndArgs()
    {
        var parsed = ChatSseParser.TryParse(
            """data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"station_overview","arguments":"{}"}}]}}]}""");
        Assert.True(parsed.Ok);
        Assert.NotNull(parsed.ToolDeltas);
        Assert.Equal("call_1", parsed.ToolDeltas![0].Id);
        Assert.Equal("station_overview", parsed.ToolDeltas[0].Name);
        Assert.Equal("{}", parsed.ToolDeltas[0].Arguments);
    }
}
