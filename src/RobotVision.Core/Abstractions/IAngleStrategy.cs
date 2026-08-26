using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Core.Abstractions;

/// <summary>
/// 角度计算策略：输入去畸变后的图像，输出像素坐标系下的位姿（中心 + 角度）。
/// 外参变换（像素→机器人）由标定层统一完成，策略不做坐标变换。
/// </summary>
public interface IAngleStrategy
{
    List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default);
}
