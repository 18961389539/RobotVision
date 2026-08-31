using System.Text;

namespace RobotVision.Hosting.Chat;

/// <summary>按 token 预算裁剪对话历史，较早轮次折叠为摘要。</summary>
public static class ChatHistoryComposer
{
    private const int SystemPromptReserveTokens = 1200;
    private const int ImageTokenEstimate = 512;

    public static IReadOnlyList<ChatTurn> Compose(
        IReadOnlyList<ChatTurn> turns,
        int contextSize,
        int maxTokens,
        int historyTokenBudget = 0)
    {
        if (turns.Count == 0)
            return turns;

        var budget = historyTokenBudget > 0
            ? historyTokenBudget
            : Math.Max(512, (int)((contextSize - maxTokens - SystemPromptReserveTokens) * 0.55));

        var kept = new List<ChatTurn>();
        var dropped = new List<ChatTurn>();
        var used = 0;

        for (var i = turns.Count - 1; i >= 0; i--)
        {
            var cost = EstimateTokens(turns[i]);
            if (kept.Count > 0 && used + cost > budget)
            {
                dropped.Insert(0, turns[i]);
                continue;
            }

            used += cost;
            kept.Insert(0, turns[i]);
        }

        if (dropped.Count == 0)
            return kept;

        var summary = BuildSummary(dropped);
        var result = new List<ChatTurn>(kept.Count + 1) { new("user", summary) };
        result.AddRange(kept);
        return result;
    }

    internal static int EstimateTokens(ChatTurn turn)
    {
        var chars = turn.Content?.Length ?? 0;
        if (turn.ImagePaths is { Count: > 0 })
            chars += turn.ImagePaths.Count * ImageTokenEstimate;
        return Math.Max(1, (chars + 2) / 3);
    }

    private static string BuildSummary(List<ChatTurn> dropped)
    {
        var sb = new StringBuilder();
        sb.Append("〔对话摘要〕已省略较早 ").Append(dropped.Count).Append(" 条消息。");
        var lastUser = dropped.LastOrDefault(t => string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase));
        var lastAssistant = dropped.LastOrDefault(t => string.Equals(t.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        if (lastUser.Content is { Length: > 0 } userText)
            sb.Append(" 末条用户：").Append(Truncate(userText, 120));
        if (lastAssistant.Content is { Length: > 0 } assistantText)
            sb.Append(" 末条助手：").Append(Truncate(assistantText, 120));
        return sb.ToString();
    }

    private static string Truncate(string value, int max)
    {
        var t = value.Trim().ReplaceLineEndings(" ");
        return t.Length <= max ? t : t[..max] + "…";
    }
}
