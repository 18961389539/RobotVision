namespace RobotVision.Core.Models;

/// <summary>
/// 检测区域（ROI），相对比例坐标：X/Y 为左上角原点，Width/Height 为宽高，
/// 全部取值 ∈ [0,1]（相对图像宽度/高度的比例）。null = 全图推理。
/// 推理在裁剪后的区域上进行，结果坐标自动偏移回全图坐标系。
/// </summary>
public sealed record Roi(double X, double Y, double Width, double Height);
