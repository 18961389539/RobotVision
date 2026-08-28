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
public sealed class StationChatTools
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
        Tool("capture_frame", "拍一张并保存 PNG。与产线 TRIGGER 共用相机锁，可能增加节拍延迟。camera_id 可空（仅一台时默认）。", Props("camera_id"), Capture),
        Tool("get_recipe", "读取配方摘要（相机、工位、模型、角度模式）。", Props("name", required: true), GetRecipe),
        Tool("run_recipe", "按配方跑一次检测（与 PLC 同一条队列）。name 为配方名或序列号。", Props("name", required: true), RunRecipe),
        Tool("query_results", "本机结果库（与分析页同一套查询）。action=dashboard|rows|summary|angles|codes|by_recipe|trend|recipes|info。range=today|7d|30d|all；可筛 recipe/station/camera/code/ok_only/message、from/to、days、hours、grain=hour|day、bins、limit、offset。分析/合格率/分布用 dashboard。", Props("action", "range", "recipe", "station", "camera", "code", "ok_only", "message", "from", "to", "days", "hours", "grain", "bins", "limit", "offset"), QueryResults),
        Tool("list_failures", "列出失败留存图文件。", Props("limit"), ListFailures),
        Tool("list_calibrations", "列出内参/外参/多项式/比例/旋转中心档案。", Empty(), ListCalib),
        Tool("set_light", "开关光源或发原始指令。action=on|off|raw；on 用 channel/brightness；raw 用 command。", Props("id", "action", "channel", "brightness", "command"), SetLight),
        Tool("clear_inhibit", "解除过程联锁(1018)。可选 recipe，空则全部解除。", Props("recipe"), ClearInhibit),
        Tool("tcp_control", "PLC 通信。action=status|stop|start|restart|disconnect；disconnect 需 client_id。停/重启会断开机器人。", Props("action", "client_id"), TcpControl),
        Tool("get_logs", "最近界面日志。", Props("limit"), GetLogs),
        Tool("get_settings", "超时、队列、TCP 端口、位姿校验等运行设置。", Empty(), GetSettings),
        Tool("manage_recipe", "配方写操作。action=enable|disable|delete|duplicate|validate|patch。patch 可改 enabled/confidence/iou/camera_id/description/serial。duplicate 需 new_name。", Props("action", "name", "new_name", "confidence", "iou", "camera_id", "description", "serial", "enabled"), ManageRecipe),
        Tool("set_camera", "改相机曝光/增益并写配置，也可立刻下发到已打开的相机。可 unregister。", Props("camera_id", "exposure_us", "gain", "grab_timeout_ms", "action"), SetCamera),
        Tool("update_settings", "改运行参数并热应用。可填 timeout_ms/max_queue/tcp_port/ip/whitelist/pose_check/xy_tol/rz_tol/idle_timeout_ms/failure_enabled/process_health 等。", Props("timeout_ms", "max_queue", "tcp_port", "ip", "whitelist", "pose_check", "xy_tol", "rz_tol", "idle_timeout_ms", "max_concurrent", "tcp_backlog", "max_connections", "failure_enabled", "failure_retained", "process_health", "consecutive_fail_limit", "inhibit_on_limit"), UpdateSettings),
        Tool("manage_model", "模型。action=list|unload|unload_all；unload 需 file，可选 task=ObjectDetection|Segmentation|PoseEstimation。", Props("action", "file", "task"), ManageModel),
        Tool("manage_calibration", "标定档案。action=delete 时 kind=intrinsic|extrinsic|polynomial|scale|rotation，id 为相机或工位。", Props("action", "kind", "id"), ManageCalibration),
        Tool("manage_files", "失败图/留存图/对话拍照。action=list_captures|delete_failure|delete_capture|delete_chat；删除需 path。", Props("action", "path", "limit"), ManageFiles),
        Tool("convert_pose", "像素坐标换机器人坐标。需 station_id、px、py，可选 angle_deg、camera_id。", Props("station_id", "px", "py", "angle_deg", "camera_id"), ConvertPose),
        Tool("system_info", "内存、本机时间、目录、推理后端、各配方统计。问今天几号/现在几点用这个。", Empty(), SystemInfo),
        Tool("list_files", "列目录。folder=recipes|models|calibration|failures|captures|chat|results。", Props("folder"), ListFiles),
        Tool("light_send_raw", "向光源控制器发原始协议字符串。", Props("id", "command"), LightSendRaw),
    ];

    private Task<ChatToolResult> Overview(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var cameras = _cameras.CameraIds.Select(id =>
        {
            _cameras.TryGet(id, out var cam);
            var cfg = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            return new
            {
                id,
                kind = cam?.Kind.ToString(),
                type = cfg?.Type,
                name = string.IsNullOrWhiteSpace(cfg?.Name) ? id : cfg!.Name,
                grabHint = _cameras.GetGrabErrorHint(id),
            };
        }).ToList();
        var health = _vision.Health;
        return Task.FromResult(Ok(new
        {
            ok = true,
            cameraCount = cameras.Count,
            configured = _cfg.Cameras.Select(c => new { c.Id, c.Type, name = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name }).ToList(),
            cameras,
            lights = _lights.ControllerIds.ToArray(),
            recipes = _recipes.ListNames(),
            tcp = new
            {
                running = _tcp.IsRunning,
                endpoint = _tcp.ListenEndPoint,
                port = _tcp.Port,
                clients = _tcp.ConnectedClients,
                totalRequests = _tcp.TotalRequests,
            },
            vision = new
            {
                queue = _vision.QueueDepth,
                processing = _vision.IsProcessing,
                lastMs = _vision.LastElapsedMs,
                maxQueue = _vision.MaxQueueDepth,
                health.Total,
                health.Failed,
                health.TimedOut,
                health.AvgMs,
                inhibited = _vision.AnyInhibited,
            },
            models = new { files = _models.ModelFileNames, loaded = _models.LoadedCount },
        }));
    }

    private Task<ChatToolResult> GetCamera(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "camera_id");
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("请提供 camera_id"));
        var cfg = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (!_cameras.TryGet(id, out var cam) || cam is null)
        {
            if (cfg is null)
                return Task.FromResult(Fail($"相机未注册: {id}"));
            return Task.FromResult(Ok(new
            {
                ok = true,
                id,
                registered = false,
                type = cfg.Type,
                name = cfg.Name,
                deviceId = cfg.DeviceId,
                folder = cfg.Folder,
                exposureUs = cfg.ExposureTimeUs,
                gain = cfg.Gain,
                grabTimeoutMs = cfg.GrabTimeoutMs,
            }));
        }
        var live = cam as IExposureControl;
        return Task.FromResult(Ok(new
        {
            ok = true,
            id,
            registered = true,
            kind = cam.Kind.ToString(),
            type = cfg?.Type,
            name = cfg?.Name,
            deviceId = cfg?.DeviceId,
            folder = cfg?.Folder,
            exposureUs = cfg?.ExposureTimeUs,
            gain = cfg?.Gain,
            liveExposureUs = live?.GetExposureTimeUs(),
            liveGain = live?.GetGain(),
            exposureRange = live?.GetExposureRange() is { } er ? new { er.Min, er.Max } : null,
            gainRange = live?.GetGainRange() is { } gr ? new { gr.Min, gr.Max } : null,
            grabTimeoutMs = cfg?.GrabTimeoutMs,
            grabHint = _cameras.GetGrabErrorHint(id),
            failed = cam is FailedCamera,
        }));
    }

    private async Task<ChatToolResult> Capture(string args, CancellationToken ct)
    {
        using var doc = Parse(args);
        var id = Str(doc, "camera_id");
        if (string.IsNullOrWhiteSpace(id))
        {
            var ids = _cameras.CameraIds.ToList();
            if (ids.Count == 1)
                id = ids[0];
            else if (ids.Count == 0)
                return Fail("没有已注册相机");
            else
                return Fail("有多台相机，请指定 camera_id: " + string.Join(", ", ids));
        }

        var hint = _cameras.GetGrabErrorHint(id);
        if (hint is not null)
            return Fail(hint);

        using var frame = await _cameras.GrabAsync(id, ct).ConfigureAwait(false);
        using var mat = VisionImageCv.AsMat(frame.Image);
        var folder = AppConfigExtensions.ResolveFolder("data/chat-captures");
        Directory.CreateDirectory(folder);
        var safeId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(folder, $"chat_{DateTime.Now:yyyyMMdd_HHmmss}_{safeId}.png");
        if (!Cv2.ImWrite(path, mat))
            return Fail("写 PNG 失败: " + path);
        return Ok(new
        {
            ok = true,
            path,
            cameraId = id,
            width = mat.Width,
            height = mat.Height,
            capturedAtUtc = frame.CapturedAtUtc,
            acquireMs = frame.AcquireMs,
        }, path);
    }

    private Task<ChatToolResult> GetRecipe(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var name = Str(doc, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(Fail("请提供配方 name"));
        try
        {
            var r = _recipes.Get(name);
            return Task.FromResult(Ok(new
            {
                ok = true,
                r.Name,
                r.Enabled,
                r.SerialNumber,
                r.Description,
                r.CameraId,
                r.StationId,
                angleMode = r.AngleMode.ToString(),
                r.Models,
                r.Confidence,
                r.Iou,
                r.LightControllerId,
                mapping = r.StationId is null ? "none" : _calib.GetMappingMode(r.StationId).ToString(),
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private async Task<ChatToolResult> RunRecipe(string args, CancellationToken ct)
    {
        using var doc = Parse(args);
        var name = Str(doc, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Fail("请提供配方 name");
        var (resolved, resolveErr) = _recipes.ResolveTriggerKey(name);
        if (resolved is null)
            return Fail(resolveErr ?? "未知配方");
        var result = await _vision.RunAsync(resolved, ct).ConfigureAwait(false);
        return Ok(new
        {
            ok = result.Ok,
            recipe = result.RecipeName,
            code = (int)result.ErrorCode,
            error = result.ErrorCode.ToString(),
            result.Message,
            result.ElapsedMs,
            count = result.Poses.Count,
            poses = result.Poses.Select((p, i) => new
            {
                i,
                p.X,
                p.Y,
                p.AngleDeg,
                score = i < result.Confidences.Count ? result.Confidences[i] : (double?)null,
            }),
        });
    }

    private Task<ChatToolResult> QueryResults(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "" or "page" or "analysis")
            action = "dashboard";
        if (Has(doc, "from") && ParseWhen(doc, "from") is null)
            return Task.FromResult(Fail("from 无法解析，请用 ISO 时间、today 或 -7d"));
        if (Has(doc, "to") && ParseWhen(doc, "to") is null)
            return Task.FromResult(Fail("to 无法解析，请用 ISO 时间、today 或 now"));

        var query = BuildResultQuery(doc, defaultLimit: action is "dashboard" ? 12 : 20, maxLimit: 80);
        if (action is "dashboard" or "angles" or "trend" or "by_recipe")
            query = WithAnalysisDefaultRange(doc, query);
        var grain = Str(doc, "grain").ToLowerInvariant();
        if (grain is not ("hour" or "day"))
            grain = LooksLikeToday(query) ? "hour" : "day";
        var bins = Math.Clamp(Int(doc, "bins", 12), 4, 24);
        try
        {
            switch (action)
            {
                case "info":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        path = _sqlite.DatabasePath,
                        folder = _sqlite.Folder,
                        writeEnabled = _sqlite.Enabled,
                        retainedDays = _sqlite.RetainedDays,
                        total = _sqlite.Count(),
                    }));
                case "recipes":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        recipes = _sqlite.ListRecipes(),
                        stations = _sqlite.ListStations(),
                        cameras = _sqlite.ListCameras(),
                        total = _sqlite.Count(),
                    }));
                case "codes":
                {
                    var codes = _sqlite.CountByCode(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        matched = _sqlite.Count(query),
                        codes = codes.Select(c => new
                        {
                            c.Code,
                            label = ResultAnalysis.DescribeCode(c.Code),
                            c.Count,
                        }),
                    }));
                }
                case "summary":
                {
                    var summary = _sqlite.Summarize(query);
                    var spread = _sqlite.QuerySpread(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        matched = _sqlite.Count(query),
                        summary,
                        spread,
                        yield = YieldPct(summary),
                    }));
                }
                case "angles":
                {
                    var angles = _sqlite.QueryAngles(query);
                    var spread = _sqlite.QuerySpread(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        n = angles.Count,
                        spread = new { spread.MinAngle, spread.MaxAngle, spread.StdAngle, spread.AvgConfidence },
                        histogram = ResultAnalysis.BuildHistogram(angles, bins),
                    }));
                }
                case "by_recipe":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        matched = _sqlite.Count(query),
                        recipes = _sqlite.SummarizeByRecipe(query).Select(MapRecipeStat),
                    }));
                case "trend":
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        grain,
                        buckets = _sqlite.QueryTrend(query, grain),
                    }));
                case "dashboard":
                {
                    var summary = _sqlite.Summarize(query);
                    var spread = _sqlite.QuerySpread(query);
                    var angles = _sqlite.QueryAngles(query);
                    var codes = _sqlite.CountByCode(query);
                    var rows = _sqlite.Query(query);
                    var okQuery = query with { OkOnly = true, Code = null };
                    var okAngles = _sqlite.QueryAngles(okQuery);
                    var okSpread = _sqlite.QuerySpread(okQuery);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        path = _sqlite.DatabasePath,
                        matched = summary.Total,
                        yield = YieldPct(summary),
                        summary,
                        spread,
                        hints = RecipeHealthAdvisor.Analyze(
                            summary.Total, codes, okAngles, okSpread, TeachPeak(query.Recipe))
                            .Select(h => new { h.Id, h.Severity, h.Message }),
                        histogram = ResultAnalysis.BuildHistogram(angles, bins),
                        codes = codes.Select(c => new
                        {
                            c.Code,
                            label = ResultAnalysis.DescribeCode(c.Code),
                            c.Count,
                        }),
                        byRecipe = _sqlite.SummarizeByRecipe(query).Select(MapRecipeStat),
                        trend = _sqlite.QueryTrend(query, grain),
                        count = rows.Count,
                        rows = rows.Select(MapResultRow),
                    }));
                }
                case "rows":
                {
                    var rows = _sqlite.Query(query);
                    var summary = _sqlite.Summarize(query);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        path = _sqlite.DatabasePath,
                        matched = _sqlite.Count(query),
                        summary,
                        yield = YieldPct(summary),
                        count = rows.Count,
                        offset = query.Offset,
                        rows = rows.Select(MapResultRow),
                    }));
                }
                default:
                    return Task.FromResult(Fail("action 必须是 dashboard|rows|summary|angles|codes|by_recipe|trend|recipes|info"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private static double? YieldPct(ResultDbSummary summary) =>
        summary.Total == 0 ? null : Math.Round(100.0 * summary.Ok / summary.Total, 2);

    private double TeachPeak(string? recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName) || !RecipeLoader.IsValidRecipeName(recipeName))
            return 0;
        try
        {
            return _recipes.Get(recipeName).Template.TeachPeakScore;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static object MapRecipeStat(ResultRecipeStat s) => new
    {
        s.Recipe,
        s.Total,
        s.Ok,
        s.Failed,
        yield = s.Total == 0 ? (double?)null : Math.Round(100.0 * s.Ok / s.Total, 2),
        s.AvgMs,
        s.AvgAngle,
    };

    private static object MapResultRow(ResultDbRow r) => new
    {
        r.Id,
        r.T,
        r.Recipe,
        r.Station,
        r.Camera,
        r.X,
        r.Y,
        r.Angle,
        r.Confidence,
        r.Count,
        r.ElapsedMs,
        r.Code,
        codeLabel = ResultAnalysis.DescribeCode(r.Code),
        r.Message,
    };

    private Task<ChatToolResult> ListFailures(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var limit = Math.Clamp(Int(doc, "limit", 15), 1, 50);
        if (!Directory.Exists(_failures.Folder))
            return Task.FromResult(Ok(new { ok = true, count = 0, files = Array.Empty<string>(), folder = _failures.Folder }));
        var files = Directory.EnumerateFiles(_failures.Folder, "*.png", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTime)
            .Take(limit)
            .Select(p => new { path = p, name = Path.GetFileName(p), written = File.GetLastWriteTime(p) })
            .ToList();
        return Task.FromResult(Ok(new { ok = true, count = files.Count, folder = _failures.Folder, files }));
    }

    private Task<ChatToolResult> ListCalib(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Ok(new
        {
            ok = true,
            intrinsic = _calib.IntrinsicProfiles.Select(p => new { p.CameraId, p.Width, p.Height, p.Rms, p.CalibratedAt }),
            extrinsic = _calib.ExtrinsicProfiles.Select(p => new { p.StationId, p.CameraId, p.MountType, p.Rms, p.CalibratedAt }),
            polynomial = _calib.PolynomialProfiles.Select(p => new { p.StationId, p.CameraId, p.Width, p.Height, p.CalibratedAt }),
            scale = _calib.ScaleProfiles.Select(p => new { p.StationId, p.CameraId, p.ScaleX, p.ScaleY }),
            rotation = _calib.RotationCenterProfiles.Select(p => new { p.StationId, p.CameraId, p.Cx, p.Cy, p.Rms }),
            warnings = _calib.QualityWarnings,
        }));
    }

    private Task<ChatToolResult> SetLight(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "id");
        var action = Str(doc, "action").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("请提供光源 id"));
        if (action is "raw")
        {
            var command = Str(doc, "command");
            if (string.IsNullOrWhiteSpace(command))
                return Task.FromResult(Fail("raw 需要 command"));
            _lights.Get(id).SendRaw(command);
            return Task.FromResult(Ok(new { ok = true, id, action, command }));
        }
        if (action is not ("on" or "off"))
            return Task.FromResult(Fail("action 必须是 on、off 或 raw"));
        if (action == "off")
        {
            _lights.TurnOff(id);
            return Task.FromResult(Ok(new { ok = true, id, action }));
        }
        var channel = Math.Max(1, Int(doc, "channel", 1));
        var brightness = Math.Clamp(Int(doc, "brightness", 128), 0, 255);
        _lights.TurnOn(id, channel, brightness);
        return Task.FromResult(Ok(new { ok = true, id, action, channel, brightness }));
    }

    private Task<ChatToolResult> ClearInhibit(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var recipe = EmptyToNull(Str(doc, "recipe"));
        _vision.ClearInhibit(recipe);
        return Task.FromResult(Ok(new { ok = true, recipe, inhibited = _vision.AnyInhibited }));
    }

    private Task<ChatToolResult> TcpControl(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action) || action == "status")
        {
            return Task.FromResult(Ok(new
            {
                ok = true,
                running = _tcp.IsRunning,
                endpoint = _tcp.ListenEndPoint,
                port = _tcp.Port,
                clients = _tcp.GetClients().Select(c => new { c.Id, c.Remote, c.Requests, c.LastRequest }),
                recent = _tcp.GetRecentRequests().TakeLast(8).Select(r => new { r.Time, r.Request, r.Reply, r.Ok, r.ElapsedMs }),
                _tcp.TotalRequests,
                _tcp.RejectedConnections,
            }));
        }
        switch (action)
        {
            case "stop":
                _tcp.Stop();
                return Task.FromResult(Ok(new { ok = true, action, running = _tcp.IsRunning }));
            case "start":
                _tcp.Start();
                return Task.FromResult(Ok(new { ok = true, action, running = _tcp.IsRunning, endpoint = _tcp.ListenEndPoint }));
            case "restart":
                var ok = _tcp.Restart(_cfg.IpAddress, _cfg.TcpPort);
                return Task.FromResult(Ok(new { ok, action, running = _tcp.IsRunning, endpoint = _tcp.ListenEndPoint }));
            case "disconnect":
                if (!doc.RootElement.TryGetProperty("client_id", out var idNode) || !idNode.TryGetInt64(out var cid))
                    return Task.FromResult(Fail("disconnect 需要 client_id"));
                return Task.FromResult(Ok(new { ok = _tcp.DisconnectClient(cid), action, clientId = cid }));
            default:
                return Task.FromResult(Fail("action 必须是 status|stop|start|restart|disconnect"));
        }
    }

    private Task<ChatToolResult> GetLogs(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_logs is null)
            return Task.FromResult(Fail("日志源未接入（非 WPF 宿主）"));
        using var doc = Parse(args);
        var limit = Math.Clamp(Int(doc, "limit", 30), 1, 100);
        return Task.FromResult(Ok(new { ok = true, lines = _logs.Recent(limit) }));
    }

    private Task<ChatToolResult> GetSettings(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Ok(new
        {
            ok = true,
            _cfg.IpAddress,
            _cfg.TcpPort,
            _cfg.TimeoutMs,
            _cfg.IdleTimeoutMs,
            _cfg.MaxQueueDepth,
            _cfg.MaxConcurrent,
            _cfg.TcpBacklog,
            _cfg.MaxConnections,
            whitelist = _cfg.IpWhitelist,
            poseCheck = _cfg.PoseCheck,
            processHealth = _cfg.ProcessHealth,
            failureImage = _cfg.FailureImage,
            captureSuccess = _cfg.CaptureSuccess,
            inference = _cfg.Inference,
            chat = new { _cfg.Chat.Endpoint, _cfg.Chat.Model, _cfg.Chat.MaxTokens, _cfg.Chat.ContextSize },
        }));
    }

    private Task<ChatToolResult> ManageRecipe(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        var name = Str(doc, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(Fail("请提供配方 name"));
        var (resolved, resolveErr) = _recipes.ResolveTriggerKey(name);
        if (resolved is null)
            return Task.FromResult(Fail(resolveErr ?? "未知配方"));
        name = resolved;
        try
        {
            switch (action)
            {
                case "enable":
                case "disable":
                {
                    var r = _recipes.Get(name);
                    r.Enabled = action == "enable";
                    _recipes.Save(r);
                    return Task.FromResult(Ok(new { ok = true, action, r.Name, r.Enabled }));
                }
                case "delete":
                    return Task.FromResult(Ok(new { ok = _recipes.Delete(name), action, name }));
                case "duplicate":
                {
                    var newName = Str(doc, "new_name");
                    if (string.IsNullOrWhiteSpace(newName))
                        return Task.FromResult(Fail("duplicate 需要 new_name"));
                    var copy = _recipes.Get(name).Clone();
                    copy.Name = newName;
                    copy.SerialNumber = 0;
                    _recipes.Save(copy);
                    return Task.FromResult(Ok(new { ok = true, action, from = name, copy.Name }));
                }
                case "validate":
                {
                    var r = _recipes.Get(name);
                    RecipeLoader.Validate(r);
                    _recipes.ValidateReferences(r);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        r.Name,
                        asset = _assets.Check(r) ?? "ok",
                        integrityEnabled = _assets.IsEnabled,
                    }));
                }
                case "patch":
                {
                    var r = _recipes.Get(name);
                    if (MaybeBool(doc, "enabled") is { } en)
                        r.Enabled = en;
                    if (Has(doc, "confidence"))
                        r.Confidence = Dbl(doc, "confidence", r.Confidence);
                    if (Has(doc, "iou"))
                        r.Iou = Dbl(doc, "iou", r.Iou);
                    if (Has(doc, "camera_id"))
                        r.CameraId = Str(doc, "camera_id");
                    if (Has(doc, "description"))
                        r.Description = Str(doc, "description");
                    if (Has(doc, "serial"))
                        r.SerialNumber = Int(doc, "serial", r.SerialNumber);
                    _recipes.Save(r);
                    return Task.FromResult(Ok(new { ok = true, action, r.Name, r.Enabled, r.Confidence, r.Iou, r.CameraId, r.SerialNumber }));
                }
                default:
                    return Task.FromResult(Fail("action 必须是 enable|disable|delete|duplicate|validate|patch"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private Task<ChatToolResult> SetCamera(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "camera_id");
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("请提供 camera_id"));
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "unregister")
        {
            var removed = _cameras.Unregister(id);
            var list = _cfg.Cameras.Where(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
            _cameraStore.Save(list);
            return Task.FromResult(Ok(new { ok = removed, action, id }));
        }

        var cfg = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (cfg is null)
            return Task.FromResult(Fail($"配置中没有相机 {id}"));
        if (Has(doc, "exposure_us"))
            cfg.ExposureTimeUs = Dbl(doc, "exposure_us", cfg.ExposureTimeUs ?? 0);
        if (Has(doc, "gain"))
            cfg.Gain = Dbl(doc, "gain", cfg.Gain ?? 0);
        if (Has(doc, "grab_timeout_ms"))
            cfg.GrabTimeoutMs = Int(doc, "grab_timeout_ms", cfg.GrabTimeoutMs);
        _cameraStore.Save(_cfg.Cameras);

        var live = false;
        if (_cameras.TryGet(id, out var cam) && cam is IExposureControl exp)
        {
            if (cfg.ExposureTimeUs is { } us)
                live |= exp.TrySetExposureTimeUs(us);
            if (cfg.Gain is { } g)
                live |= exp.TrySetGain(g);
        }

        return Task.FromResult(Ok(new
        {
            ok = true,
            id,
            cfg.ExposureTimeUs,
            cfg.Gain,
            cfg.GrabTimeoutMs,
            liveApplied = live,
        }));
    }

    private Task<ChatToolResult> UpdateSettings(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var values = CurrentSettings();
        if (Has(doc, "timeout_ms"))
            values = values with { TimeoutMs = Int(doc, "timeout_ms", values.TimeoutMs) };
        if (Has(doc, "idle_timeout_ms"))
            values = values with { IdleTimeoutMs = Long(doc, "idle_timeout_ms", values.IdleTimeoutMs) };
        if (Has(doc, "max_queue"))
            values = values with { MaxQueueDepth = Int(doc, "max_queue", values.MaxQueueDepth) };
        if (Has(doc, "max_concurrent"))
            values = values with { MaxConcurrent = Int(doc, "max_concurrent", values.MaxConcurrent) };
        if (Has(doc, "tcp_backlog"))
            values = values with { TcpBacklog = Int(doc, "tcp_backlog", values.TcpBacklog) };
        if (Has(doc, "max_connections"))
            values = values with { MaxConnections = Int(doc, "max_connections", values.MaxConnections) };
        if (Has(doc, "tcp_port"))
            values = values with { TcpPort = Int(doc, "tcp_port", values.TcpPort) };
        if (Has(doc, "ip"))
            values = values with { IpAddress = Str(doc, "ip") };
        if (MaybeBool(doc, "pose_check") is { } pc)
            values = values with { PoseCheckEnabled = pc };
        if (Has(doc, "xy_tol"))
            values = values with { PoseXyToleranceMm = Dbl(doc, "xy_tol", values.PoseXyToleranceMm) };
        if (Has(doc, "rz_tol"))
            values = values with { PoseRzToleranceDeg = Dbl(doc, "rz_tol", values.PoseRzToleranceDeg) };
        if (Has(doc, "whitelist"))
            values = values with { IpWhitelist = ReadStringList(doc, "whitelist") };
        if (MaybeBool(doc, "failure_enabled") is { } fe)
            values = values with { FailureEnabled = fe };
        if (Has(doc, "failure_retained"))
            values = values with { FailureRetainedCount = Int(doc, "failure_retained", values.FailureRetainedCount) };
        if (MaybeBool(doc, "process_health") is { } ph)
            values = values with { ProcessHealthEnabled = ph };
        if (Has(doc, "consecutive_fail_limit"))
            values = values with { ConsecutiveFailLimit = Int(doc, "consecutive_fail_limit", values.ConsecutiveFailLimit) };
        if (MaybeBool(doc, "inhibit_on_limit") is { } inhib)
            values = values with { InhibitOnLimit = inhib };
        try
        {
            var restart = _settings.Save(values);
            return Task.FromResult(Ok(new { ok = true, restartHint = restart, values }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private Task<ChatToolResult> ManageModel(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action) || action == "list")
        {
            return Task.FromResult(Ok(new
            {
                ok = true,
                folder = _models.ModelsFolder,
                files = _models.ModelFileNames,
                loaded = _models.LoadedKeys.Select(k => new { path = k.Path, task = k.Task.ToString() }),
                loadedCount = _models.LoadedCount,
            }));
        }
        if (action == "unload_all")
        {
            _models.UnloadAll();
            return Task.FromResult(Ok(new { ok = true, action, loadedCount = _models.LoadedCount }));
        }
        if (action != "unload")
            return Task.FromResult(Fail("action 必须是 list|unload|unload_all"));
        var file = Str(doc, "file");
        if (string.IsNullOrWhiteSpace(file))
            return Task.FromResult(Fail("unload 需要 file"));
        var taskName = Str(doc, "task");
        if (string.IsNullOrWhiteSpace(taskName))
            _models.UnloadAll(file);
        else if (Enum.TryParse<InferenceTask>(taskName, ignoreCase: true, out var task))
            _models.Unload(file, task);
        else
            return Task.FromResult(Fail("task 必须是 ObjectDetection、Segmentation 或 PoseEstimation"));
        return Task.FromResult(Ok(new { ok = true, action, file, task = taskName, loadedCount = _models.LoadedCount }));
    }

    private Task<ChatToolResult> ManageCalibration(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "" or "list")
            return ListCalib("{}", ct);
        if (action != "delete")
            return Task.FromResult(Fail("action 必须是 list 或 delete"));
        var kind = Str(doc, "kind").ToLowerInvariant();
        var id = Str(doc, "id");
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("delete 需要 id（相机或工位）"));
        var done = kind switch
        {
            "intrinsic" => _calib.DeleteIntrinsic(id),
            "extrinsic" => _calib.DeleteExtrinsic(id),
            "polynomial" => _calib.DeletePolynomial(id),
            "scale" => _calib.DeleteScale(id),
            "rotation" => _calib.DeleteRotationCenter(id),
            _ => (bool?)null,
        };
        if (done is null)
            return Task.FromResult(Fail("kind 必须是 intrinsic|extrinsic|polynomial|scale|rotation"));
        return Task.FromResult(Ok(new { ok = done.Value, action, kind, id }));
    }

    private Task<ChatToolResult> ManageFiles(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "" or "list_captures")
        {
            var limit = Math.Clamp(Int(doc, "limit", 15), 1, 50);
            var chatDir = AppConfigExtensions.ResolveFolder("data/chat-captures");
            var capDir = AppConfigExtensions.ResolveFolder(_cfg.CaptureSuccess.Folder);
            return Task.FromResult(Ok(new
            {
                ok = true,
                chatCaptures = ListPng(chatDir, limit),
                successCaptures = ListPng(capDir, limit),
                successEnabled = _captures.Enabled,
            }));
        }
        var path = Str(doc, "path");
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(Fail("删除需要 path"));
        var root = action switch
        {
            "delete_failure" => _failures.Folder,
            "delete_capture" => AppConfigExtensions.ResolveFolder(_cfg.CaptureSuccess.Folder),
            "delete_chat" => AppConfigExtensions.ResolveFolder("data/chat-captures"),
            _ => "",
        };
        if (root.Length == 0)
            return Task.FromResult(Fail("action 必须是 list_captures|delete_failure|delete_capture|delete_chat"));
        if (!IsSafeUnder(root, path))
            return Task.FromResult(Fail("路径不在允许的目录内"));
        if (!File.Exists(path))
            return Task.FromResult(Fail("文件不存在"));
        File.Delete(path);
        var meta = Path.ChangeExtension(path, ".json");
        if (File.Exists(meta))
            File.Delete(meta);
        return Task.FromResult(Ok(new { ok = true, action, path }));
    }

    private Task<ChatToolResult> ConvertPose(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var station = Str(doc, "station_id");
        if (string.IsNullOrWhiteSpace(station))
            return Task.FromResult(Fail("请提供 station_id"));
        if (!Has(doc, "px") || !Has(doc, "py"))
            return Task.FromResult(Fail("请提供 px 和 py"));
        var pixel = new PixelPose(Dbl(doc, "px", 0), Dbl(doc, "py", 0), Dbl(doc, "angle_deg", 0), 1);
        var cameraId = EmptyToNull(Str(doc, "camera_id"));
        try
        {
            var mode = _calib.GetMappingMode(station);
            var robot = mode switch
            {
                StationMappingMode.Polynomial => _calib.PixelToRobotPolynomial(station, pixel, cameraId),
                StationMappingMode.Scale => _calib.PixelToRobotScale(station, pixel, cameraId),
                _ => _calib.PixelToRobot(station, pixel, cameraId),
            };
            return Task.FromResult(Ok(new
            {
                ok = true,
                station,
                mode = mode.ToString(),
                pixel = new { pixel.Cx, pixel.Cy, pixel.AngleDeg },
                robot = new { robot.X, robot.Y, robot.AngleDeg },
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private Task<ChatToolResult> SystemInfo(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        var now = DateTimeOffset.Now;
        return Task.FromResult(Ok(new
        {
            ok = true,
            now = now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            today = now.ToString("yyyy-MM-dd"),
            weekday = now.ToString("dddd", new System.Globalization.CultureInfo("zh-CN")),
            machine = Environment.MachineName,
            processors = Environment.ProcessorCount,
            workingSetMb = Math.Round(proc.WorkingSet64 / 1024d / 1024d, 1),
            gcMb = Math.Round(GC.GetTotalMemory(false) / 1024d / 1024d, 1),
            inference = _cfg.Inference.Provider,
            recipes = _recipes.LoadedCount,
            recipeStats = _vision.GetRecipeStats(),
            consecutiveFails = _vision.MaxConsecutiveFails,
            inhibited = _vision.AnyInhibited,
            healthFolder = _vision.ProcessHealth?.Folder,
            resultDb = new
            {
                path = _sqlite.DatabasePath,
                writeEnabled = _sqlite.Enabled,
                retainedDays = _sqlite.RetainedDays,
                total = _sqlite.Count(),
            },
        }));
    }

    private Task<ChatToolResult> ListFiles(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var key = Str(doc, "folder").ToLowerInvariant();
        var folder = key switch
        {
            "recipes" => _recipes.Folder,
            "models" => _models.ModelsFolder,
            "calibration" => AppConfigExtensions.ResolveFolder(_cfg.CalibrationFolder),
            "failures" => _failures.Folder,
            "captures" => AppConfigExtensions.ResolveFolder(_cfg.CaptureSuccess.Folder),
            "chat" => AppConfigExtensions.ResolveFolder("data/chat-captures"),
            "results" => _sqlite.Folder,
            _ => null,
        };
        if (folder is null)
            return Task.FromResult(Fail("folder 必须是 recipes|models|calibration|failures|captures|chat|results"));
        if (!Directory.Exists(folder))
            return Task.FromResult(Ok(new { ok = true, folder, files = Array.Empty<string>() }));
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Take(80)
            .Select(Path.GetFileName)
            .ToArray();
        return Task.FromResult(Ok(new { ok = true, folder, count = files.Length, files }));
    }

    private Task<ChatToolResult> LightSendRaw(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "id");
        var command = Str(doc, "command");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(command))
            return Task.FromResult(Fail("需要 id 和 command"));
        _lights.Get(id).SendRaw(command);
        return Task.FromResult(Ok(new { ok = true, id, command }));
    }

    private ServiceSettingsValues CurrentSettings() => new(
        TimeoutMs: _cfg.TimeoutMs,
        MaxQueueDepth: _cfg.MaxQueueDepth,
        MaxConcurrent: _cfg.MaxConcurrent,
        TcpBacklog: _cfg.TcpBacklog,
        MaxConnections: _cfg.MaxConnections,
        FailureEnabled: _cfg.FailureImage.Enabled,
        FailureRetainedCount: _cfg.FailureImage.RetainedCount,
        IpAddress: _cfg.IpAddress,
        TcpPort: _cfg.TcpPort,
        IpWhitelist: _cfg.IpWhitelist,
        IdleTimeoutMs: _cfg.IdleTimeoutMs,
        PoseCheckEnabled: _cfg.PoseCheck.Enabled,
        PoseXyToleranceMm: _cfg.PoseCheck.XyToleranceMm,
        PoseRzToleranceDeg: _cfg.PoseCheck.RzToleranceDeg,
        ProcessHealthEnabled: _cfg.ProcessHealth.Enabled,
        ConsecutiveFailLimit: _cfg.ProcessHealth.ConsecutiveFailLimit,
        InhibitOnLimit: _cfg.ProcessHealth.InhibitOnLimit);

    private static IReadOnlyList<object> ListPng(string folder, int limit)
    {
        if (!Directory.Exists(folder))
            return [];
        return Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTime)
            .Take(limit)
            .Select(p => (object)new { path = p, name = Path.GetFileName(p), written = File.GetLastWriteTime(p) })
            .ToList();
    }

    private static bool IsSafeUnder(string root, string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IChatTool Tool(
        string name, string description, JsonElement parameters,
        Func<string, CancellationToken, Task<ChatToolResult>> invoke) =>
        new DelegateChatTool(name, description, parameters, invoke);

    private static JsonElement Empty() => Schema(new { type = "object", properties = new Dictionary<string, object>() });

    private static JsonElement Props(params string[] names) => Props(names, required: false);

    private static JsonElement Props(string name, bool required) => Props([name], required);

    private static JsonElement Props(string[] names, bool required)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var n in names)
        {
            properties[n] = n switch
            {
                "ok_only" or "enabled" or "pose_check" or "failure_enabled" or "process_health"
                    or "inhibit_on_limit" => new { type = "boolean" },
                "limit" or "channel" or "brightness" or "client_id" or "serial" or "timeout_ms"
                    or "max_queue" or "tcp_port" or "grab_timeout_ms" or "max_concurrent"
                    or "tcp_backlog" or "max_connections" or "failure_retained"
                    or "consecutive_fail_limit" or "code" or "offset" or "days" or "hours" or "bins" => new { type = "integer" },
                "idle_timeout_ms" or "exposure_us" or "gain" or "xy_tol" or "rz_tol" or "px" or "py"
                    or "angle_deg" or "confidence" or "iou" => new { type = "number" },
                _ => new { type = "string" },
            };
        }
        object schema = required && names.Length > 0
            ? new { type = "object", properties, required = new[] { names[0] } }
            : new { type = "object", properties };
        return Schema(schema);
    }

    private static JsonElement Schema(object o) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(o))!;

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static string Str(JsonDocument doc, string name) =>
        doc.RootElement.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? ""
            : n.ValueKind is JsonValueKind.Number ? n.GetRawText() : "";

    private static int Int(JsonDocument doc, string name, int fallback)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return fallback;
        if (n.TryGetInt32(out var v))
            return v;
        if (n.ValueKind == JsonValueKind.String && int.TryParse(n.GetString(), out v))
            return v;
        return fallback;
    }

    private static long Long(JsonDocument doc, string name, long fallback)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return fallback;
        if (n.TryGetInt64(out var v))
            return v;
        if (n.ValueKind == JsonValueKind.String && long.TryParse(n.GetString(), out v))
            return v;
        if (n.TryGetDouble(out var d) && d is >= long.MinValue and <= long.MaxValue)
            return (long)d;
        return fallback;
    }

    private static bool Has(JsonDocument doc, string name) =>
        doc.RootElement.TryGetProperty(name, out var n) && n.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static double Dbl(JsonDocument doc, string name, double fallback)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return fallback;
        if (n.TryGetDouble(out var v))
            return v;
        if (n.ValueKind == JsonValueKind.String &&
            double.TryParse(n.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;
        return fallback;
    }

    private static bool? MaybeBool(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return null;
        if (n.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return n.GetBoolean();
        if (n.ValueKind == JsonValueKind.String && bool.TryParse(n.GetString(), out var b))
            return b;
        return null;
    }

    private static IReadOnlyList<string> ReadStringList(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return [];
        if (n.ValueKind == JsonValueKind.Array)
        {
            return n.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.GetRawText())
                .Where(s => s.Length > 0)
                .ToList();
        }
        if (n.ValueKind == JsonValueKind.String)
            return (n.GetString() ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return [];
    }

    private static string? EmptyToNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static ResultDbQuery BuildResultQuery(JsonDocument doc, int defaultLimit, int maxLimit)
    {
        var now = DateTimeOffset.Now;
        DateTimeOffset? from = ParseWhen(doc, "from");
        DateTimeOffset? to = ParseWhen(doc, "to");
        if (from is null && Has(doc, "days"))
            from = now.AddDays(-Math.Clamp(Int(doc, "days", 1), 1, 3650));
        if (from is null && Has(doc, "hours"))
            from = now.AddHours(-Math.Clamp(Int(doc, "hours", 1), 1, 24 * 90));
        if (from is null)
            ApplyRangePreset(Str(doc, "range"), now, ref from, ref to);
        return new ResultDbQuery
        {
            Recipe = EmptyToNull(Str(doc, "recipe")),
            Station = EmptyToNull(Str(doc, "station")),
            Camera = EmptyToNull(Str(doc, "camera")),
            Code = Has(doc, "code") ? Int(doc, "code", 0) : null,
            OkOnly = MaybeBool(doc, "ok_only"),
            MessageContains = EmptyToNull(Str(doc, "message")),
            From = from,
            To = to,
            Limit = Math.Clamp(Int(doc, "limit", defaultLimit), 1, maxLimit),
            Offset = Math.Max(0, Int(doc, "offset", 0)),
        };
    }

    private static ResultDbQuery WithAnalysisDefaultRange(JsonDocument doc, ResultDbQuery query)
    {
        if (query.From is not null || Has(doc, "from") || Has(doc, "days") || Has(doc, "hours") || Has(doc, "range"))
            return query;
        var now = DateTimeOffset.Now;
        return query with
        {
            From = new DateTimeOffset(now.Date, now.Offset),
            To = now,
        };
    }

    private static bool LooksLikeToday(ResultDbQuery query)
    {
        if (query.From is not { } from)
            return false;
        var now = DateTimeOffset.Now;
        return from.Date == now.Date;
    }

    private static void ApplyRangePreset(string range, DateTimeOffset now, ref DateTimeOffset? from, ref DateTimeOffset? to)
    {
        switch (range.Trim().ToLowerInvariant())
        {
            case "today" or "今天":
                from = new DateTimeOffset(now.Date, now.Offset);
                to ??= now;
                break;
            case "7d" or "7" or "week" or "近7天":
                from = now.AddDays(-7);
                to ??= now;
                break;
            case "30d" or "30" or "month" or "近30天":
                from = now.AddDays(-30);
                to ??= now;
                break;
            case "all" or "全部":
                from = null;
                break;
        }
    }

    private static DateTimeOffset? ParseWhen(JsonDocument doc, string name)
    {
        if (!Has(doc, name))
            return null;
        var s = Str(doc, name).Trim();
        if (s.Length == 0)
            return null;
        var now = DateTimeOffset.Now;
        if (s.Equals("today", StringComparison.OrdinalIgnoreCase) || s == "今天")
            return new DateTimeOffset(now.Date, now.Offset);
        if (s.Equals("now", StringComparison.OrdinalIgnoreCase) || s == "现在")
            return now;
        if (TryParseRelativeAgo(s, now, out var rel))
            return rel;
        if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out var local))
            return local;
        if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out var inv))
            return inv;
        return null;
    }

    private static bool TryParseRelativeAgo(string s, DateTimeOffset now, out DateTimeOffset value)
    {
        value = default;
        var t = s.Trim().ToLowerInvariant();
        if (t.StartsWith('-'))
            t = t[1..];
        t = t.Replace(" ", "", StringComparison.Ordinal);
        var unitIndex = -1;
        for (var i = 0; i < t.Length; i++)
        {
            if (!char.IsDigit(t[i]))
            {
                unitIndex = i;
                break;
            }
        }
        if (unitIndex <= 0 ||
            !int.TryParse(t[..unitIndex], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var n) || n <= 0)
            return false;
        var unit = t[unitIndex..];
        value = unit switch
        {
            "d" or "day" or "days" or "天" => now.AddDays(-n),
            "h" or "hr" or "hrs" or "hour" or "hours" or "小时" => now.AddHours(-n),
            _ => default,
        };
        return unit is "d" or "day" or "days" or "天" or "h" or "hr" or "hrs" or "hour" or "hours" or "小时";
    }

    private static ChatToolResult Ok(object o, string? image = null) =>
        new(JsonSerializer.Serialize(o, Json), image);

    private static ChatToolResult Fail(string error) =>
        new(JsonSerializer.Serialize(new { ok = false, error }, Json));
}
