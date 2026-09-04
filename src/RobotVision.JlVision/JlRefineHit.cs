namespace RobotVision.JlVision;

/// <summary>JLVision 精修一次尝试的结果（坐标在传入 ROI 图坐标系）。</summary>
public readonly record struct JlRefineHit(
    bool Found,
    double Cx,
    double Cy,
    double AngleDeg,
    double Score,
    string Note)
{
    public static JlRefineHit Miss(string note) =>
        new(false, double.NaN, double.NaN, double.NaN, 0, note);
}
