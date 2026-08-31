using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.Hosting;

/// <summary>
/// 服务参数的写回与同步：把超时/队列/并发/连接上限/白名单/失败留存/网络端点
/// 写入 appsettings.json（保留其他节点），并同步内存 AppConfig 单例。
/// 分两层语义：热参数（超时/队列/并发/连接上限/白名单/留存）由调用方同时应用到
/// 运行中的管理器立即生效；网络端点（IP/端口）改动经 Restart 热重启监听，
/// 失败时由调用方回滚提示（本类只负责落盘与内存同步）。
/// 校验集中在本层（Save 抛 InvalidDataException），非 UI 调用方同样受保护。
/// </summary>
public sealed class AppSettingsStore(AppConfig cfg, string? settingsPath = null)
{
    private readonly string _settingsPath =
        settingsPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public string SettingsPath => _settingsPath;

    /// <summary>
    /// 落盘并同步内存配置后触发的运行时同步回调（组装层注入）：
    /// 把可热应用的参数同步到运行中的管理器（TcpServerManager/VisionService 等），
    /// 使任何调用方（不限于 UI）保存后运行时即生效，无需调用方逐个手动应用。
    /// 不可热应用的参数（MaxConcurrent/TcpBacklog 首次固化）由回调内显式跳过并记录。
    /// </summary>
    public Action<AppConfig>? RuntimeSync { get; set; }

