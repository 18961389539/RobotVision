using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Assets;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>工位映射模式、指纹与像素→机器人坐标换算。</summary>
internal sealed class StationMappingOrchestrator
{
    private static readonly JsonSerializerOptions FingerprintJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly CalibrationStores _stores;

    public StationMappingOrchestrator(CalibrationStores stores) => _stores = stores;

    public StationMappingMode GetMappingMode(string? stationId)
    {
        if (string.IsNullOrEmpty(stationId))
            return StationMappingMode.None;
        if (_stores.Polynomials.Contains(stationId))
            return StationMappingMode.Polynomial;
        if (_stores.Extrinsics.Contains(stationId))
            return StationMappingMode.Extrinsic;
        if (_stores.Scales.Contains(stationId))
            return StationMappingMode.Scale;
        return StationMappingMode.None;
    }

    public string? ComputeStationSha256(string? stationId, bool includeRotation = false, string? undistortCameraId = null)
    {
        if (string.IsNullOrEmpty(stationId))
            return null;

        var mode = GetMappingMode(stationId);
        MappingFingerprint? mapping = mode switch
        {
            StationMappingMode.Polynomial when _stores.Polynomials.Get(stationId) is { } poly =>
                FromPolynomial(poly),
            StationMappingMode.Extrinsic when _stores.Extrinsics.Get(stationId) is { } ext =>
                FromExtrinsic(ext),
            StationMappingMode.Scale when _stores.Scales.Get(stationId) is { } scale =>
                FromScale(scale),
            _ => null,
        };
        if (mapping is null)
            return null;

        IntrinsicFingerprint? intrinsic = null;
        if (mode == StationMappingMode.Extrinsic)
        {
            var cameraId = string.IsNullOrWhiteSpace(undistortCameraId)
                ? mapping.CameraId
                : undistortCameraId;
            if (_stores.Intrinsics.TryGetProfile(cameraId, out var profile))
                intrinsic = FromIntrinsic(profile);
        }

        RotationFingerprint? rotation = null;
        if (includeRotation && _stores.RotationCenters.Get(stationId) is { } rot)
            rotation = FromRotation(rot);

        return FileSha256.ComputeUtf8(
            JsonSerializer.Serialize(new StationFingerprint(mapping, intrinsic, rotation), FingerprintJson));
    }

    public void VerifyPolynomialResolution(string stationId, int width, int height)
    {
        var profile = _stores.Polynomials.Get(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做多项式标定: {stationId}");
        if (profile.Width != width || profile.Height != height)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"图像分辨率 {width}x{height} 与多项式标定档案 {profile.Width}x{profile.Height} 不一致，请重新标定");
    }

    public void VerifyScaleResolution(string stationId, int width, int height)
    {
        var profile = _stores.Scales.Get(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未录入比例标定: {stationId}");
        if (profile.Width > 0 && (profile.Width != width || profile.Height != height))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"图像分辨率 {width}x{height} 与比例标定档案 {profile.Width}x{profile.Height} 不一致，请重新录入比例");
    }

