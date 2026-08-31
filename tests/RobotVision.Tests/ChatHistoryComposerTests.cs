using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class ChatHistoryComposerTests
{
    [Fact]
    public void Compose_ShortHistory_KeepsAll()
    {
        var turns = new[]
        {
            new ChatTurn("user", "你好"),
            new ChatTurn("assistant", "你好，请说。"),
        };
        var result = ChatHistoryComposer.Compose(turns, contextSize: 8192, maxTokens: 512);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, t => t.Content.StartsWith("〔对话摘要〕", StringComparison.Ordinal));
    }

    [Fact]
    public void Compose_OverBudget_PrependsSummaryAndKeepsRecent()
    {
        var turns = Enumerable.Range(0, 40)
            .Select(i => new ChatTurn(i % 2 == 0 ? "user" : "assistant", new string('x', 400)))
            .ToList();
        var result = ChatHistoryComposer.Compose(turns, contextSize: 2048, maxTokens: 512, historyTokenBudget: 800);
        Assert.True(result.Count < turns.Count);
        Assert.StartsWith("〔对话摘要〕", result[0].Content, StringComparison.Ordinal);
        Assert.Equal("assistant", result[^1].Role);
    }

    [Fact]
    public void EstimateTokens_CountsImagesHigher()
    {
        var textOnly = new ChatTurn("user", "hi");
        var withImage = new ChatTurn("user", "hi", ["a.png"]);
        Assert.True(ChatHistoryComposer.EstimateTokens(withImage) > ChatHistoryComposer.EstimateTokens(textOnly));
    }
}