    /// <summary>
    /// 保存全部可管理参数：校验 → 落盘（其他节点原样保留）→ 同步内存配置。
    /// 返回需要重启才能生效的参数名列表（当前为空：端点可热重启；保留返回值以兼容扩展）。
    /// </summary>
    public IReadOnlyList<string> Save(ServiceSettingsValues values)
    {
        Validate(values);

        // 反向联动校验：总超时必须大于所有硬件相机的取图超时，
        // 否则相机取图超时将表现为 1008（处理超时）而非 1003（取图失败），排障困难
        var conflicts = cfg.Cameras
            .Where(c => c.UsesGrabTimeout())
            .Where(c => c.GrabTimeoutMs >= values.TimeoutMs)
            .Select(c => $"{c.Id}(GrabTimeoutMs={c.GrabTimeoutMs})")
            .ToList();
        if (conflicts.Count > 0)
            throw new InvalidDataException(
                $"相机 {string.Join("、", conflicts)} 的取图超时不小于新的总超时 {values.TimeoutMs}ms，" +
                "取图超时将表现为 1008 而非 1003，请先调大总超时或调小 GrabTimeoutMs");

        // 原子读-改-写：整段"读取→变更→落盘"在 JsonAtomicWrite 同一把进程内静态锁下执行，
        // 与其他写方（CameraConfigStore/LightingConfigStore）串行化，杜绝并发保存互相覆盖
        JsonAtomicWrite.Update(_settingsPath, Indented, obj =>
        {
            obj["TimeoutMs"] = values.TimeoutMs;
            obj["IdleTimeoutMs"] = values.IdleTimeoutMs;
            obj["MaxQueueDepth"] = values.MaxQueueDepth;
            obj["MaxConcurrent"] = values.MaxConcurrent;
            obj["TcpBacklog"] = values.TcpBacklog;
            obj["MaxConnections"] = values.MaxConnections;
            obj["IpAddress"] = values.IpAddress;
            obj["TcpPort"] = values.TcpPort;
            obj["IpWhitelist"] = JsonSerializer.SerializeToNode(values.IpWhitelist, Indented);
            obj["UiTheme"] = UiThemes.Normalize(values.UiTheme);

            var poseCheck = obj["PoseCheck"] as JsonObject ?? [];
            poseCheck["Enabled"] = values.PoseCheckEnabled;
            poseCheck["XyToleranceMm"] = values.PoseXyToleranceMm;
            poseCheck["RzToleranceDeg"] = values.PoseRzToleranceDeg;
            obj["PoseCheck"] = poseCheck;

            var health = obj["ProcessHealth"] as JsonObject ?? [];
            health["Enabled"] = values.ProcessHealthEnabled;
            health["ConsecutiveFailLimit"] = values.ConsecutiveFailLimit;
            health["InhibitOnLimit"] = values.InhibitOnLimit;
            health["RetainedDays"] = values.ProcessHealthRetainedDays;
            obj["ProcessHealth"] = health;

            var inference = obj["Inference"] as JsonObject ?? [];
            inference["Provider"] = values.InferenceProvider;
            inference["MaxSessions"] = values.InferenceMaxSessions;
            obj["Inference"] = inference;

            var fileLogging = obj["FileLogging"] as JsonObject ?? [];
            fileLogging["Enabled"] = values.FileLoggingEnabled;
            fileLogging["RetainedDays"] = values.FileLoggingRetainedDays;
            obj["FileLogging"] = fileLogging;

            var failure = obj["FailureImage"] as JsonObject ?? [];
            failure["Enabled"] = values.FailureEnabled;
            failure["RetainedCount"] = values.FailureRetainedCount;
            failure["RetainedDays"] = values.FailureRetainedDays;
            obj["FailureImage"] = failure;

            var capture = obj["CaptureSuccess"] as JsonObject ?? [];
            capture["Enabled"] = values.CaptureSuccessEnabled;
            capture["RetainedDays"] = values.CaptureSuccessRetainedDays;
            capture["MaxWidth"] = values.CaptureSuccessMaxWidth;
            obj["CaptureSuccess"] = capture;

            var resultLog = obj["ResultLog"] as JsonObject ?? [];
            resultLog["Enabled"] = values.ResultLogEnabled;
            resultLog["Jsonl"] = values.ResultLogJsonl;
            resultLog["Sqlite"] = values.ResultLogSqlite;
            resultLog["RetainedDays"] = values.ResultLogRetainedDays;
            obj["ResultLog"] = resultLog;
        });

        cfg.TimeoutMs = values.TimeoutMs;
        cfg.IdleTimeoutMs = values.IdleTimeoutMs;
        cfg.MaxQueueDepth = values.MaxQueueDepth;
        cfg.MaxConcurrent = values.MaxConcurrent;
        cfg.TcpBacklog = values.TcpBacklog;
        cfg.MaxConnections = values.MaxConnections;
        cfg.IpAddress = values.IpAddress;
        cfg.TcpPort = values.TcpPort;
        cfg.IpWhitelist = [.. values.IpWhitelist];
        cfg.FailureImage.Enabled = values.FailureEnabled;
        cfg.FailureImage.RetainedCount = values.FailureRetainedCount;
        cfg.FailureImage.RetainedDays = values.FailureRetainedDays;
        cfg.CaptureSuccess.Enabled = values.CaptureSuccessEnabled;
        cfg.CaptureSuccess.RetainedDays = values.CaptureSuccessRetainedDays;
        cfg.CaptureSuccess.MaxWidth = values.CaptureSuccessMaxWidth;
        cfg.ResultLog.Enabled = values.ResultLogEnabled;
        cfg.ResultLog.Jsonl = values.ResultLogJsonl;
        cfg.ResultLog.Sqlite = values.ResultLogSqlite;
        cfg.ResultLog.RetainedDays = values.ResultLogRetainedDays;
        cfg.PoseCheck.Enabled = values.PoseCheckEnabled;
        cfg.PoseCheck.XyToleranceMm = values.PoseXyToleranceMm;
        cfg.PoseCheck.RzToleranceDeg = values.PoseRzToleranceDeg;
        cfg.ProcessHealth.Enabled = values.ProcessHealthEnabled;
        cfg.ProcessHealth.ConsecutiveFailLimit = values.ConsecutiveFailLimit;
        cfg.ProcessHealth.InhibitOnLimit = values.InhibitOnLimit;
        cfg.ProcessHealth.RetainedDays = values.ProcessHealthRetainedDays;
        cfg.Inference.Provider = values.InferenceProvider;
        cfg.Inference.MaxSessions = values.InferenceMaxSessions;
        cfg.FileLogging.Enabled = values.FileLoggingEnabled;
        cfg.FileLogging.RetainedDays = values.FileLoggingRetainedDays;
        cfg.UiTheme = UiThemes.Normalize(values.UiTheme);

        // 落盘 + 内存同步完成后，把可热应用的参数同步到运行中的管理器（见 RuntimeSync 注释）
        RuntimeSync?.Invoke(cfg);

        return [];
    }

