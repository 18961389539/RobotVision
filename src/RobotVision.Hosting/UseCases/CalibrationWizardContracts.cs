using OpenCvSharp;
using RobotVision.Core.Models;

namespace RobotVision.Hosting;

public enum CalibrationWizardMode
{
    Intrinsic,
    Extrinsic,
    Rotation,
    Polynomial,
}

public sealed record CalibrationCornerPoint(float X, float Y);

public sealed record CalibrationGrabRequest(
    CalibrationWizardMode Mode,
    string CameraId,
    int Cols,
    int Rows,
    string StationId,
    bool PolynomialImageSpace);

public sealed record CalibrationGrabResult(
    BgraImageBuffer Display,
    BgraImageBuffer? Raw,
    string Info,
    bool ChessboardFound,
    IReadOnlyList<CalibrationCornerPoint> ChessboardCorners);

public sealed record CalibrationPointSnapshot(
    double PixelX,
    double PixelY,
    double RobotX,
    double RobotY,
    bool RobotEntered,
    double? RobotRzDeg);

public sealed record CalibrationComputeRequest(
    CalibrationWizardMode Mode,
    string CameraId,
    string StationId,
    int Cols,
    int Rows,
    double SquareMm,
    int PolynomialOrder,
    bool PolynomialImageSpace,
    double ToolOffsetDeg,
    IReadOnlyList<CalibrationPointSnapshot> Points,
    CalibrationCornerPoint[] ChessboardCorners,
    int FrameWidth,
    int FrameHeight,
    string[] IntrinsicFramePaths,
    int MappingImageWidth,
    int MappingImageHeight,
    bool RotationHasPolynomial);

public sealed class CalibrationWizardComputeResult
{
    public IntrinsicProfile? Intrinsic { get; init; }
    public ExtrinsicProfile? Extrinsic { get; init; }
    public PolynomialProfile? Polynomial { get; init; }
    public RotationCenterProfile? Rotation { get; init; }
}

public interface ICalibrationWizardService
{
    int IntrinsicMinImageCount { get; }
    int IntrinsicRecommendedImageCount { get; }

    CalibrationGrabResult GrabFrame(CalibrationGrabRequest request, CancellationToken ct = default);

    void SaveFramePng(BgraImageBuffer buffer, string path);

    int NearestPolynomialCornerIndex(IReadOnlyList<CalibrationCornerPoint> corners, float clickX, float clickY);

    CalibrationWizardComputeResult Compute(CalibrationComputeRequest request);
}
