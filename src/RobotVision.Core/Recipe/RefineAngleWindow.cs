namespace RobotVision.Core.Recipe;

/// <summary>相对分割粗角的精修搜索窗 [LoDeg, HiDeg]（度）。旧配方只有对称半宽时由 <see cref="Symmetric"/> 展开。</summary>
public readonly record struct RefineAngleWindow(double LoDeg, double HiDeg)
{
    public static RefineAngleWindow Symmetric(double rangeDeg)
    {
        var r = Math.Clamp(rangeDeg, 0.5, 45);
        return new(-r, r);
    }

    public double SpanDeg => HiDeg - LoDeg;

    public double MaxAbs => Math.Max(Math.Abs(LoDeg), Math.Abs(HiDeg));
}