    /// <summary>
    /// 参数值域校验（集中于此，任何保存入口共用）：
    /// 超时 ≥500ms、队列 ≥1、并发 ∈ [1, 队列深度]、backlog ∈ [1,1024]、连接 ≥0、
    /// 留存 ≥0、端口 1~65535、IP 可解析、白名单条目与匹配器语义一致。
    /// </summary>
    public static void Validate(ServiceSettingsValues values)
    {
        if (values.TimeoutMs < 500)
            throw new InvalidDataException("请求超时不能低于 500ms");
        if (values.IdleTimeoutMs < 0)
            throw new InvalidDataException("空闲超时不能为负（0 = 永久保持连接）");
        if (values.IdleTimeoutMs is > 0 and < 1000)
            throw new InvalidDataException("空闲超时若启用须 ≥1000ms（0 = 永久）");
        if (values.PoseXyToleranceMm <= 0 || values.PoseRzToleranceDeg <= 0)
            throw new InvalidDataException("PoseCheck 容差必须为正");
        if (values.ConsecutiveFailLimit < 0)
            throw new InvalidDataException("连续失败联锁次数不能为负（0 = 不联锁）");
        if (values.MaxQueueDepth < 1)
            throw new InvalidDataException("队列深度至少为 1");
        if (values.MaxConcurrent < 1 || values.MaxConcurrent > values.MaxQueueDepth)
            throw new InvalidDataException($"并发执行上限必须在 1~队列深度({values.MaxQueueDepth}) 之间（含执行中的任务）");
        if (values.TcpBacklog is < 1 or > 1024)
            throw new InvalidDataException("监听 backlog 必须在 1~1024");
        if (values.MaxConnections < 0)
            throw new InvalidDataException("连接上限不能为负（0 = 不限）");
        if (values.FailureRetainedCount < 0)
            throw new InvalidDataException("失败留存数量不能为负（0 = 不自动清理）");
        if (values.FailureRetainedDays < 0)
            throw new InvalidDataException("失败留存天数不能为负（0 = 不按天清理）");
        if (values.CaptureSuccessRetainedDays < 0)
            throw new InvalidDataException("成功留存天数不能为负（0 = 不按天清理）");
        if (values.CaptureSuccessMaxWidth < 0)
            throw new InvalidDataException("成功留存缩图宽度不能为负（0 = 原图）");
        if (values.ResultLogRetainedDays < 0)
            throw new InvalidDataException("结果留档天数不能为负（0 = 不清理）");
        if (values.ResultLogEnabled && !values.ResultLogJsonl && !values.ResultLogSqlite)
            throw new InvalidDataException("结果留档已开启时，须至少勾选 JSONL 或 SQLite 之一");
        if (!IsKnownInferenceProvider(values.InferenceProvider))
            throw new InvalidDataException("推理 Provider 须为 OpenVinoGpu 或 OpenVinoCpu");
        if (values.InferenceMaxSessions < 0)
            throw new InvalidDataException("推理会话上限不能为负（0 = 不限制）");
        if (values.FileLoggingRetainedDays < 0)
            throw new InvalidDataException("文件日志保留天数不能为负（0 = 不清理）");
        if (values.ProcessHealthRetainedDays < 0)
            throw new InvalidDataException("过程能力指标保留天数不能为负（0 = 不按天清理）");
        if (values.TcpPort is < 1 or > 65535)
            throw new InvalidDataException("端口必须在 1~65535");
        if (!IPAddress.TryParse(values.IpAddress, out _))
            throw new InvalidDataException($"IP 地址无效: {values.IpAddress}");
        foreach (var entry in values.IpWhitelist)
        {
            if (!TcpServerManager.TryParseWhitelistEntry(entry))
                throw new InvalidDataException($"白名单条目无效: {entry}（支持精确 IP 或前缀通配如 192.168.*）");
        }
    }

