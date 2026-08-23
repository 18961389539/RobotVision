using RobotVision.Core.Models;

namespace RobotVision.Core.Geometry;

/// <summary>
/// 旋转中心补偿的纯几何计算（机器人坐标系内）。
/// 轴心在像素空间拟合、经外参映射到机器人空间后，在此完成物理旋转。
/// </summary>
public static class RotationCenterCompensation
{
    /// <summary>点绕轴心旋转 deltaDeg 度（逆时针为正，与机器人 XY 右手系一致）。</summary>
    public static (double X, double Y) Rotate(double x, double y, double cx, double cy, double deltaDeg)
    {
        var rad = deltaDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dx = x - cx;
        var dy = y - cy;
        return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
    }

    /// <summary>
    /// 偏心工具补偿：位置绕轴心反转零件角 θ，角度保持不变。P' = C + R(−θ)·(P − C)。
    /// 机器人先移动到 P'，再旋转第 4 轴到 AngleDeg，工具尖端恰好落回原检测位置 P。
    /// 前提：第 4 轴角度正方向与机器人 XY 系旋转方向一致（右手系逆时针为正），
    /// 不一致的机器人请在示教侧取反角度。
    /// </summary>
    public static RobotPose Apply(RobotPose pose, double cx, double cy)
    {
        var (x, y) = Rotate(pose.X, pose.Y, cx, cy, -pose.AngleDeg);
        return new RobotPose(x, y, pose.AngleDeg);
    }
}
