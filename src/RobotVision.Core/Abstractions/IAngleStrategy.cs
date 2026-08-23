using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Core.Abstractions;

/// <summary>
/// 角度计算策略：输入去畸变后的图像，输出像素坐标系下的位姿（中心 + 角度）。
/// 外参变换（像素→机器人）由 CalibrationManager 统一完成，策略不做坐标变换。
/// CancellationToken 用于"等待模型信号量"阶段的可取消（多工位共用模型排队时响应超时）；
/// 进入 ONNX 推理后不可中断（Yolo 无法取消），取消在推理返回后由调用方处理。
/// </summary>
public interface IAngleStrategy
{
    List<PixelPose> Compute(Mat undistorted, RecipeConfig recipe, CancellationToken ct = default);
}
