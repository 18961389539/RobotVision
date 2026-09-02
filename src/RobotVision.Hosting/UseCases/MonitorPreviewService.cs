using RobotVision.Core.Recipe;

using RobotVision.Core.Models;

namespace RobotVision.Hosting;

internal sealed class MonitorPreviewService(
    ICameraRuntime cameras,
    ICalibrationRuntime calibration,
    RecipeLoader recipes) : IMonitorPreviewService
{
    public BgraImageBuffer GrabDisplayFrame(string cameraId, string? recipeName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var frame = cameras.Grab(cameraId, ct);
        try
        {
            string? stationId = null;
            if (!string.IsNullOrEmpty(recipeName))
            {
                try { stationId = recipes.Get(recipeName).StationId; }
                catch { /* 预览不因配方无效失败 */ }
            }

            if (!string.IsNullOrEmpty(stationId) && calibration.HasPolynomial(stationId))
                return ExportClone(frame.Image);
            if (calibration.IsCalibrated(cameraId))
            {
                using var undistorted = calibration.Undistort(cameraId, frame.Image);
                return ExportClone(undistorted);
            }

            return ExportClone(frame.Image);
        }
        finally
        {
            frame.Dispose();
        }
    }

    private static BgraImageBuffer ExportClone(VisionImage image)
    {
        using var mat = VisionImageMat.AsMat(image);
        using var clone = mat.Clone();
        return BgraImageBuffer.FromBgrMat(clone);
    }
}
