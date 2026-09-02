using RobotVision.Infrastructure.Communication;

namespace RobotVision.Hosting;

/// <summary>连续失败联锁（1018）横幅文案。</summary>
public static class InterlockBannerText
{
    public static string Format(VisionService vision, bool includeTcpHint = false)
    {
        if (!vision.AnyInhibited)
            return "";

        var limit = Math.Max(1, vision.ConsecutiveFailLimit);
        var locked = vision.GetRecipeStats()
            .Where(s => s.ConsecutiveFails >= limit)
            .Select(s => $"{s.Recipe}×{s.ConsecutiveFails}")
            .ToList();
        if (locked.Count == 0)
            return "连续失败联锁已触发（1018）。排除现场问题后点「解除联锁」。";

        var text = $"连续失败联锁：{string.Join("、", locked)}。TRIGGER 返回 1018";
        return includeTcpHint
            ? text + "，排除后点「解除联锁」。"
            : text + "。";
    }
}
