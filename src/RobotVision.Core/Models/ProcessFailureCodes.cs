namespace RobotVision.Core.Models;

/// <summary>
/// 计入过程能力连续失败的错误码：取图/推理/未检出等现场过程问题。
/// 配置类错误（配方名、未标定、位姿、资产哈希、联锁本身）不计入，避免误锁。
/// </summary>
public static class ProcessFailureCodes
{
    public static bool CountsTowardStreak(VisionErrorCode code) => code is
        VisionErrorCode.CameraGrabFailed or
        VisionErrorCode.ModelNotAvailable or
        VisionErrorCode.NoTargetFound or
        VisionErrorCode.Timeout or
        VisionErrorCode.CameraInitFailed or
        VisionErrorCode.InternalError or
        VisionErrorCode.RefineFailed or
        VisionErrorCode.LightCommandFailed;
}
