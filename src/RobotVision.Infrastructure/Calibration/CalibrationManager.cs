using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>
/// 标定管理门面：组合内参服务、JSON 档案协调、映射编排、位姿校验与旋转补偿。
/// 对外 API 保持不变；实现已拆分到同目录子服务类。
/// </summary>
public sealed class CalibrationManager : IDisposable
{
    public const double IntrinsicRmsGood = CalibrationConstants.IntrinsicRmsGood;
    public const double IntrinsicRmsFair = CalibrationConstants.IntrinsicRmsFair;
    public const double ExtrinsicResidualGood = CalibrationConstants.ExtrinsicResidualGood;
    public const double ExtrinsicResidualFair = CalibrationConstants.ExtrinsicResidualFair;
    public const double RotationRmsGood = CalibrationConstants.RotationRmsGood;
    public const double RotationRmsFair = CalibrationConstants.RotationRmsFair;
    public const double RotationAxisRatioLimit = CalibrationConstants.RotationAxisRatioLimit;
    public const double LeaveOneOutWarnLimit = CalibrationConstants.LeaveOneOutWarnLimit;
    public const double ScaleAnisotropyWarnLimit = CalibrationConstants.ScaleAnisotropyWarnLimit;

    private readonly CalibrationStores _stores = new();
    private readonly JsonProfileCoordinator _profiles;
    private readonly StationMappingOrchestrator _mapping;
    private readonly ClientPoseValidator _pose;
    private readonly RotationCompensationService _rotation;

    public CalibrationManager()
    {
        _mapping = new StationMappingOrchestrator(_stores);
        _profiles = new JsonProfileCoordinator(_stores);
        _pose = new ClientPoseValidator(_stores);
        _rotation = new RotationCompensationService(_stores, _mapping);
    }

    public int IntrinsicCount => _stores.Intrinsics.Count;
    public int ExtrinsicCount => _stores.Extrinsics.Count;
    public int RotationCenterCount => _stores.RotationCenters.Count;
    public int PolynomialCount => _stores.Polynomials.Count;
    public int ScaleCount => _stores.Scales.Count;

    public IReadOnlyList<ScaleProfile> ScaleProfiles =>
        _stores.Scales.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    public ScaleProfile? GetScale(string? stationId) => _stores.Scales.Get(stationId);

    public IReadOnlyList<PolynomialProfile> PolynomialProfiles =>
        _stores.Polynomials.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    public bool HasPolynomial(string? stationId) => _stores.Polynomials.Contains(stationId);
    public bool HasExtrinsic(string? stationId) => _stores.Extrinsics.Contains(stationId);

    public StationMappingMode GetMappingMode(string? stationId) => _mapping.GetMappingMode(stationId);

    public string? ComputeStationSha256(string? stationId, bool includeRotation = false, string? undistortCameraId = null) =>
        _mapping.ComputeStationSha256(stationId, includeRotation, undistortCameraId);

    public IReadOnlyList<string> QualityWarnings => _stores.QualityWarnings.ToArray();

    public IReadOnlyList<IntrinsicProfile> IntrinsicProfiles => _stores.Intrinsics.Profiles;
    public IReadOnlyList<ExtrinsicProfile> ExtrinsicProfiles =>
        _stores.Extrinsics.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();
    public IReadOnlyList<RotationCenterProfile> RotationCenterProfiles =>
        _stores.RotationCenters.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<(string File, string Error)> LoadDirectory(string folder) =>
        CalibrationDirectoryLoader.Load(
            _stores, folder,
            LoadIntrinsic,
            LoadExtrinsic,
            LoadRotationCenter,
            LoadPolynomial,
            LoadScale);

    public void LoadExtrinsic(ExtrinsicProfile profile) => _profiles.LoadExtrinsic(profile);
    public void LoadRotationCenter(RotationCenterProfile profile) => _profiles.LoadRotationCenter(profile);
    public void LoadPolynomial(PolynomialProfile profile) => _profiles.LoadPolynomial(profile);
    public void LoadScale(ScaleProfile profile) => _profiles.LoadScale(profile);

    public void SaveExtrinsic(ExtrinsicProfile profile) => _profiles.SaveExtrinsic(profile);
    public void SaveRotationCenter(RotationCenterProfile profile) => _profiles.SaveRotationCenter(profile);
    public void SavePolynomial(PolynomialProfile profile) => _profiles.SavePolynomial(profile);
    public void SaveScale(ScaleProfile profile) => _profiles.SaveScale(profile);

    public bool DeleteExtrinsic(string stationId) => _profiles.DeleteExtrinsic(stationId);
    public bool DeleteRotationCenter(string stationId) => _profiles.DeleteRotationCenter(stationId);
    public bool DeletePolynomial(string stationId) => _profiles.DeletePolynomial(stationId);
    public bool DeleteScale(string stationId) => _profiles.DeleteScale(stationId);

