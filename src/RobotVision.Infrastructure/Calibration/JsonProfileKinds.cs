using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>
/// 外参档案（按工位 Id）：像素坐标到机器人坐标的仿射变换。
/// 规则原在 <c>CalibrationManager.ValidateExtrinsic/AssessExtrinsic/LoadExtrinsic</c>，
/// 现与同类的质量告警收拢到一处，供 <see cref="JsonProfileStore{TProfile}"/> 驱动。
/// </summary>
internal sealed class ExtrinsicKind : IJsonProfileKind<ExtrinsicProfile>
{
    public static readonly ExtrinsicKind Instance = new();

    private ExtrinsicKind()
    {
    }

    public string Kind => "extrinsic";

    public string IdOf(ExtrinsicProfile profile) => profile.StationId;

    /// <summary>
    /// 外参档案值域校验：Affine 必须为 6 元素且全部有限、Rms/MaxResidual 非负且有限。
    /// 损坏档案在加载时即拒绝（1099 InternalError），避免仿射映射产生 NaN 坐标被误当真实位姿（安全问题）。
    /// </summary>
    public void Validate(ExtrinsicProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "外参 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的 CameraId 为空");
        if (profile.Affine is not { Length: 6 } || profile.Affine.Any(v => !double.IsFinite(v)))
            throw new VisionException(VisionErrorCode.InternalError,
                $"外参 {profile.StationId} 的 Affine 必须为 6 个有限数值，当前 {profile.Affine?.Length ?? 0}");
        if (!double.IsFinite(profile.Rms) || profile.Rms < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的 Rms 非法: {profile.Rms}");
        if (!double.IsFinite(profile.MaxResidual) || profile.MaxResidual < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的 MaxResidual 非法: {profile.MaxResidual}");
        if (!CameraMountType.IsValid(profile.MountType))
            throw new VisionException(VisionErrorCode.InternalError,
                $"外参 {profile.StationId} 的 MountType 非法: {profile.MountType}（仅支持 Fixed/OnArm）");
        if (!PoseComposeMode.IsValid(profile.ComposeMode))
            throw new VisionException(VisionErrorCode.InternalError,
                $"外参 {profile.StationId} 的 ComposeMode 非法: {profile.ComposeMode}（仅 Check/Translate）");
        if (!double.IsFinite(profile.TeachTcpX) || !double.IsFinite(profile.TeachTcpY) ||
            !double.IsFinite(profile.TeachRzDeg) || !double.IsFinite(profile.CalibrationPlaneZ))
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的拍照位姿/标定平面字段含非有限值");
    }

    public CalibrationQuality Assess(ExtrinsicProfile p) =>
        p.MaxResidual <= CalibrationManager.ExtrinsicResidualGood ? CalibrationQuality.Good
        : p.MaxResidual <= CalibrationManager.ExtrinsicResidualFair ? CalibrationQuality.Fair
        : CalibrationQuality.Poor;

    public void CheckQuality(ExtrinsicProfile profile, Action<string> warn)
    {
        if (Assess(profile) == CalibrationQuality.Poor)
            warn($"外参 {profile.StationId} 质量超标: 最大残差 {profile.MaxResidual:0.000}（>{CalibrationManager.ExtrinsicResidualFair:0.0} 可用上限），建议重新标定");
        if (profile.LeaveOneOutMax > CalibrationManager.LeaveOneOutWarnLimit)
            warn($"外参 {profile.StationId} 留一最大误差 {profile.LeaveOneOutMax:0.000} 偏大（>{CalibrationManager.LeaveOneOutWarnLimit:0.0}），疑似存在抄错/误点，请核对点对");
    }
}

/// <summary>
/// 旋转中心档案（按工位 Id）：第 4 轴轴心，偏心工具补偿用。
/// </summary>
internal sealed class RotationCenterKind : IJsonProfileKind<RotationCenterProfile>
{
    public static readonly RotationCenterKind Instance = new();

    private RotationCenterKind()
    {
    }

    public string Kind => "rotation";

    public string IdOf(RotationCenterProfile profile) => profile.StationId;

