using OpenCvSharp;
using RobotVision.Core.Calibration;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.Hosting;

/// <summary>标定运行时（WPF/宿主稳定入口）。</summary>
public interface ICalibrationRuntime
{
    double ExtrinsicResidualFair { get; }
    double ScaleAnisotropyWarnLimit { get; }

    IReadOnlyList<IntrinsicProfile> IntrinsicProfiles { get; }
    IReadOnlyList<ExtrinsicProfile> ExtrinsicProfiles { get; }
    IReadOnlyList<RotationCenterProfile> RotationCenterProfiles { get; }
    IReadOnlyList<PolynomialProfile> PolynomialProfiles { get; }
    IReadOnlyList<ScaleProfile> ScaleProfiles { get; }

    ScaleProfile? GetScale(string? stationId);
    bool HasPolynomial(string? stationId);
    bool HasExtrinsic(string? stationId);
    StationMappingMode GetMappingMode(string? stationId);
    bool IsCalibrated(string cameraId);
    VisionImage Undistort(string cameraId, VisionImage src);

    void RequireIntrinsic(string cameraId);
    void LoadIntrinsic(IntrinsicProfile profile);
    void LoadExtrinsic(ExtrinsicProfile profile);
    void LoadPolynomial(PolynomialProfile profile);
    void LoadRotationCenter(RotationCenterProfile profile);
    void SaveScale(ScaleProfile profile);

    bool DeleteIntrinsic(string cameraId);
    bool DeleteExtrinsic(string stationId);
    bool DeleteRotationCenter(string stationId);
    bool DeletePolynomial(string stationId);
    bool DeleteScale(string stationId);

    void VerifyRotationDirection(string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg);
    (double OffsetDeg, double SpreadDeg) ComputeToolOffsetDeg(
        string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg);

    CalibrationQuality AssessIntrinsic(IntrinsicProfile profile);
    CalibrationQuality AssessExtrinsic(ExtrinsicProfile profile);
    CalibrationQuality AssessRotation(RotationCenterProfile profile);
    CalibrationQuality AssessPolynomial(PolynomialProfile profile);
}

internal sealed class CalibrationRuntime(CalibrationManager inner) : ICalibrationRuntime
{
    public double ExtrinsicResidualFair => CalibrationManager.ExtrinsicResidualFair;
    public double ScaleAnisotropyWarnLimit => CalibrationLimits.ScaleAnisotropyWarnLimit;

    public IReadOnlyList<IntrinsicProfile> IntrinsicProfiles => inner.IntrinsicProfiles;
    public IReadOnlyList<ExtrinsicProfile> ExtrinsicProfiles => inner.ExtrinsicProfiles;
    public IReadOnlyList<RotationCenterProfile> RotationCenterProfiles => inner.RotationCenterProfiles;
    public IReadOnlyList<PolynomialProfile> PolynomialProfiles => inner.PolynomialProfiles;
    public IReadOnlyList<ScaleProfile> ScaleProfiles => inner.ScaleProfiles;

    public ScaleProfile? GetScale(string? stationId) => inner.GetScale(stationId);
    public bool HasPolynomial(string? stationId) => inner.HasPolynomial(stationId);
    public bool HasExtrinsic(string? stationId) => inner.HasExtrinsic(stationId);
    public StationMappingMode GetMappingMode(string? stationId) => inner.GetMappingMode(stationId);
    public bool IsCalibrated(string cameraId) => inner.IsCalibrated(cameraId);
    public VisionImage Undistort(string cameraId, VisionImage src) => inner.Undistort(cameraId, src);

    public void RequireIntrinsic(string cameraId) => inner.RequireIntrinsic(cameraId);
    public void LoadIntrinsic(IntrinsicProfile profile) => inner.LoadIntrinsic(profile);
    public void LoadExtrinsic(ExtrinsicProfile profile) => inner.LoadExtrinsic(profile);
    public void LoadPolynomial(PolynomialProfile profile) => inner.LoadPolynomial(profile);
    public void LoadRotationCenter(RotationCenterProfile profile) => inner.LoadRotationCenter(profile);
    public void SaveScale(ScaleProfile profile) => inner.SaveScale(profile);

    public bool DeleteIntrinsic(string cameraId) => inner.DeleteIntrinsic(cameraId);
    public bool DeleteExtrinsic(string stationId) => inner.DeleteExtrinsic(stationId);
    public bool DeleteRotationCenter(string stationId) => inner.DeleteRotationCenter(stationId);
    public bool DeletePolynomial(string stationId) => inner.DeletePolynomial(stationId);
    public bool DeleteScale(string stationId) => inner.DeleteScale(stationId);

    public void VerifyRotationDirection(string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg) =>
        inner.VerifyRotationDirection(stationId, rc, points, anglesDeg);

    public (double OffsetDeg, double SpreadDeg) ComputeToolOffsetDeg(
        string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg) =>
        inner.ComputeToolOffsetDeg(stationId, rc, points, anglesDeg);

    public CalibrationQuality AssessIntrinsic(IntrinsicProfile profile) =>
        CalibrationManager.AssessIntrinsic(profile);

    public CalibrationQuality AssessExtrinsic(ExtrinsicProfile profile) =>
        CalibrationManager.AssessExtrinsic(profile);

    public CalibrationQuality AssessRotation(RotationCenterProfile profile) =>
        CalibrationManager.AssessRotation(profile);

    public CalibrationQuality AssessPolynomial(PolynomialProfile profile) =>
        CalibrationManager.AssessPolynomial(profile);
}