    public static void ValidateExtrinsic(ExtrinsicProfile profile) => ExtrinsicKind.Instance.Validate(profile);
    public static void ValidateRotationCenter(RotationCenterProfile profile) => RotationCenterKind.Instance.Validate(profile);
    public static void ValidatePolynomial(PolynomialProfile profile) => PolynomialKind.Instance.Validate(profile);
    public static void ValidateScale(ScaleProfile profile) => ScaleKind.Instance.Validate(profile);

    public static CalibrationQuality AssessExtrinsic(ExtrinsicProfile p) => ExtrinsicKind.Instance.Assess(p);
    public static CalibrationQuality AssessRotation(RotationCenterProfile p) => RotationCenterKind.Instance.Assess(p);
    public static CalibrationQuality AssessPolynomial(PolynomialProfile p) => PolynomialKind.Instance.Assess(p);

    public void LoadIntrinsic(IntrinsicProfile profile) => _stores.Intrinsics.Load(profile);

    public void SaveIntrinsic(IntrinsicProfile profile)
    {
        _stores.RequireFolder();
        IntrinsicCalibrationService.Validate(profile);
        _stores.WriteJson(_stores.ProfileFile("intrinsic", profile.CameraId), profile);
        LoadIntrinsic(profile);
    }

    public bool DeleteIntrinsic(string cameraId)
    {
        _stores.Intrinsics.Delete(cameraId);
        return _stores.DeleteProfileFile(cameraId, "intrinsic");
    }

    public static void ValidateIntrinsic(IntrinsicProfile profile) => IntrinsicCalibrationService.Validate(profile);
    public static CalibrationQuality AssessIntrinsic(IntrinsicProfile p) => IntrinsicCalibrationService.Assess(p);

    public void RequireIntrinsic(string cameraId) => _stores.Intrinsics.RequireIntrinsic(cameraId);
    public bool IsCalibrated(string cameraId) => _stores.Intrinsics.IsCalibrated(cameraId);
    public VisionImage Undistort(string cameraId, VisionImage src) => _stores.Intrinsics.Undistort(cameraId, src);
    public Mat Undistort(string cameraId, Mat src) => _stores.Intrinsics.Undistort(cameraId, src);

    public void VerifyPolynomialResolution(string stationId, int width, int height) =>
        _mapping.VerifyPolynomialResolution(stationId, width, height);

    public void VerifyScaleResolution(string stationId, int width, int height) =>
        _mapping.VerifyScaleResolution(stationId, width, height);

    public void VerifyPolynomialClientPose(string? stationId, TcpClientPose? pose) =>
        _pose.VerifyPolynomialClientPose(stationId, pose);

    public RobotPose PixelToRobotPolynomial(string stationId, PixelPose pose, string? cameraId = null, TcpClientPose? clientPose = null) =>
        _mapping.PixelToRobotPolynomial(stationId, pose, cameraId, clientPose);

    public RobotPose PixelToRobotScale(string stationId, PixelPose pose, string? cameraId = null) =>
        _mapping.PixelToRobotScale(stationId, pose, cameraId);

    public Point2d RotationCenterRobot(string stationId) => _mapping.RotationCenterRobot(stationId);

    public RobotPose CompensateRotation(string? stationId, RotationCompensationMode mode, RobotPose pose) =>
        _rotation.CompensateRotation(stationId, mode, pose);

    public void VerifyRotationDirection(string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg) =>
        _rotation.VerifyRotationDirection(stationId, rc, points, anglesDeg);

    public bool PoseCheckEnabled
    {
        get => _pose.Enabled;
        set => _pose.Enabled = value;
    }

    public double PoseXyToleranceMm
    {
        get => _pose.XyToleranceMm;
        set => _pose.XyToleranceMm = value;
    }

    public double PoseRzToleranceDeg
    {
        get => _pose.RzToleranceDeg;
        set => _pose.RzToleranceDeg = value;
    }

    public bool ClientPoseRequired(string? stationId) => _pose.ClientPoseRequired(stationId);
    public void RequireClientPose(string? stationId, TcpClientPose? pose) => _pose.RequireClientPose(stationId, pose);
    public void VerifyClientPose(string? stationId, TcpClientPose pose) => _pose.VerifyClientPose(stationId, pose);

    public (double OffsetDeg, double SpreadDeg) ComputeToolOffsetDeg(
        string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg) =>
        _rotation.ComputeToolOffsetDeg(stationId, rc, points, anglesDeg);

    public RobotPose PixelToRobot(string? stationId, PixelPose pose, string? cameraId = null, TcpClientPose? clientPose = null) =>
        _mapping.PixelToRobot(stationId, pose, cameraId, clientPose);

    public void Dispose() => _stores.Dispose();
}
