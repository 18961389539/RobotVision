namespace RobotVision.JlVision;

/// <summary>Phase 2 定标后的 JlShape 运行时默认值（Dev 网格锁定）。</summary>
public static class JlShapeDefaults
{
    /// <summary>FindShapeModel minScore。配方 MatchThreshold 仍作 Usable 门。</summary>
    public static double FindMinScore { get; set; } = 0.40;

    public static JlFindOptions Find { get; set; } = JlFindOptions.ProductDefault;
}
