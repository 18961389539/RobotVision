namespace RobotVision.Core.Models;

/// <summary>
/// 相机内参档案（按相机序列号存储）。
/// CameraMatrix 为 3x3 行主序 9 元素 [fx,0,cx, 0,fy,cy, 0,0,1]；
/// DistCoeffs 为 OpenCV 畸变系数 (k1,k2,p1,p2,k3,...)。
/// </summary>
public sealed record IntrinsicProfile
{
    public string CameraId { get; init; } = "";

    public int Width { get; init; }

    public int Height { get; init; }

    public double[] CameraMatrix { get; init; } = [];

    public double[] DistCoeffs { get; init; } = [];

    /// <summary>重投影 RMS（px），验收参考：≤0.3 优秀，≤0.5 可用。</summary>
    public double Rms { get; init; }

    /// <summary>参与标定的有效图像数（检测成功的棋盘图）。</summary>
    public int ImageCount { get; init; }

    /// <summary>逐图重投影 RMS（与参与标定的图像一一对应，用于定位坏图）。</summary>
    public IReadOnlyList<double>? PerImageRms { get; init; }

    public DateTime CalibratedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// 工位外参档案（像素→机器人，2x3 仿射，行主序 6 元素）。
/// 残差单位与机器人坐标一致（通常 mm）。
/// </summary>
public sealed record ExtrinsicProfile
{
    public string StationId { get; init; } = "";

    public string CameraId { get; init; } = "";

    public double[] Affine { get; init; } = [];

    public double Rms { get; init; }

    public double MaxResidual { get; init; }

    /// <summary>逐点残差（与输入点对一一对应，用于定位抄错的点）。</summary>
    public double[]? PointResiduals { get; init; }

    /// <summary>留一交叉验证最大误差：每个点用其余点拟合后预测该点的误差（发现单个误点的最敏感指标）。</summary>
    public double LeaveOneOutMax { get; init; }

    public DateTime CalibratedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// 旋转中心档案（按工位存储）：第 4 轴/旋转平台轴心在像素坐标系中的位置。
/// 由标记点随第 4 轴旋转多角度的轨迹拟合圆得到（≥5 点用 FitEllipse，3~4 点用代数圆拟合）。
/// 像素坐标须取自去畸变后的图像（与外参标定同一坐标系）。
/// </summary>
public sealed record RotationCenterProfile
{
    public string StationId { get; init; } = "";

    public string CameraId { get; init; } = "";

    /// <summary>轴心像素坐标 x。</summary>
    public double Cx { get; init; }

    /// <summary>轴心像素坐标 y。</summary>
    public double Cy { get; init; }

    /// <summary>拟合半径（px）：标记点到轴心的距离，即偏心距的像素当量。</summary>
    public double RadiusPx { get; init; }

    /// <summary>各点到轴心距离与半径之差的 RMS（px），标定质量指标。</summary>
    public double Rms { get; init; }

    /// <summary>椭圆长短轴比（≥5 点时有效，1=正圆）。明显偏离 1 说明标记提取不稳或机械抖动。</summary>
    public double AxisRatio { get; init; } = 1.0;

    public int PointCount { get; init; }

    public DateTime CalibratedAt { get; init; } = DateTime.Now;
}
