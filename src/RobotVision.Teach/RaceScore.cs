using RobotVision.Core.Geometry;

namespace RobotVision.Teach;

/// <summary>
/// 多帧赛马打分聚合的<b>唯一数值实现</b>：分为「过门率 × 过门均分 × 角度一致性」。
///
/// 一致性 = 1 − clamp(角σ / <see cref="ConsistencyDivDeg"/>°)，角度样本 &lt; 2 时视为 1；
/// 复合分 &lt; <see cref="OkGate"/> 或无过门帧则 <c>Ok=false</c>。空帧计入分母。
/// 只算数值、<b>不产字符串</b>（文案在 <see cref="TeachNarrator"/>）。
/// 供 <see cref="SegmentRefineBakeOff.Aggregate"/> 与 <see cref="RefineParamTuner"/> 共用，避免两处各抄一份。
/// </summary>
public static class RaceScore
{
    /// <summary>取胜/过门复合分下限。</summary>
    public const double OkGate = 0.35;

    /// <summary>角σ 归一到一致性 [0,1] 的除数（度）。</summary>
    public const double ConsistencyDivDeg = 8.0;

    /// <summary>一方法多帧聚合结果。<see cref="AngleStdDeg"/> 样本 &lt; 2 时为 NaN。</summary>
    public readonly record struct Aggregate(
        int OkCount,
        int TotalFrames,
        double MeanScore,
        int AngleSampleCount,
        double AngleStdDeg,
        double Consistency,
        double Score,
        bool Ok,
        double SampleAngleDeg);

    /// <summary>
    /// 给定「已通过本方法门限的帧候选」<paramref name="okRows"/>、总帧数 <paramref name="totalFrames"/>、
    /// 是否有向 <paramref name="directed"/>，算复合分。<paramref name="okRows"/> 的挑选方式由调用方决定
    /// （按 <see cref="SegmentRefineCandidate.Ok"/> 或按 <c>Score&gt;=阈值</c>）。
    /// </summary>
    public static Aggregate Compute(
        IReadOnlyList<SegmentRefineCandidate> okRows, int totalFrames, bool directed)
    {
        var meanOk = okRows.Count == 0 ? 0.0 : okRows.Average(c => c.Score);
        var period = directed ? 360.0 : 180.0;
        var angles = okRows.Where(c => double.IsFinite(c.AngleDeg)).Select(c => c.AngleDeg).ToList();
        var std = angles.Count < 2 ? double.NaN : AngleGeometry.CircularStdDeg(angles, period);
        var consistency = angles.Count < 2 ? 1.0 : Math.Clamp(1.0 - std / ConsistencyDivDeg, 0, 1);
        var score = totalFrames == 0
            ? 0.0
            : (okRows.Count / (double)totalFrames) * meanOk * consistency;
        var ok = okRows.Count > 0 && score >= OkGate;
        var sample = angles.Count == 0 ? double.NaN : angles[0];
        return new Aggregate(okRows.Count, totalFrames, meanOk, angles.Count, std, consistency, score, ok, sample);
    }
}
