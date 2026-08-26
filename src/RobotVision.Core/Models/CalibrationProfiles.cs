namespace RobotVision.Core.Models;

/// <summary>相机安装模式：Fixed = 固定机架（eye-to-hand）；OnArm = 装在机器人末端（eye-in-hand）。
/// OnArm 档案只在"标定时记录的拍照位姿"下有效——换拍照点/改拍照 RZ 即失效，
/// 换位姿必须重标该工位外参。</summary>
public static class CameraMountType
{
    public const string Fixed = "Fixed";

    public const string OnArm = "OnArm";

    public static bool IsValid(string value) =>
        string.Equals(value, Fixed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, OnArm, StringComparison.OrdinalIgnoreCase);
}

/// <summary>OnArm 工位的 TRIGGER 位姿处理模式：
/// Check = 仅校验（位姿必须与标定一致，超容差 1012）；
/// Translate = 平移合成（相机只有平移、姿态不变时，位置映射 + (当前TCP−示教TCP)，
/// RZ 仍须一致——换拍照点不重标，位姿从"拦截器"变"合成器"）。</summary>
public static class PoseComposeMode
{
    public const string Check = "Check";

    public const string Translate = "Translate";

    public static bool IsValid(string value) =>
        string.Equals(value, Check, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, Translate, StringComparison.OrdinalIgnoreCase);
}

/// <summary>多项式标定的输出坐标空间：
/// Robot = 机器人系（由 2 个示教参考点锚定，输出可直接给机器人）；
/// Image = 棋盘平面毫米系（免示教，原点=标定棋盘首角点，轴=棋盘行列方向）——
/// 只解"像素→毫米"的比例/畸变/旋转，不锚定机器人系。适合上位机自行换算或纯测量场景。</summary>
public static class PolynomialCoordinateSpace
{
    public const string Robot = "Robot";

    public const string Image = "Image";

    public static bool IsValid(string value) =>
        string.Equals(value, Robot, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, Image, StringComparison.OrdinalIgnoreCase);
}

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

    /// <summary>标定时像素坐标系分辨率（与内参档案一致性校验用；0 = 旧版档案未记录，跳过校验）。
    /// 换相机/改分辨率后内参重标，外参像素坐标系即失效——不一致时拒绝使用，防静默错位。</summary>
    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>相机安装模式（Fixed=固定机架 / OnArm=装在末端随动）。见 <see cref="CameraMountType"/>。
    /// 旧版档案缺失时默认 Fixed。OnArm 档案依赖下方记录的拍照位姿。</summary>
    public string MountType { get; init; } = CameraMountType.Fixed;

    /// <summary>OnArm：标定时拍照点的 TCP X（机器人系）。生产时拍照位姿必须与此一致；是否已记录由 HasTeachPose 决定。</summary>
    public double TeachTcpX { get; init; }

    /// <summary>OnArm：标定时拍照点的 TCP Y。</summary>
    public double TeachTcpY { get; init; }

    /// <summary>OnArm：标定时拍照点第 4 轴角度（deg）。拍照 RZ 改变图像方向与变换，必须一致。</summary>
    public double TeachRzDeg { get; init; }

    /// <summary>是否已记录标定拍照位姿。显式标志而非 (0,0,0) 哨兵——拍照点恰为坐标原点的
    /// 工位不该被误判为"未记录"而跳过校验。旧档案缺省 false（跳过校验，向后兼容）。</summary>
    public bool HasTeachPose { get; init; }

    /// <summary>OnArm 位姿处理：Check（须与标定一致）/ Translate（允许平移合成）。旧档案缺省 Check。</summary>
    public string ComposeMode { get; init; } = PoseComposeMode.Check;

    /// <summary>标定平面 Z 高度（机器人系，供多厚度零件分层标定比对；0 = 未记录）。
    /// 九点外参是单平面仿射：零件高度差会引入透视误差（≈ 视场偏移×Δh/工作距离），
    /// 高度差大的产线应按料厚分层标定多组档案。</summary>
    public double CalibrationPlaneZ { get; init; }

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

    /// <summary>标定时像素坐标系分辨率（与内参档案一致性校验用；0 = 旧版档案未记录，跳过校验）。</summary>
    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>工具零位偏角 δ（deg，机器人系）：第 4 轴零位时工具指向相对 X 轴的偏角。
    /// 补偿链路：第 4 轴转角 φ = 零件角 θ − δ；未补偿时位置误差 2r·sin(δ/2)。
    /// 通过"验证角"步骤实测（转已知角度检测工具方向），或从工具坐标系参数换算；0 = 无偏移。</summary>
    public double ToolOffsetDeg { get; init; }

    public DateTime CalibratedAt { get; init; } = DateTime.Now;
}

