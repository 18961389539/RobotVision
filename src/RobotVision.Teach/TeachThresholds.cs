namespace RobotVision.Teach;

/// <summary>
/// 示教/推荐层跨文件复用的<b>决策门限</b>集中处。只收「多处出现或明显是需要调的政策阈值」；
/// 单个算法内部的加权系数（如 ScoreKinds 的 0.50/0.65、FeatureRoi 的偏置）留在原处就近可读，不迁入。
/// 值为 <c>const</c>，可安全用于 <c>is &gt;= TeachThresholds.X</c> 关系模式。
/// </summary>
public static class TeachThresholds
{
    /// <summary>灰度熵落在此区间视为"中等纹理"，建议模板匹配开边缘图定角。</summary>
    public const double EdgeMatchEntropyLo = 4.0;
    public const double EdgeMatchEntropyHi = 6.5;

    /// <summary>自转 180° 的 NCC 分差达到此值，才算"头尾可分"。</summary>
    public const double SeparabilityOrientable = 0.08;

    /// <summary>短轴外伸相对短边长达到此比例，认为存在可助卡尺定头尾的凸起。</summary>
    public const double ProtrusionShortLenRatio = 0.08;

    /// <summary>赛马取胜：高分项需比政策序默认项至少高出此分才改选高分。</summary>
    public const double WinMarginScore = 0.08;

    /// <summary>整夹角σ（度）：超过判"不稳"，低于判"稳定"。</summary>
    public const double AngleStdUnstableDeg = 8.0;
    public const double AngleStdStableDeg = 4.0;
}
