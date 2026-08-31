using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>旋转中心补偿、方向自检与工具偏角实测。</summary>
internal sealed class RotationCompensationService
{
    private readonly CalibrationStores _stores;
    private readonly StationMappingOrchestrator _mapping;

    public RotationCompensationService(CalibrationStores stores, StationMappingOrchestrator mapping)
    {
        _stores = stores;
        _mapping = mapping;
    }

    public RobotPose CompensateRotation(string? stationId, RotationCompensationMode mode, RobotPose pose)
    {
        if (mode == RotationCompensationMode.None || string.IsNullOrEmpty(stationId))
            return pose;

        var center = _mapping.RotationCenterRobot(stationId);
        var toolOffset = (_stores.RotationCenters.Get(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做旋转中心标定: {stationId}")).ToolOffsetDeg;
        return RotationCenterCompensation.Apply(pose, center.X, center.Y, toolOffset);
    }

    public void VerifyRotationDirection(string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg)
    {
        if (points.Length != anglesDeg.Length || points.Length < 3)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"方向自检需要 ≥3 个带角度记录的标记点，当前 {Math.Min(points.Length, anglesDeg.Length)} 个");
        var verifyMapping = _mapping.GetMapping(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参/多项式标定，无法做方向自检: {stationId}");
        if (!string.Equals(verifyMapping.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"映射相机 {verifyMapping.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，无法做方向自检");

        var cx = verifyMapping.MapX(rc.Cx, rc.Cy);
        var cy = verifyMapping.MapY(rc.Cx, rc.Cy);

        var samples = points.Select((p, i) => (
                Angle: anglesDeg[i],
                Bearing: Math.Atan2(verifyMapping.MapY(p.X, p.Y) - cy, verifyMapping.MapX(p.X, p.Y) - cx) * 180.0 / Math.PI))
            .OrderBy(s => s.Angle)
            .ToArray();

        var pairs = 0;
        var consistent = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            var dBearing = CalibrationAngleMath.NormalizeDelta(samples[i].Bearing - samples[i - 1].Bearing);
            var dAngle = CalibrationAngleMath.NormalizeDelta(samples[i].Angle - samples[i - 1].Angle);
            if (Math.Abs(dAngle) < 1e-9)
                continue;
            pairs++;
            if (Math.Sign(dBearing) == Math.Sign(dAngle))
                consistent++;
        }

        if (pairs >= 2 && consistent * 10 < pairs * 6)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"方向自检失败：{consistent}/{pairs} 个相邻角度对方向一致（需 ≥60%）。" +
                "第 4 轴正方向与图像旋转方向相反或角度记录有误——请在示教侧取反角度，或检查录入的第 4 轴角度");
    }

    public (double OffsetDeg, double SpreadDeg) ComputeToolOffsetDeg(
        string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg)
    {
        if (points.Length != anglesDeg.Length || points.Length < 2)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"实测偏角需要 ≥2 个带角度记录的标记点，当前 {Math.Min(points.Length, anglesDeg.Length)} 个");
        var offsetMapping = _mapping.GetMapping(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参/多项式标定，无法实测偏角: {stationId}");
        if (!string.Equals(offsetMapping.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"映射相机 {offsetMapping.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，无法实测偏角");

        var cx = offsetMapping.MapX(rc.Cx, rc.Cy);
        var cy = offsetMapping.MapY(rc.Cx, rc.Cy);

        var deltas = new double[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var bearing = Math.Atan2(offsetMapping.MapY(points[i].X, points[i].Y) - cy,
                                     offsetMapping.MapX(points[i].X, points[i].Y) - cx) * 180.0 / Math.PI;
            deltas[i] = CalibrationAngleMath.NormalizeDelta(bearing - anglesDeg[i]);
        }

        var cosSum = 0.0;
        var sinSum = 0.0;
        foreach (var d in deltas)
        {
            var rad = d * Math.PI / 180.0;
            cosSum += Math.Cos(rad);
            sinSum += Math.Sin(rad);
        }
        var mean = AngleGeometry.NormalizeSignedDeg(Math.Atan2(sinSum, cosSum) * 180.0 / Math.PI);

        var spread = 0.0;
        for (var i = 0; i < deltas.Length; i++)
            for (var j = i + 1; j < deltas.Length; j++)
                spread = Math.Max(spread, Math.Abs(CalibrationAngleMath.NormalizeDelta(deltas[i] - deltas[j])));

        return (mean, spread);
    }
}
