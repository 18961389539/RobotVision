using Microsoft.Extensions.Logging;

namespace RobotVision.Infrastructure.Cameras;

internal static partial class CameraManagerLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Camera gate drain timed out for {CameraId} during shutdown")]
    public static partial void GateDrainTimedOut(ILogger logger, string cameraId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Camera gate release raced with dispose during shutdown (expected, ignored)")]
    public static partial void GateReleaseRacedWithDispose(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camera {CameraId} release on shutdown failed")]
    public static partial void ShutdownReleaseFailed(ILogger logger, Exception ex, string cameraId);
}