    public RobotPose PixelToRobotPolynomial(string stationId, PixelPose pose, string? cameraId = null, TcpClientPose? clientPose = null)
    {
        if (_stores.Polynomials.Get(stationId) is not { } profile)
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做多项式标定: {stationId}");

        if (!string.IsNullOrEmpty(cameraId) &&
            !string.Equals(profile.CameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"多项式标定相机 {profile.CameraId} 与取图相机 {cameraId} 不一致，请重新标定");

        var (x, y) = profile.Evaluate(pose.Cx, pose.Cy);

        const double Epsilon = 2.0;
        var rad = pose.AngleDeg * Math.PI / 180.0;
        var (fx, fy) = profile.Evaluate(pose.Cx + Epsilon * Math.Cos(rad), pose.Cy + Epsilon * Math.Sin(rad));
        var angleDeg = Math.Atan2(fy - y, fx - x) * 180.0 / Math.PI;

        var translate = clientPose is not null &&
                        string.Equals(profile.CoordinateSpace, PolynomialCoordinateSpace.Robot, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(profile.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(profile.ComposeMode, PoseComposeMode.Translate, StringComparison.OrdinalIgnoreCase) &&
                        profile.HasTeachPose;
        if (translate)
        {
            x += clientPose!.X - profile.TeachTcpX;
            y += clientPose.Y - profile.TeachTcpY;
        }

        return new RobotPose(x, y, AngleGeometry.NormalizeSignedDeg(angleDeg));
    }

    public RobotPose PixelToRobotScale(string stationId, PixelPose pose, string? cameraId = null)
    {
        if (_stores.Scales.Get(stationId) is not { } profile)
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未录入比例标定: {stationId}");

        if (!string.IsNullOrEmpty(cameraId) &&
            !string.Equals(profile.CameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"比例标定相机 {profile.CameraId} 与取图相机 {cameraId} 不一致，请重新录入");

        var x = pose.Cx * profile.ScaleX;
        var y = pose.Cy * profile.ScaleY;

        var rad = pose.AngleDeg * Math.PI / 180.0;
        var angleDeg = Math.Atan2(profile.ScaleY * Math.Sin(rad), profile.ScaleX * Math.Cos(rad)) * 180.0 / Math.PI;

        return new RobotPose(x, y, AngleGeometry.NormalizeSignedDeg(angleDeg));
    }

    public RobotPose PixelToRobot(string? stationId, PixelPose pose, string? cameraId = null, TcpClientPose? clientPose = null)
    {
        if (string.IsNullOrEmpty(stationId))
            throw new VisionException(VisionErrorCode.NotCalibrated, "配方未设置 stationId");

        if (_stores.Extrinsics.Get(stationId) is not { } profile)
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参标定: {stationId}");

        if (!string.IsNullOrEmpty(cameraId) &&
            !string.Equals(profile.CameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"外参相机 {profile.CameraId} 与取图相机 {cameraId} 不一致，请重新标定");

        _stores.Intrinsics.VerifyResolutionConsistency(profile.CameraId, profile.Width, profile.Height, "外参");

        var a = profile.Affine;
        double Tx(double x, double y) => a[0] * x + a[1] * y + a[2];
        double Ty(double x, double y) => a[3] * x + a[4] * y + a[5];

        const double ForwardPx = 100.0;
        var rad = pose.AngleDeg * Math.PI / 180.0;
        var qx = pose.Cx + ForwardPx * Math.Cos(rad);
        var qy = pose.Cy + ForwardPx * Math.Sin(rad);

        var angleDeg = Math.Atan2(
            Ty(qx, qy) - Ty(pose.Cx, pose.Cy),
            Tx(qx, qy) - Tx(pose.Cx, pose.Cy)) * 180.0 / Math.PI;

        var x = Tx(pose.Cx, pose.Cy);
        var y = Ty(pose.Cx, pose.Cy);

        var translate = clientPose is not null &&
                        string.Equals(profile.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(profile.ComposeMode, PoseComposeMode.Translate, StringComparison.OrdinalIgnoreCase) &&
                        profile.HasTeachPose;
        if (translate)
        {
            x += clientPose!.X - profile.TeachTcpX;
            y += clientPose.Y - profile.TeachTcpY;
        }

        return new RobotPose(x, y, AngleGeometry.NormalizeSignedDeg(angleDeg));
    }

    public Point2d RotationCenterRobot(string stationId)
    {
        if (_stores.RotationCenters.Get(stationId) is not { } rc)
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做旋转中心标定: {stationId}");

        var mapping = GetMapping(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参/多项式标定: {stationId}");

        if (!string.Equals(mapping.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"映射相机 {mapping.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，请重新标定");

        if (_stores.Extrinsics.Get(stationId) is { } ext)
        {
            _stores.Intrinsics.VerifyResolutionConsistency(ext.CameraId, ext.Width, ext.Height, "外参");
            _stores.Intrinsics.VerifyResolutionConsistency(rc.CameraId, rc.Width, rc.Height, "旋转中心");
        }
        else if (_stores.Polynomials.Get(stationId) is { } poly &&
                 (rc.Width != poly.Width || rc.Height != poly.Height) &&
                 rc.Width > 0 && rc.Height > 0)
        {
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"旋转中心标定分辨率 {rc.Width}x{rc.Height} 与多项式档案 {poly.Width}x{poly.Height} 不一致（须在同一原图坐标系下标定）");
        }

        return new Point2d(mapping.MapX(rc.Cx, rc.Cy), mapping.MapY(rc.Cx, rc.Cy));
    }

    internal (Func<double, double, double> MapX, Func<double, double, double> MapY, string CameraId)? GetMapping(string stationId)
    {
        if (_stores.Polynomials.Get(stationId) is { } poly)
            return ((x, y) => poly.Evaluate(x, y).X, (x, y) => poly.Evaluate(x, y).Y, poly.CameraId);

        if (_stores.Extrinsics.Get(stationId) is { } ext)
        {
            var a = ext.Affine;
            return ((x, y) => a[0] * x + a[1] * y + a[2], (x, y) => a[3] * x + a[4] * y + a[5], ext.CameraId);
        }

        return null;
    }

    private sealed record StationFingerprint(
        MappingFingerprint Mapping, IntrinsicFingerprint? Intrinsic, RotationFingerprint? Rotation);

    private sealed record MappingFingerprint(
        string Kind,
        string StationId,
        string CameraId,
        int Width,
        int Height,
        double[]? Affine,
        int Order,
        double[]? CoefX,
        double[]? CoefY,
        double ScaleX,
        double ScaleY,
        string MountType,
        string ComposeMode,
        string CoordinateSpace,
        double TeachTcpX,
        double TeachTcpY,
        double TeachRzDeg,
        bool HasTeachPose,
        double CalibrationPlaneZ);

    private sealed record IntrinsicFingerprint(
        string CameraId, int Width, int Height, double[] CameraMatrix, double[] DistCoeffs);

    private sealed record RotationFingerprint(
        string StationId, string CameraId, double Cx, double Cy, double RadiusPx,
        int Width, int Height, double ToolOffsetDeg);

    private static MappingFingerprint FromPolynomial(PolynomialProfile p) => new(
        "polynomial", p.StationId, p.CameraId, p.Width, p.Height,
        Affine: null, p.Order, p.CoefX, p.CoefY, 0, 0,
        p.MountType, p.ComposeMode, p.CoordinateSpace,
        p.TeachTcpX, p.TeachTcpY, p.TeachRzDeg, p.HasTeachPose, p.CalibrationPlaneZ);

    private static MappingFingerprint FromExtrinsic(ExtrinsicProfile p) => new(
        "extrinsic", p.StationId, p.CameraId, p.Width, p.Height,
        p.Affine, Order: 0, CoefX: null, CoefY: null, 0, 0,
        p.MountType, p.ComposeMode, CoordinateSpace: "",
        p.TeachTcpX, p.TeachTcpY, p.TeachRzDeg, p.HasTeachPose, p.CalibrationPlaneZ);

    private static MappingFingerprint FromScale(ScaleProfile p) => new(
        "scale", p.StationId, p.CameraId, p.Width, p.Height,
        Affine: null, Order: 0, CoefX: null, CoefY: null, p.ScaleX, p.ScaleY,
        MountType: "", ComposeMode: "", CoordinateSpace: "",
        0, 0, 0, false, 0);

    private static IntrinsicFingerprint FromIntrinsic(IntrinsicProfile p) =>
        new(p.CameraId, p.Width, p.Height, p.CameraMatrix, p.DistCoeffs);

    private static RotationFingerprint FromRotation(RotationCenterProfile p) =>
        new(p.StationId, p.CameraId, p.Cx, p.Cy, p.RadiusPx, p.Width, p.Height, p.ToolOffsetDeg);
}