    /// <summary>
    /// 启动时校验 appsettings.json 解析出的运行时配置值域（与 <see cref="Validate"/> 保存校验保持同一套规则）。
    /// 非法值（如 TimeoutMs=100）直接启动失败并抛出清晰异常，避免静默生效带病运行。
    /// 逐相机联动校验：Basler / GigEVision 取图超时须小于总超时，否则取图超时表现为 1008 而非 1003，排障困难。
    /// </summary>
    public static void ValidateConfig(AppConfig cfg)
    {
        if (cfg.TimeoutMs < 500)
            throw new InvalidDataException($"appsettings.json 的 TimeoutMs={cfg.TimeoutMs} 低于 500ms，无法保证相机取图与推理的合理裕量");
        if (cfg.IdleTimeoutMs < 0)
            throw new InvalidDataException($"appsettings.json 的 IdleTimeoutMs={cfg.IdleTimeoutMs} 不能为负（0 = 永久保持连接）");
        if (cfg.IdleTimeoutMs is > 0 and < 1000)
            throw new InvalidDataException($"appsettings.json 的 IdleTimeoutMs={cfg.IdleTimeoutMs} 若启用须 ≥1000ms（0 = 永久）");
        if (cfg.PoseCheck.XyToleranceMm <= 0 || cfg.PoseCheck.RzToleranceDeg <= 0)
            throw new InvalidDataException("appsettings.json 的 PoseCheck 容差必须为正");
        if (cfg.ProcessHealth.ConsecutiveFailLimit < 0)
            throw new InvalidDataException("appsettings.json 的 ProcessHealth.ConsecutiveFailLimit 不能为负（0 = 不联锁）");
        if (cfg.ResultLog.RetainedDays < 0)
            throw new InvalidDataException("appsettings.json 的 ResultLog.RetainedDays 不能为负（0 = 不清理）");
        if (!IsKnownInferenceProvider(cfg.Inference.Provider))
            throw new InvalidDataException(
                $"appsettings.json 的 Inference.Provider={cfg.Inference.Provider} 无效（须为 OpenVinoGpu 或 OpenVinoCpu）");
        if (cfg.Inference.MaxSessions < 0)
            throw new InvalidDataException("appsettings.json 的 Inference.MaxSessions 不能为负（0 = 不限制）");
        if (cfg.FileLogging.RetainedDays < 0)
            throw new InvalidDataException("appsettings.json 的 FileLogging.RetainedDays 不能为负（0 = 不清理）");
        if (cfg.ProcessHealth.RetainedDays < 0)
            throw new InvalidDataException("appsettings.json 的 ProcessHealth.RetainedDays 不能为负（0 = 不按天清理）");
        if (cfg.MaxQueueDepth < 1)
            throw new InvalidDataException($"appsettings.json 的 MaxQueueDepth={cfg.MaxQueueDepth} 至少为 1");
        if (cfg.MaxConcurrent < 1 || cfg.MaxConcurrent > cfg.MaxQueueDepth)
            throw new InvalidDataException($"appsettings.json 的 MaxConcurrent={cfg.MaxConcurrent} 必须在 1~MaxQueueDepth({cfg.MaxQueueDepth}) 之间（含执行中的任务）");
        if (cfg.TcpBacklog is < 1 or > 1024)
            throw new InvalidDataException($"appsettings.json 的 TcpBacklog={cfg.TcpBacklog} 必须在 1~1024");
        if (cfg.MaxConnections < 0)
            throw new InvalidDataException($"appsettings.json 的 MaxConnections={cfg.MaxConnections} 不能为负（0 = 不限）");
        if (cfg.TcpPort is < 1 or > 65535)
            throw new InvalidDataException($"appsettings.json 的 TcpPort={cfg.TcpPort} 必须在 1~65535");
        if (!IPAddress.TryParse(cfg.IpAddress, out _))
            throw new InvalidDataException($"appsettings.json 的 IpAddress 无效: {cfg.IpAddress}");
        foreach (var entry in cfg.IpWhitelist)
        {
            if (!TcpServerManager.TryParseWhitelistEntry(entry))
                throw new InvalidDataException($"appsettings.json 白名单条目无效: {entry}（支持精确 IP 或前缀通配如 192.168.*）");
        }
        foreach (var camera in cfg.Cameras)
        {
            if (camera.UsesGrabTimeout() && camera.GrabTimeoutMs >= cfg.TimeoutMs)
                throw new InvalidDataException(
                    $"相机 {camera.Id} 的 GrabTimeoutMs={camera.GrabTimeoutMs} 不小于总超时 TimeoutMs={cfg.TimeoutMs}，" +
                    "取图超时将表现为 1008 而非 1003，请先调大总超时或调小 GrabTimeoutMs");
        }
    }

    private static bool IsKnownInferenceProvider(string provider)
    {
        var key = provider.Trim().ToUpperInvariant()
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        return key is "OPENVINOGPU" or "GPU" or "OPENVINO" or "OPENVINOCPU" or "CPU";
    }
}

/// <summary>一次保存携带的完整参数集合（与 AppConfig 字段一一对应）。</summary>
public sealed record ServiceSettingsValues(
    int TimeoutMs,
    int MaxQueueDepth,
    int MaxConcurrent,
    int TcpBacklog,
    int MaxConnections,
    bool FailureEnabled,
    int FailureRetainedCount,
    string IpAddress,
    int TcpPort,
    IReadOnlyList<string> IpWhitelist,
    long IdleTimeoutMs = 0,
    bool PoseCheckEnabled = true,
    double PoseXyToleranceMm = 0.5,
    double PoseRzToleranceDeg = 0.5,
    bool ProcessHealthEnabled = true,
    int ConsecutiveFailLimit = 5,
    bool InhibitOnLimit = true,
    int FailureRetainedDays = 0,
    bool CaptureSuccessEnabled = false,
    int CaptureSuccessRetainedDays = 30,
    int CaptureSuccessMaxWidth = 0,
    bool ResultLogEnabled = true,
    bool ResultLogJsonl = true,
    bool ResultLogSqlite = true,
    int ResultLogRetainedDays = 30,
    string InferenceProvider = "OpenVinoGpu",
    int InferenceMaxSessions = 8,
    bool FileLoggingEnabled = true,
    int FileLoggingRetainedDays = 30,
    int ProcessHealthRetainedDays = 90,
    string UiTheme = UiThemes.Dark);
