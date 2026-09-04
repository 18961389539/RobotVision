using System.Globalization;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting.Chat;

/// <summary>把视觉台现有管理器暴露给对话工具。查询尽量全；动作与产线共用相机锁/检测队列。</summary>
public sealed partial class StationChatTools
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppConfig _cfg;
    private readonly CameraManager _cameras;
    private readonly CameraConfigStore _cameraStore;
    private readonly LightingManager _lights;
    private readonly RecipeLoader _recipes;
    private readonly ModelManager _models;
    private readonly CalibrationManager _calib;
    private readonly VisionService _vision;
    private readonly SqliteResultStore _sqlite;
    private readonly FailureImageStore _failures;
    private readonly SuccessCaptureStore _captures;
    private readonly TcpServerManager _tcp;
    private readonly AppSettingsStore _settings;
    private readonly AssetIntegrityChecker _assets;
    private readonly IChatLogSource? _logs;

    public StationChatTools(
        AppConfig cfg,
        CameraManager cameras,
        CameraConfigStore cameraStore,
        LightingManager lights,
        RecipeLoader recipes,
        ModelManager models,
        CalibrationManager calib,
        VisionService vision,
        SqliteResultStore sqlite,
        FailureImageStore failures,
        SuccessCaptureStore captures,
        TcpServerManager tcp,
        AppSettingsStore settings,
        AssetIntegrityChecker assets,
        IServiceProvider services)
    {
        _cfg = cfg;
        _cameras = cameras;
        _cameraStore = cameraStore;
        _lights = lights;
        _recipes = recipes;
        _models = models;
        _calib = calib;
        _vision = vision;
        _sqlite = sqlite;
        _failures = failures;
        _captures = captures;
        _tcp = tcp;
        _settings = settings;
        _assets = assets;
        _logs = services.GetService(typeof(IChatLogSource)) as IChatLogSource;
    }

    public IReadOnlyList<IChatTool> Tools =>
    [
        Tool("station_overview", "本机总览：相机、光源、配方、TCP、检测队列、模型。问有几台相机/现在忙不忙时用。", Empty(), Overview),
        Tool("get_camera", "单个相机详情（类型、曝光、是否可取图）。", Props("camera_id"), GetCamera),
        Tool("capture_frame", "拍一张并保存 PNG。与产线 TRIGGER 共用相机锁。须 confirm:true，且用户点名相机或说拍照。camera_id 可空（仅一台时默认）。", Props("camera_id", "confirm"), Capture),
        Tool("get_recipe", "读取配方摘要（相机、工位、模型、角度模式）。", Props("name", required: true), GetRecipe),
        Tool("run_recipe", "按配方跑一次检测（与 PLC 同一条队列）。须 confirm:true，且用户点名配方。name 为配方名或序列号。", Props(["name", "confirm"], required: true), RunRecipe),
        Tool("query_results", "本机结果库（与分析页同一套查询）。action=dashboard|rows|summary|angles|codes|by_recipe|trend|recipes|info。range=today|7d|30d|all；可筛 recipe/station/camera/code/ok_only/message、from/to、days、hours、grain=hour|day、bins、limit、offset。分析/合格率/分布用 dashboard。", Props("action", "range", "recipe", "station", "camera", "code", "ok_only", "message", "from", "to", "days", "hours", "grain", "bins", "limit", "offset"), QueryResults),
        Tool("list_failures", "列出失败留存图文件。", Props("limit"), ListFailures),
        Tool("list_calibrations", "列出内参/外参/多项式/比例/旋转中心档案。", Empty(), ListCalib),
        Tool("set_light", "开关光源或发原始指令。action=on|off|raw。全部须 confirm:true。", Props("id", "action", "channel", "brightness", "command", "confirm"), SetLight),
        Tool("clear_inhibit", "解除过程联锁(1018)。须 confirm:true 且用户明确同意。可选 recipe。", Props("recipe", "confirm"), ClearInhibit),
        Tool("tcp_control", "PLC 通信。action=status|stop|start|restart|disconnect；stop/restart/disconnect 须 confirm:true。", Props("action", "client_id", "confirm"), TcpControl),
        Tool("get_logs", "最近界面日志。", Props("limit"), GetLogs),
        Tool("get_settings", "超时、队列、TCP 端口、位姿校验等运行设置。", Empty(), GetSettings),
        Tool("manage_recipe", "配方写操作。写操作须 confirm:true。action=enable|disable|delete|duplicate|validate|patch。", Props("action", "name", "new_name", "confidence", "iou", "camera_id", "description", "serial", "enabled", "confirm"), ManageRecipe),
        Tool("set_camera", "改相机曝光/增益并写配置。unregister 须 confirm:true。", Props("camera_id", "exposure_us", "gain", "grab_timeout_ms", "action", "confirm"), SetCamera),
        Tool("update_settings", "改运行参数并热应用。任何字段变更须 confirm:true。", Props("timeout_ms", "max_queue", "tcp_port", "ip", "whitelist", "pose_check", "xy_tol", "rz_tol", "idle_timeout_ms", "max_concurrent", "tcp_backlog", "max_connections", "failure_enabled", "failure_retained", "process_health", "consecutive_fail_limit", "inhibit_on_limit", "confirm"), UpdateSettings),
        Tool("manage_model", "模型。unload/unload_all 须 confirm:true。", Props("action", "file", "task", "confirm"), ManageModel),
        Tool("manage_calibration", "标定档案。delete 须 confirm:true。", Props("action", "kind", "id", "confirm"), ManageCalibration),
        Tool("manage_files", "失败图/留存图/对话拍照。删除须 confirm:true。", Props("action", "path", "limit", "confirm"), ManageFiles),
        Tool("convert_pose", "像素坐标换机器人坐标。需 station_id、px、py，可选 angle_deg、camera_id。", Props("station_id", "px", "py", "angle_deg", "camera_id"), ConvertPose),
        Tool("system_info", "内存、本机时间、目录、推理后端、各配方统计。问今天几号/现在几点用这个。", Empty(), SystemInfo),
        Tool("list_files", "列目录。folder=recipes|models|calibration|failures|captures|chat|results。", Props("folder"), ListFiles),
        Tool("light_send_raw", "向光源控制器发原始协议字符串。须 confirm:true。", Props("id", "command", "confirm"), LightSendRaw),
    ];

}
