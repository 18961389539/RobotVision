namespace RobotVision.Core.Models;

public enum VisionErrorCode
{
    None = 0,
    UnknownCommand = 1000,
    UnknownRecipe = 1001,
    CameraNotRegistered = 1002,
    CameraGrabFailed = 1003,
    NotCalibrated = 1004,
    ModelNotAvailable = 1005,
    LightNotRegistered = 1006,
    NoTargetFound = 1007,
    Timeout = 1008,
    Busy = 1009,
    InternalError = 1099,

    /// <summary>排队阶段超时（未进入推理，请求放弃排队）。区别于 1008 处理超时。</summary>
    QueueTimeout = 1010,

    /// <summary>相机初始化失败（pylon 运行库缺失/设备打开失败）。取图前失败，无现场图可留。</summary>
    CameraInitFailed = 1011,
}

/// <summary>一次 TRIGGER 请求的完整结果。</summary>
public sealed record VisionResult
{
    public bool Ok { get; init; }

    public string RecipeName { get; init; } = "";

    public IReadOnlyList<RobotPose> Poses { get; init; } = [];

    /// <summary>每个目标的置信度（与 Poses 一一对应，来自检测管线 PixelPose.Score）。</summary>
    public IReadOnlyList<double> Confidences { get; init; } = [];

    public VisionErrorCode ErrorCode { get; init; }

    public string Message { get; init; } = "";

    public double ElapsedMs { get; init; }

    public static VisionResult Success(
        string recipe,
        IReadOnlyList<RobotPose> poses,
        double elapsedMs,
        IReadOnlyList<double>? confidences = null) => new()
    {
        Ok = true,
        RecipeName = recipe,
        Poses = poses,
        Confidences = confidences ?? [],
        ElapsedMs = elapsedMs,
    };

    public static VisionResult Fail(string recipe, VisionErrorCode code, string message, double elapsedMs) => new()
    {
        Ok = false,
        RecipeName = recipe,
        ErrorCode = code,
        Message = message,
        ElapsedMs = elapsedMs,
    };
}
