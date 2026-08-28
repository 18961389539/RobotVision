namespace RobotVision.Core.Recipe;

/// <summary>示教时建议检测门（只写配方页，不进 TRIGGER）。默认值不收紧；仅当实例刚过门时下调。</summary>
public static class DetectionThresholdAdvisor
{
    public const double DefaultBoxConfidence = 0.5;
    public const double DefaultPixelConfidence = 0.65;

    /// <summary>
    /// 实例置信度只比当前门高一点点 → 生产抖动会漏检，把门降到约 0.85×实例分。
    /// 实例远高于当前门 → 保持手调（不把 0.5 抬到 0.75）。
    /// </summary>
    public static double SuggestBoxConfidence(double instanceConf, double current)
    {
        if (!double.IsFinite(instanceConf) || instanceConf < 0.15 || !double.IsFinite(current))
            return current;
        if (instanceConf < current + 0.12)
            return Math.Clamp(Math.Min(current, instanceConf * 0.85), 0.20, 0.85);
        return current;
    }

    /// <summary>仍是出厂 0.65 时略放宽掩码门，减少示教时孔/边被像素阈值吃掉；手调过则不动。</summary>
    public static double SuggestPixelConfidence(double current)
    {
        if (!double.IsFinite(current))
            return DefaultPixelConfidence;
        return Math.Abs(current - DefaultPixelConfidence) < 0.051 ? 0.55 : current;
    }
}