    /// <summary>旋转中心档案值域校验：Cx/Cy/RadiusPx/Rms 必须有限，Rms 非负。损坏档案拒绝加载（1099 InternalError）。</summary>
    public void Validate(RotationCenterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "旋转中心 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 CameraId 为空");
        if (!double.IsFinite(profile.Cx) || !double.IsFinite(profile.Cy) ||
            !double.IsFinite(profile.RadiusPx) || !double.IsFinite(profile.AxisRatio))
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的数值非法（非有限值）");
        if (!double.IsFinite(profile.Rms) || profile.Rms < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 Rms 非法: {profile.Rms}");
        if (profile.AxisRatio < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 AxisRatio 非法: {profile.AxisRatio}");
        if (!double.IsFinite(profile.ToolOffsetDeg))
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 ToolOffsetDeg 非法: {profile.ToolOffsetDeg}");
    }

    public CalibrationQuality Assess(RotationCenterProfile p)
    {
        if (p.Rms > CalibrationManager.RotationRmsFair)
            return CalibrationQuality.Poor;
        // 长短轴比仅在点数足够（≥5）时才有统计意义：点太少拟合出的椭圆长短轴不可靠
        if (p.PointCount >= 5 && p.AxisRatio > CalibrationManager.RotationAxisRatioLimit)
            return CalibrationQuality.Poor;
        return p.Rms <= CalibrationManager.RotationRmsGood ? CalibrationQuality.Good : CalibrationQuality.Fair;
    }

    public void CheckQuality(RotationCenterProfile profile, Action<string> warn)
    {
        if (Assess(profile) != CalibrationQuality.Poor)
            return;
        warn($"旋转中心 {profile.StationId} 质量超标: RMS {profile.Rms:0.000}px"
             + (profile.PointCount >= 5 && profile.AxisRatio > CalibrationManager.RotationAxisRatioLimit
                 ? $"，长短轴比 {profile.AxisRatio:0.00}"
                 : "")
             + "，建议重新标定");
    }
}

/// <summary>
/// 多项式档案（按工位 Id）：单图模式，原图推理 + 多项式映射到机器人/棋盘毫米系。
/// </summary>
internal sealed class PolynomialKind : IJsonProfileKind<PolynomialProfile>
{
    public static readonly PolynomialKind Instance = new();

    private PolynomialKind()
    {
    }

    public string Kind => "polynomial";

    public string IdOf(PolynomialProfile profile) => profile.StationId;

    /// <summary>多项式档案校验：Id/相机、阶数 2~3、系数个数与阶数匹配且全有限、分辨率、残差非负。
    /// 系数损坏（个数不符/NaN）会让 Evaluate 输出垃圾坐标，加载时拒绝。</summary>
    public void Validate(PolynomialProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "多项式档案 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的 CameraId 为空");
        if (profile.Order is not (2 or 3))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的阶数非法: {profile.Order}（仅 2/3）");
        var expected = (profile.Order + 1) * (profile.Order + 2) / 2;
        if (profile.CoefX.Length != expected || profile.CoefY.Length != expected)
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的系数个数非法: X {profile.CoefX.Length} / Y {profile.CoefY.Length}（{profile.Order} 阶应为 {expected}）");
        if (profile.CoefX.Concat(profile.CoefY).Any(v => !double.IsFinite(v)))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的系数含非有限值，档案已损坏");
        if (profile.Width <= 0 || profile.Height <= 0)
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的分辨率非法: {profile.Width}x{profile.Height}");
        if (!double.IsFinite(profile.Rms) || profile.Rms < 0 || !double.IsFinite(profile.MaxResidual) || profile.MaxResidual < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的残差指标非法");
        if (!CameraMountType.IsValid(profile.MountType))
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的 MountType 非法: {profile.MountType}（仅 Fixed/OnArm）");
        if (!PolynomialCoordinateSpace.IsValid(profile.CoordinateSpace))
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的 CoordinateSpace 非法: {profile.CoordinateSpace}（仅 Robot/Image）");
        if (!PoseComposeMode.IsValid(profile.ComposeMode))
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的 ComposeMode 非法: {profile.ComposeMode}（仅 Check/Translate）");
        if (!double.IsFinite(profile.TeachTcpX) || !double.IsFinite(profile.TeachTcpY) ||
            !double.IsFinite(profile.TeachRzDeg) || !double.IsFinite(profile.CalibrationPlaneZ))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的位姿/平面字段含非有限值");
    }