/// <summary>比例标定档案（按工位存储）：像素 → 图像平面毫米的线性比例（mm/px）。
/// 手动录入（现场用量具/产品特征/机器人示教测算后填入），不建模畸变/透视/安装角——
/// 视场边缘精度受镜头畸变限制，X/Y 比例差异大（各向异性）或精度要求高时应改用多项式标定。
/// 分辨率锁：换相机/改分辨率后比例失效，须重新录入。</summary>
public sealed record ScaleProfile
{
    public string StationId { get; init; } = "";

    public string CameraId { get; init; } = "";

    /// <summary>X 方向比例（mm/px）= 物长 mm / 图上像素数。</summary>
    public double ScaleX { get; init; }

    /// <summary>Y 方向比例（mm/px）。</summary>
    public double ScaleY { get; init; }

    /// <summary>录入时图像分辨率（比例的像素基准；0 = 未记录，跳过一致性校验）。</summary>
    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>录入方式备注（Manual / 量具 / 产品特征 / 机器人两点…），追溯精度可信度用。</summary>
    public string Method { get; init; } = "Manual";

    public DateTime CalibratedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// 多项式标定档案（按工位存储，VisionPro 式单图模式）：像素 → 机器人平面坐标的经验多项式映射，
/// 一个模型整体吸收镜头畸变 / 透视 / 安装角 / 像素当量——替代"内参去畸变 + 外参仿射"两步。
/// 有效范围：该档案标定时的相机姿态、单一工作平面、统一高度；推理直接用原图（跳过去畸变）。
/// 基函数（归一化坐标 u,v ∈ [-1,1]，u = 2·px/Width − 1）：
/// 二阶 [1,u,v,u²,uv,v²]（6 系数）；三阶追加 [u³,u²v,uv²,v³]（10 系数）。
/// CoefX/CoefY 为各轴系数，X = Σ CoefX[k]·Basis[k](u,v)。
/// </summary>
public sealed record PolynomialProfile
{
    public string StationId { get; init; } = "";

    public string CameraId { get; init; } = "";

    /// <summary>标定时图像分辨率（推理图像必须一致，否则映射错位）。</summary>
    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>多项式阶数：2（默认，小畸变）或 3（畸变较大，需更多网格点）。</summary>
    public int Order { get; init; } = 2;

    public double[] CoefX { get; init; } = [];

    public double[] CoefY { get; init; } = [];

    /// <summary>拟合残差 RMS（机器人单位）。</summary>
    public double Rms { get; init; }

    public double MaxResidual { get; init; }

    /// <summary>参与拟合的网格点数。</summary>
    public int PointCount { get; init; }

    public string MountType { get; init; } = CameraMountType.Fixed;

    /// <summary>输出坐标空间：Robot（示教锚定机器人系）/ Image（棋盘平面毫米系，免示教）。
    /// 见 <see cref="PolynomialCoordinateSpace"/>。旧档案缺省 Robot。</summary>
    public string CoordinateSpace { get; init; } = PolynomialCoordinateSpace.Robot;

    /// <summary>OnArm 位姿处理模式：Check（校验）/ Translate（平移合成）。见 <see cref="PoseComposeMode"/>。</summary>
    public string ComposeMode { get; init; } = PoseComposeMode.Check;

    public double TeachTcpX { get; init; }

    public double TeachTcpY { get; init; }

    public double TeachRzDeg { get; init; }

    public bool HasTeachPose { get; init; }

    /// <summary>标定平面 Z 高度（0 = 未记录）。</summary>
    public double CalibrationPlaneZ { get; init; }

    public DateTime CalibratedAt { get; init; } = DateTime.Now;

    /// <summary>多项式求值：像素坐标 → 机器人平面坐标。</summary>
    public (double X, double Y) Evaluate(double px, double py)
    {
        var u = 2.0 * px / Width - 1.0;
        var v = 2.0 * py / Height - 1.0;
        double x = 0, y = 0;
        var k = 0;
        for (var j = 0; j <= Order; j++)
            for (var i = 0; i + j <= Order; i++)
            {
                var basis = Math.Pow(u, i) * Math.Pow(v, j);
                x += CoefX[k] * basis;
                y += CoefY[k] * basis;
                k++;
            }
        return (x, y);
    }

    /// <summary>该阶数的系数个数：(order+1)(order+2)/2。</summary>
    public int CoefficientCount => CoefX.Length;
}
