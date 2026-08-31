namespace RobotVision.Infrastructure.Calibration;

internal static class CalibrationAngleMath
{
    /// <summary>归一化角度差到 (-180,180]（跨 ±180° 边界的相邻点不失真）。</summary>
    public static double NormalizeDelta(double delta)
    {
        var d = ((delta + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }
}