    public CalibrationQuality Assess(PolynomialProfile p) =>
        p.MaxResidual <= CalibrationManager.ExtrinsicResidualGood ? CalibrationQuality.Good
        : p.MaxResidual <= CalibrationManager.ExtrinsicResidualFair ? CalibrationQuality.Fair
        : CalibrationQuality.Poor;

    public void CheckQuality(PolynomialProfile profile, Action<string> warn)
    {
        if (profile.MaxResidual > CalibrationManager.ExtrinsicResidualFair)
            warn($"多项式标定 {profile.StationId} 质量超标: 最大残差 {profile.MaxResidual:0.000}（>{CalibrationManager.ExtrinsicResidualFair:0.0} 可用上限），建议重新标定");
    }
}

/// <summary>
/// 比例档案（按工位 Id）：无标定板工位的回退路径，像素→图像平面毫米。
/// </summary>
internal sealed class ScaleKind : IJsonProfileKind<ScaleProfile>
{
    public static readonly ScaleKind Instance = new();

    private ScaleKind()
    {
    }

    public string Kind => "scale";

    public string IdOf(ScaleProfile profile) => profile.StationId;

    /// <summary>比例档案校验：Id/相机非空，比例 &gt; 0 且有限（0 或负数无物理意义；非有限值 = 档案损坏），
    /// 分辨率 ≥ 0（0 = 未记录，跳过一致性校验）。手动录入无法验证数值真伪，只能挡住明显笔误。</summary>
    public void Validate(ScaleProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "比例档案 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"比例档案 {profile.StationId} 的 CameraId 为空");
        if (!double.IsFinite(profile.ScaleX) || profile.ScaleX <= 0 ||
            !double.IsFinite(profile.ScaleY) || profile.ScaleY <= 0)
            throw new VisionException(VisionErrorCode.InternalError,
                $"比例档案 {profile.StationId} 的比例非法: {profile.ScaleX} / {profile.ScaleY} mm/px（须为正数）");
        if (profile.Width < 0 || profile.Height < 0)
            throw new VisionException(VisionErrorCode.InternalError,
                $"比例档案 {profile.StationId} 的分辨率非法: {profile.Width}x{profile.Height}");
    }

    /// <summary>
    /// 比例档案无残差类质量指标，唯一可用的健康信号是 X/Y 各向异性：
    /// |kx/ky − 1| 超过 2% 说明存在旋转/透视/畸变，线性比例只能近似。
    /// <para>注意：<c>AssessScale</c> 从未作为 public API 存在过，此处仅驱动载入告警，
    /// 不对外暴露，避免无谓扩大 API 面。</para>
    /// </summary>
    public CalibrationQuality Assess(ScaleProfile p) =>
        AnisotropyRatio(p) > CalibrationManager.ScaleAnisotropyWarnLimit
            ? CalibrationQuality.Poor
            : CalibrationQuality.Good;

    public void CheckQuality(ScaleProfile profile, Action<string> warn)
    {
        var ratio = AnisotropyRatio(profile);
        if (ratio <= CalibrationManager.ScaleAnisotropyWarnLimit)
            return;
        warn($"比例标定 {profile.StationId} X/Y 各向异性 {ratio * 100:0.0}%（>{CalibrationManager.ScaleAnisotropyWarnLimit * 100:0}%）："
             + "疑似存在旋转/透视/畸变，线性比例仅为近似值，建议改用多项式标定");
    }

    /// <summary>X/Y 各向异性：|max/min − 1|。比例为 0 时 Validate 已拒绝，此处不会除零。</summary>
    private static double AnisotropyRatio(ScaleProfile p) =>
        Math.Max(p.ScaleX, p.ScaleY) / Math.Min(p.ScaleX, p.ScaleY) - 1;
}
