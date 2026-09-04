namespace RobotVision.Tests;

/// <summary>
/// 形状匹配（分割后 Chamfer 精修）合成对标门槛。
/// 对标 HALCON <c>find_shape_model</c> 局部精修精度量级，非整图搜索。
/// </summary>
internal static class ShapeMatchBenchGates
{
    /// <summary>产品规格：合成矩阵 P90 角误差上限（°）。</summary>
    public const double SpecAngleDeg = 0.3;

    /// <summary>矩阵 P90 角误差上限（°），与 <see cref="SpecAngleDeg"/> 同口径（含合成浮点裕量）。</summary>
    public const double AngleP90Deg = 0.3;

    /// <summary>单场景角误差上限（°）。</summary>
    public const double AngleMaxDeg = 0.5;

    /// <summary>合成矩阵中心误差 P90 上限（px）。</summary>
    public const double SpecCenterPx = 0.1;

    /// <summary>当前合成基线中心 P90（px）。</summary>
    public const double CenterP90BaselinePx = 0.05;

    /// <summary>单次精修耗时 P90 上限（ms）。</summary>
    public const double SpecLatencyMs = 180.0;

    /// <summary>合成矩阵识别成功率下限（8 角场景）。</summary>
    public const double SpecSuccessRate = 0.992;

    /// <summary>当前合成基线成功率（8 角矩阵）。</summary>
    public const double SuccessRateBaseline = 1.0;

    /// <summary>大负角（-37°/-20°）角误差上限（°）。</summary>
    public const double LargeNegativeAngleMaxDeg = 0.25;

    /// <summary>命中门：与 <see cref="MaskShapeMatch"/> MinHitRate 同口径。</summary>
    public const double MinHitRate = 0.18;

    /// <summary>分割轮廓并入距离场后，旋转场景 Chamfer 贴边命中下限。</summary>
    public const double OverlayMinHitRate = 0.55;

    /// <summary>均距门（px）；大负角场景放宽见 bench 分支。</summary>
    public const double MaxMeanDistPx = 10.0;

    /// <summary>分割轮廓并入距离场后，旋转场景 Chamfer 均距上限（px）。</summary>
    public const double OverlayMaxMeanDistPx = 2.5;

    /// <summary>大角度 warp（|deg|≥20）最低质量分（NCC+Chamfer 混合分）。</summary>
    public const double LargeWarpMinScore = 0.20;
}
