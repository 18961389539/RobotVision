using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>OnArm 工位 TRIGGER 位姿校验（外参 / 多项式）。</summary>
internal sealed class ClientPoseValidator
{
    private readonly CalibrationStores _stores;
    private volatile bool _enabled = true;
    private double _xyToleranceMm = 0.5;
    private double _rzToleranceDeg = 0.5;

    public ClientPoseValidator(CalibrationStores stores) => _stores = stores;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public double XyToleranceMm
    {
        get => Volatile.Read(ref _xyToleranceMm);
        set => Volatile.Write(ref _xyToleranceMm, value);
    }

    public double RzToleranceDeg
    {
        get => Volatile.Read(ref _rzToleranceDeg);
        set => Volatile.Write(ref _rzToleranceDeg, value);
    }

    public bool ClientPoseRequired(string? stationId)
    {
        if (!Enabled || string.IsNullOrEmpty(stationId))
            return false;
        if (_stores.Polynomials.Get(stationId) is { } poly)
        {
            if (string.Equals(poly.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
                return false;
            return string.Equals(poly.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase)
                   && poly.HasTeachPose;
        }
        if (_stores.Extrinsics.Get(stationId) is { } ext)
            return string.Equals(ext.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase)
                   && ext.HasTeachPose;
        return false;
    }

    public void RequireClientPose(string? stationId, TcpClientPose? pose)
    {
        if (pose is not null || !ClientPoseRequired(stationId))
            return;
        throw new VisionException(VisionErrorCode.PoseRequired,
            "OnArm 工位必须使用 配方名或序列号,X,Y,RZ（未上报拍照位姿，拒绝执行以免输出错位坐标）");
    }

    public void VerifyClientPose(string? stationId, TcpClientPose pose)
    {
        if (!Enabled || string.IsNullOrEmpty(stationId))
            return;
        if (_stores.Extrinsics.Get(stationId) is not { } ext)
            return;
        if (!string.Equals(ext.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase))
            return;
        if (!ext.HasTeachPose)
            return;

        var rzDeviation = Math.Abs(CalibrationAngleMath.NormalizeDelta(pose.RzDeg - ext.TeachRzDeg));
        if (rzDeviation > RzToleranceDeg)
            throw new VisionException(VisionErrorCode.PoseMismatch,
                $"拍照姿态不一致: RZ 偏差 {rzDeviation:0.000}° 超容差 {RzToleranceDeg:0.0}°" +
                "（OnArm 工位拍照姿态必须与标定一致；Translate 模式只允许平移）");

        if (string.Equals(ext.ComposeMode, PoseComposeMode.Translate, StringComparison.OrdinalIgnoreCase))
            return;

        var dx = pose.X - ext.TeachTcpX;
        var dy = pose.Y - ext.TeachTcpY;
        var xyDeviation = Math.Sqrt(dx * dx + dy * dy);
        if (xyDeviation > XyToleranceMm)
            throw new VisionException(VisionErrorCode.PoseMismatch,
                $"拍照位姿不一致: 上报 ({pose.X:0.000},{pose.Y:0.000},{pose.RzDeg:0.000}°) " +
                $"与标定 ({ext.TeachTcpX:0.000},{ext.TeachTcpY:0.000},{ext.TeachRzDeg:0.000}°) " +
                $"偏差 XY {xyDeviation:0.000}mm / RZ {rzDeviation:0.000}° 超容差 " +
                $"({XyToleranceMm:0.0}mm/{RzToleranceDeg:0.0}°)。" +
                "OnArm 工位拍照位姿必须与标定一致，请核对拍照点或重标该工位外参；相机只有平移可改用 ComposeMode=Translate");
    }

    public void VerifyPolynomialClientPose(string? stationId, TcpClientPose? pose)
    {
        if (!Enabled || pose is null || string.IsNullOrEmpty(stationId))
            return;
        if (_stores.Polynomials.Get(stationId) is not { } profile)
            return;
        if (string.Equals(profile.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
            return;
        if (!string.Equals(profile.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) || !profile.HasTeachPose)
            return;

        var rzDeviation = Math.Abs(CalibrationAngleMath.NormalizeDelta(pose.RzDeg - profile.TeachRzDeg));
        if (rzDeviation > RzToleranceDeg)
            throw new VisionException(VisionErrorCode.PoseMismatch,
                $"拍照姿态不一致: RZ 偏差 {rzDeviation:0.000}° 超容差 {RzToleranceDeg:0.0}°" +
                "（多项式映射依赖固定相机姿态，Translate 模式只允许平移）");

        if (string.Equals(profile.ComposeMode, PoseComposeMode.Check, StringComparison.OrdinalIgnoreCase))
        {
            var dx = pose.X - profile.TeachTcpX;
            var dy = pose.Y - profile.TeachTcpY;
            var xyDeviation = Math.Sqrt(dx * dx + dy * dy);
            if (xyDeviation > XyToleranceMm)
                throw new VisionException(VisionErrorCode.PoseMismatch,
                    $"拍照位姿不一致: XY 偏差 {xyDeviation:0.000}mm 超容差 {XyToleranceMm:0.0}mm" +
                    "（Check 模式要求拍照点与标定一致；相机只有平移可改用 ComposeMode=Translate）");
        }
    }
}
