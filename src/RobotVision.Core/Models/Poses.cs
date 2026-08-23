namespace RobotVision.Core.Models;

/// <summary>像素坐标系下的检测结果（图像坐标，y 轴向下）。</summary>
/// <param name="Cx">中心 x（px）。</param>
/// <param name="Cy">中心 y（px）。</param>
/// <param name="AngleDeg">角度（度）。</param>
/// <param name="Score">置信度。</param>
public sealed record PixelPose(double Cx, double Cy, double AngleDeg, double Score);

/// <summary>机器人坐标系下的输出位姿。</summary>
public sealed record RobotPose(double X, double Y, double AngleDeg);
