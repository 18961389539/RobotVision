using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.Hosting;

internal sealed class CalibrationWizardService(
    ICameraRuntime cameras,
    ICalibrationRuntime calibration) : ICalibrationWizardService
{
    public int IntrinsicMinImageCount => ChessboardIntrinsicCalibrator.MinImageCount;

    public int IntrinsicRecommendedImageCount => ChessboardIntrinsicCalibrator.RecommendedImageCount;

    public CalibrationGrabResult GrabFrame(CalibrationGrabRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var grabbed = cameras.Grab(request.CameraId, ct);
        using var frame = VisionImageMat.AsMat(grabbed.Image);

        if (request.Mode is CalibrationWizardMode.Intrinsic or CalibrationWizardMode.Polynomial)
        {
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var pattern = new Size(request.Cols, request.Rows);
            var found = Cv2.FindChessboardCornersSB(gray, pattern, out var corners);

            using var preview = frame.Clone();
            if (found)
                Cv2.DrawChessboardCorners(preview, pattern, corners, true);

            if (request.Mode == CalibrationWizardMode.Intrinsic)
            {
                using var raw = frame.Clone();
                var info = found
                    ? $"棋盘检测成功（{request.Cols}×{request.Rows}），可加入采集"
                    : "未检测到棋盘：调整角度/对焦或核对内角点数";
                return new CalibrationGrabResult(
                    BgraImageBuffer.FromBgrMat(preview),
                    BgraImageBuffer.FromBgrMat(raw),
                    info,
                    found,
                    []);
            }

            var polyInfo = found
                ? request.PolynomialImageSpace
                    ? $"棋盘检测成功（{request.Cols}×{request.Rows}，{corners.Length} 角点）。棋盘毫米系免示教，直接点「计算」即可"
                    : $"棋盘检测成功（{request.Cols}×{request.Rows}，{corners.Length} 角点）。请在图上点选同一行的 2 个参考角点（自动吸附），并抄录其机器人坐标"
                : "未检测到棋盘：调整角度/对焦或核对内角点数（多项式标定不依赖内参，直接用原图）";
            var cornerPoints = found
                ? corners.Select(c => new CalibrationCornerPoint(c.X, c.Y)).ToArray()
                : Array.Empty<CalibrationCornerPoint>();
            return new CalibrationGrabResult(
                BgraImageBuffer.FromBgrMat(preview),
                null,
                polyInfo,
                found,
                cornerPoints);
        }

        if (request.Mode is CalibrationWizardMode.Rotation &&
            request.StationId.Length > 0 &&
            calibration.HasPolynomial(request.StationId))
        {
            using var preview = frame.Clone();
            return new CalibrationGrabResult(
                BgraImageBuffer.FromBgrMat(preview),
                null,
                $"新图已就绪（原图 {preview.Width}×{preview.Height}，多项式工位用原图坐标系，与推理一致）",
                false,
                []);
        }

        using var undistortedVision = calibration.Undistort(request.CameraId, grabbed.Image);
        using var undistortedMat = VisionImageMat.AsMat(undistortedVision);
        using var display = undistortedMat.Clone();
        var text = request.Mode == CalibrationWizardMode.Extrinsic
            ? $"新图已就绪（{display.Width}×{display.Height}），请依次点 9 个标定点"
            : $"新图已就绪（{display.Width}×{display.Height}），请点选本角度的标记点";
        return new CalibrationGrabResult(
            BgraImageBuffer.FromBgrMat(display),
            null,
            text,
            false,
            []);
    }

    public void SaveFramePng(BgraImageBuffer buffer, string path) => BgraImageBuffer.WritePng(buffer, path);

    public int NearestPolynomialCornerIndex(IReadOnlyList<CalibrationCornerPoint> corners, float clickX, float clickY)
    {
        var openCvCorners = corners.Select(c => new Point2f(c.X, c.Y)).ToArray();
        return PolynomialCalibrator.NearestCornerIndex(openCvCorners, new Point2f(clickX, clickY));
    }

    private static Point2f[] ToOpenCvCorners(CalibrationCornerPoint[] corners) =>
        corners.Select(c => new Point2f(c.X, c.Y)).ToArray();

    public CalibrationWizardComputeResult Compute(CalibrationComputeRequest request) =>
        request.Mode switch
        {
            CalibrationWizardMode.Intrinsic => new CalibrationWizardComputeResult
            {
                Intrinsic = ChessboardIntrinsicCalibrator.Calibrate(
                    request.CameraId, request.IntrinsicFramePaths,
                    new Size(request.Cols, request.Rows), request.SquareMm),
            },
            CalibrationWizardMode.Extrinsic => new CalibrationWizardComputeResult
            {
                Extrinsic = NinePointExtrinsicCalibrator.Calibrate(
                    request.StationId, request.CameraId,
                    [.. request.Points.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY))],
                    [.. request.Points.Select(p => new Point2f((float)p.RobotX, (float)p.RobotY))],
                    request.MappingImageWidth, request.MappingImageHeight),
            },
            CalibrationWizardMode.Polynomial when request.PolynomialImageSpace => new CalibrationWizardComputeResult
            {
                Polynomial = PolynomialCalibrator.CalibrateImageSpace(
                    request.StationId, request.CameraId,
                    ToOpenCvCorners(request.ChessboardCorners), new Size(request.Cols, request.Rows), request.SquareMm,
                    request.FrameWidth, request.FrameHeight, request.PolynomialOrder),
            },
            CalibrationWizardMode.Polynomial => new CalibrationWizardComputeResult
            {
                Polynomial = PolynomialCalibrator.Calibrate(
                    request.StationId, request.CameraId,
                    ToOpenCvCorners(request.ChessboardCorners), new Size(request.Cols, request.Rows), request.SquareMm,
                    new Point2f((float)request.Points[0].PixelX, (float)request.Points[0].PixelY),
                    new Point2f((float)request.Points[0].RobotX, (float)request.Points[0].RobotY),
                    new Point2f((float)request.Points[1].PixelX, (float)request.Points[1].PixelY),
                    new Point2f((float)request.Points[1].RobotX, (float)request.Points[1].RobotY),
                    request.FrameWidth, request.FrameHeight, request.PolynomialOrder),
            },
            CalibrationWizardMode.Rotation => new CalibrationWizardComputeResult
            {
                Rotation = RotationCenterCalibrator.Calibrate(
                    request.StationId, request.CameraId,
                    [.. request.Points.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY))],
                    request.MappingImageWidth, request.MappingImageHeight)
                    with { ToolOffsetDeg = request.ToolOffsetDeg },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(request.Mode)),
        };
}
