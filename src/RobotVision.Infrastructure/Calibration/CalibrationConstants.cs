namespace RobotVision.Infrastructure.Calibration;

/// <summary>标定验收阈值与告警常量（README 对齐）。</summary>
internal static class CalibrationConstants
{
    public const double IntrinsicRmsGood = 0.3;
    public const double IntrinsicRmsFair = 0.5;
    public const double ExtrinsicResidualGood = 0.1;
    public const double ExtrinsicResidualFair = 0.5;
    public const double RotationRmsGood = 0.3;
    public const double RotationRmsFair = 0.5;
    public const double RotationAxisRatioLimit = 1.2;
    public const double LeaveOneOutWarnLimit = 1.0;
    public const double ScaleAnisotropyWarnLimit = 0.02;
    public const int MaxQualityWarnings = 50;
}
