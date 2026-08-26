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

    public RobotPose Apply(RobotPose pose) =>
        IsZero ? pose : new(pose.X + X, pose.Y + Y, AngleGeometry.NormalizeSignedDeg(pose.AngleDeg + RzDeg));

    public OutputOffsetOptions Clone() => new() { X = X, Y = Y, RzDeg = RzDeg };
}
