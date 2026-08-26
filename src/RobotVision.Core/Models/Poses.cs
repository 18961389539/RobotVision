namespace RobotVision.Core.Models;

/// <summary>像素坐标系下的检测结果（图像坐标，y 轴向下）。</summary>
/// <param name="Cx">中心 x（px）。</param>
/// <param name="Cy">中心 y（px）。</param>
/// <param name="AngleDeg">角度（度）。</param>
/// <param name="Score">置信度。</param>
public sealed record PixelPose(double Cx, double Cy, double AngleDeg, double Score)
{
    /// <summary>检测叠加数据（可选）：mask 轮廓/检测框/关键点，全图像素坐标。
    /// 仅供画面绘制（OverlayDrawer），不参与位姿计算与标定变换。</summary>
    public PoseOverlay? Overlay { get; init; }
}

/// <summary>全图像素坐标点。</summary>
public readonly record struct PixelPoint(double X, double Y);

/// <summary>全图像素坐标矩形。</summary>
public readonly record struct PixelRect(double X, double Y, double Width, double Height);

/// <summary>
/// 单个目标的检测叠加数据（全图像素坐标系）：附着于 <see cref="PixelPose"/> 随快照发布，
/// 仅供画面绘制，不参与位姿计算与标定变换。
/// 各字段按策略类型填充：分割/BLOB 给轮廓，检测给框（主/次各一），姿态给关键点。
/// </summary>
public sealed record PoseOverlay
{
    /// <summary>目标轮廓（闭合折线）；分割/BLOB 模式提供。</summary>
    public IReadOnlyList<PixelPoint>? Contour { get; init; }

    /// <summary>检测框（单模型 1 个，双模型主/次 2 个）；检测/姿态模式提供。</summary>
    public IReadOnlyList<PixelRect>? Boxes { get; init; }

    /// <summary>关键点（与 <see cref="KeyPointConfidences"/> 等长）；姿态模式提供。</summary>
    public IReadOnlyList<PixelPoint>? KeyPoints { get; init; }

    /// <summary>关键点置信度；低置信关键点绘制时灰显。可为 null。</summary>
    public IReadOnlyList<double>? KeyPointConfidences { get; init; }

    /// <summary>角度基线（恰好 2 点：起点=主特征中心，终点=次特征中心）；
    /// 双特征模式（双模型/双BLOB）提供，用于画面上验证配对关系。</summary>
    public IReadOnlyList<PixelPoint>? Baseline { get; init; }

    /// <summary>类别标签（模型输出）；纯图像处理模式为 null。</summary>
    public string? Label { get; init; }
}

/// <summary>机器人坐标系下的输出位姿。</summary>
public sealed record RobotPose(double X, double Y, double AngleDeg);
