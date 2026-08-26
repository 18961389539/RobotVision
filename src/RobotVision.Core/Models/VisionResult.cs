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

    /// <summary>拍照位姿不一致：TRIGGER 上报的拍照位姿与 OnArm 外参档案记录的标定位姿
    /// 超出容差（appsettings PoseCheck 可配）。PLC 核对拍照点后重试，或重标该工位外参。</summary>
    PoseMismatch = 1012,

    /// <summary>TRIGGER 参数格式错误：段数不是 1（配方名）或 4（配方名,X,Y,RZ）、
    /// 或 X/Y/RZ 不是有限数字。</summary>
    InvalidTriggerArgument = 1013,

    /// <summary>OnArm 工位已记录示教位姿，但 TRIGGER 未上报 X,Y,RZ（v1 格式或界面空位姿）。
    /// 拒绝执行以免在未校验拍照点的情况下输出错位坐标。</summary>
    PoseRequired = 1014,

    /// <summary>配方存在但被停用（Enabled=false）。处置：在界面上启用后重试。
    /// 与 1001（名字错/文件缺）区分：停用是明确的运维动作，不应让 PLC 按"配方名错了"报警。</summary>
    RecipeDisabled = 1015,

    /// <summary>配方文件存在但参数/引用校验失败（cameraId 空、模型缺失、阈值越界等）。
    /// 处置：打开配方页修正配置。与 1001 区分：名字是对的，内容不合法。</summary>
    InvalidRecipeConfig = 1016,
}

/// <summary>
/// PLC 上报的拍照位姿（TRIGGER,配方名,X,Y,RZ 的后三段）。
/// OnArm（相机装在末端）外参档案仅在标定拍照位姿下有效——上位机上报实时位姿，
/// 视觉侧与档案比对（容差 appsettings PoseCheck），不一致拒绝执行（1012）。
/// </summary>
public sealed record TcpClientPose(double X, double Y, double RzDeg);

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
