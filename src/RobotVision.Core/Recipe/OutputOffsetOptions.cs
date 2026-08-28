using RobotVision.Core.Geometry;
using RobotVision.Core.Models;

namespace RobotVision.Core.Recipe;

/// <summary>
/// 配方级输出补偿（首件微调）：在像素→机器人变换与偏心工具补偿之后，对每个目标加上 ΔX/ΔY/ΔRz。
/// 用于标定残差、吸嘴安装差、料厚偏差等，避免为 0.1mm 级误差重做九点标定。
/// 零值等价于未补偿（旧配方缺省行为）。
/// </summary>
public sealed class OutputOffsetOptions
{
    /// <summary>机器人 X 方向补偿（mm）。</summary>
    public double X { get; set; }

    /// <summary>机器人 Y 方向补偿（mm）。</summary>
    public double Y { get; set; }

    /// <summary>第 4 轴角度补偿（°），加完后归一化到 (-180, 180]。</summary>
    public double RzDeg { get; set; }

    public bool IsZero => X == 0 && Y == 0 && RzDeg == 0;

    /// <summary>示教时记下的机器人输出（已含当时补偿）；null = 未记。建议补偿 = 合格中位 − 该值，再叠到当前 Δ。</summary>
    public double? TeachX { get; set; }

    public double? TeachY { get; set; }

    public double? TeachRzDeg { get; set; }

    public bool HasTeachOutput => TeachX is not null && TeachY is not null && TeachRzDeg is not null;

    public RobotPose Apply(RobotPose pose) =>
        IsZero ? pose : new(pose.X + X, pose.Y + Y, AngleGeometry.NormalizeSignedDeg(pose.AngleDeg + RzDeg));

    /// <summary>合格输出相对示教的中位差（作为要叠加的 Δ）。样本不足返回 null。</summary>
    public static OutputOffsetOptions? SuggestDelta(RobotPose teach, IReadOnlyList<RobotPose> ok, int minSamples = 8)
    {
        if (ok.Count < minSamples)
            return null;
        var xs = ok.Select(p => p.X).OrderBy(v => v).ToArray();
        var ys = ok.Select(p => p.Y).OrderBy(v => v).ToArray();
        var dAng = ok.Select(p => AngleGeometry.NormalizeSignedDeg(p.AngleDeg - teach.AngleDeg))
            .OrderBy(v => v).ToArray();
        return new()
        {
            X = Median(xs) - teach.X,
            Y = Median(ys) - teach.Y,
            RzDeg = Median(dAng),
        };
    }

    /// <summary>把建议 Δ 叠到当前补偿，并把示教改成该批合格中位（同一批再点一次 Δ≈0）。</summary>
    public void ApplySuggestedDelta(OutputOffsetOptions delta, RobotPose teach, IReadOnlyList<RobotPose> ok)
    {
        X += delta.X;
        Y += delta.Y;
        RzDeg = AngleGeometry.NormalizeSignedDeg(RzDeg + delta.RzDeg);
        var xs = ok.Select(p => p.X).OrderBy(v => v).ToArray();
        var ys = ok.Select(p => p.Y).OrderBy(v => v).ToArray();
        TeachX = Median(xs);
        TeachY = Median(ys);
        TeachRzDeg = AngleGeometry.NormalizeSignedDeg(teach.AngleDeg + delta.RzDeg);
    }

    private static double Median(double[] ordered)
    {
        var n = ordered.Length;
        return (n & 1) == 1
            ? ordered[n / 2]
            : 0.5 * (ordered[n / 2 - 1] + ordered[n / 2]);
    }

    public OutputOffsetOptions Clone() => new()
    {
        X = X,
        Y = Y,
        RzDeg = RzDeg,
        TeachX = TeachX,
        TeachY = TeachY,
        TeachRzDeg = TeachRzDeg,
    };
}
