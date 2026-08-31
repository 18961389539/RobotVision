using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.WpfHost.Features.SystemInfo;

public sealed record ConfigRow(string Key, string Value);

/// <summary>相机资产徽章：配置中的全部相机 + 运行时注册状态（未注册项在列表中标出，避免误判资产）。</summary>
public sealed record CameraBadge(string Id, bool Registered);

/// <summary>系统总览：TCP 监听/管线健康/资产计数/目录一览（可打开）/配置摘要。</summary>
public partial class SystemViewModel : ObservableObject, IDisposable
{
    private readonly AppConfig _cfg;
    private readonly ITcpRuntime _tcp;
    private readonly VisionService _vision;
    private readonly ICameraRuntime _cameras;
    private readonly RecipeLoader _recipes;
    private readonly FailureImageStore _failures;
    private readonly IModelRuntime _models;
    private readonly IInferenceEngineFactory _inference;
    private readonly ILogger<SystemViewModel> _log;
    private readonly DispatcherTimer _timer;

    public ObservableCollection<CameraBadge> CameraBadges { get; } = [];

    public ObservableCollection<ConfigRow> Directories { get; } = [];

    public ObservableCollection<ConfigRow> Settings { get; } = [];

    [ObservableProperty]
    private string _tcpStatus = "";

    [ObservableProperty]
    private string _queueStatus = "";

    /// <summary>最近 128 次请求的健康统计（成功率/耗时/超时，排障入口）。</summary>
    [ObservableProperty]
    private string _healthStatus = "";

    [ObservableProperty]
    private string _recipeStatus = "";

    public SystemViewModel(
        AppConfig cfg,
        ITcpRuntime tcp,
        VisionService vision,
        ICameraRuntime cameras,
        RecipeLoader recipes,
        FailureImageStore failures,
        IModelRuntime models,
        IInferenceEngineFactory inference,
        ILogger<SystemViewModel> log)
    {
        _cfg = cfg;
        _tcp = tcp;
        _vision = vision;
        _cameras = cameras;
        _recipes = recipes;
        _failures = failures;
        _models = models;
        _inference = inference;
        _log = log;

        RebuildSettings();
        RebuildDirectories();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshLive();
        RefreshLive();
    }

    /// <summary>上次摘要的指纹（配置未变化时跳过重建，避免 1 秒轮询反复刷新绑定）。</summary>
    private string _settingsDigest = "";

    /// <summary>上次目录列表的指纹（路径未变化时跳过重建）。</summary>
    private string _directoriesDigest = "";

    /// <summary>重建配置摘要（服务设置页保存后此处随轮询刷新，避免显示过期值）。</summary>
    private void RebuildSettings()
    {
        var digest = string.Join("|",
            _cfg.IpAddress, _cfg.TcpPort, _cfg.TimeoutMs, _cfg.IdleTimeoutMs,
            _cfg.PoseCheck.Enabled, _cfg.PoseCheck.XyToleranceMm, _cfg.PoseCheck.RzToleranceDeg,
            _cfg.MaxQueueDepth, _cfg.MaxConcurrent,
            _cfg.TcpBacklog, _cfg.MaxConnections, string.Join(",", _cfg.IpWhitelist),
            _cfg.FileLogging.Enabled, _cfg.FileLogging.RetainedDays,
            _failures.Enabled, _cfg.FailureImage.RetainedCount,
            _cfg.ResultLog.Enabled, _cfg.ResultLog.Jsonl, _cfg.ResultLog.Sqlite, _cfg.ResultLog.RetainedDays,
            _cfg.Inference.Provider, _inference.ActiveDevice, _inference.GpuUnavailable);
        if (digest == _settingsDigest)
            return;
        _settingsDigest = digest;

        Settings.Clear();
        Settings.Add(new("TCP 监听", $"{_cfg.IpAddress}:{_cfg.TcpPort}（保存后热重启监听）"));
        Settings.Add(new("请求超时", $"{_cfg.TimeoutMs} ms（启动下限 90000）"));
        Settings.Add(new("连接空闲超时", _cfg.IdleTimeoutMs <= 0 ? "永久" : $"{_cfg.IdleTimeoutMs} ms"));
        Settings.Add(new("位姿校验", _cfg.PoseCheck.Enabled
            ? $"开 · XY {_cfg.PoseCheck.XyToleranceMm:0.###}mm · RZ {_cfg.PoseCheck.RzToleranceDeg:0.###}°"
            : "关"));
        Settings.Add(new("最大排队深度", $"{_cfg.MaxQueueDepth}"));
        Settings.Add(new("并发管线槽位", $"{_cfg.MaxConcurrent}（同模型推理仍串行）"));
        Settings.Add(new("推理", FormatInference()));
        Settings.Add(new("TCP backlog", $"{_cfg.TcpBacklog}"));
        Settings.Add(new("最大连接数", _cfg.MaxConnections <= 0 ? "不限" : $"{_cfg.MaxConnections}"));
        Settings.Add(new("IP 白名单", _cfg.IpWhitelist.Count == 0
            ? "关闭（允许所有）"
            : $"开启 · {_cfg.IpWhitelist.Count} 条（保存后立即生效）"));
        Settings.Add(new("文件日志", _cfg.FileLogging.Enabled
            ? $"开启 · 保留 {_cfg.FileLogging.RetainedDays} 天" : "关闭"));
        Settings.Add(new("失败留存", _failures.Enabled
            ? $"开启 · 保留 {_cfg.FailureImage.RetainedCount} 张" : "关闭"));
        Settings.Add(new("结果留档", FormatResultLog()));
    }

    private string FormatResultLog()
    {
        if (!_cfg.ResultLog.Enabled)
            return "关闭";
        var sinks = new List<string>();
        if (_cfg.ResultLog.Jsonl) sinks.Add("JSONL");
        if (_cfg.ResultLog.Sqlite) sinks.Add("SQLite");
        var sink = sinks.Count == 0 ? "无写入目标" : string.Join("+", sinks);
        var keep = _cfg.ResultLog.RetainedDays <= 0 ? "不清理" : $"保留 {_cfg.ResultLog.RetainedDays} 天";
        return $"开启 · {sink} · {keep}";
    }

    private string FormatInference()
    {
        var configured = string.IsNullOrWhiteSpace(_cfg.Inference.Provider)
            ? "OpenVinoGpu"
            : _cfg.Inference.Provider;
        if (_inference.GpuUnavailable)
            return $"配置 {configured} · 实际 OpenVINO CPU（GPU 不可用，重启后才再试）";
        if (string.IsNullOrEmpty(_inference.ActiveDevice))
            return $"配置 {configured} · 尚未加载模型";
        return $"配置 {configured} · 实际 OpenVINO {_inference.ActiveDevice}";
    }

    /// <summary>重建目录列表（配置路径变化才更新，避免显示过期路径）。</summary>
    private void RebuildDirectories()
    {
        var digest = string.Join("|",
            _cfg.ResolveDataRoot(),
            _recipes.Folder,
            AppConfigExtensions.ResolveFolder(_cfg.ModelsFolder),
            _cfg.ResolveCalibrationFolder(),
            _failures.Folder,
            _cfg.ResolveDataPath(_cfg.FileLogging.Folder),
            _cfg.ResolveDataPath(_cfg.ResultLog.Folder));
        if (digest == _directoriesDigest)
            return;
        _directoriesDigest = digest;

        Directories.Clear();
        if (!string.IsNullOrWhiteSpace(_cfg.DataRoot))
            Directories.Add(new("数据根", _cfg.ResolveDataRoot()));
        Directories.Add(new("配方目录", _recipes.Folder));
        Directories.Add(new("模型目录", AppConfigExtensions.ResolveFolder(_cfg.ModelsFolder)));
        Directories.Add(new("标定目录", _cfg.ResolveCalibrationFolder()));
        Directories.Add(new("失败现场目录", _failures.Folder));
        Directories.Add(new("结果目录", _cfg.ResolveDataPath(_cfg.ResultLog.Folder)));
        Directories.Add(new("日志目录", _cfg.ResolveDataPath(_cfg.FileLogging.Folder)));
    }

    /// <summary>页面进入时启动实时刷新（单例 VM，可反复启停）。</summary>
    public void StartTimer() => _timer.Start();

    /// <summary>页面卸载/窗口关闭时停止刷新（VM 为单例，避免空转）。</summary>
    public void StopTimer() => _timer.Stop();

    /// <summary>进程退出时由 DI 容器级联调用（单例 VM）：停止 1 秒实时刷新。</summary>
    public void Dispose() => _timer.Stop();

    /// <summary>配方计数磁盘枚举节流：1s 轮询不每秒枚举目录，5s 一次。</summary>
    private DateTime _lastRecipeScan;
    private int _recipeCount;

    private void RefreshLive()
    {
        // TCP：监听状态 + 在线客户端（服务停止时明确显示"已停止"，不误导为"没客户端"）
        TcpStatus = _tcp.IsRunning
            ? $"监听 {_tcp.ListenEndPoint} · 在线客户端 {_tcp.ConnectedClients}"
            : "TCP 服务已停止";

        var sessions = _models.LoadedKeys.Select(k => k.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        QueueStatus = $"队列 {_vision.QueueDepth}/{_vision.MaxQueueDepth} · 已缓存模型会话 {sessions} 个";

        var h = _vision.Health;
        var succeeded = h.Total - h.Failed - h.TimedOut;
        HealthStatus = h.Total == 0
            ? "尚无请求记录"
            : $"最近 {h.Total} 次 · 成功 {succeeded} · 失败 {h.Failed} · 超时 {h.TimedOut} · 平均 {h.AvgMs:0}ms · P95 {h.P95Ms:0}ms";

        // 相机资产：配置中的全部相机 + 注册状态（相机管理页可动态增删）
        var badges = _cfg.Cameras
            .Select(c => new CameraBadge(c.Id, _cameras.IsRegistered(c.Id)))
            .ToList();
        if (!CameraBadges.SequenceEqual(badges))
        {
            CameraBadges.Clear();
            foreach (var b in badges)
                CameraBadges.Add(b);
        }

        // 配方计数节流（5 秒枚举一次磁盘，不随 1s 轮询空转 IO）
        if (DateTime.UtcNow - _lastRecipeScan > TimeSpan.FromSeconds(5))
        {
            _recipeCount = _recipes.ListNames().Count;
            _lastRecipeScan = DateTime.UtcNow;
        }
        var registeredCount = CameraBadges.Count(b => b.Registered);
        RecipeStatus = $"配方 {_recipeCount} 个 · 相机 {CameraBadges.Count} 台（注册 {registeredCount} 台）";

        RebuildSettings();
        RebuildDirectories();
    }

    [RelayCommand]
    private static void OpenFolder(ConfigRow? row)
    {
        if (row is null)
            return;
        Explorer.OpenFolder(row.Value);
    }
}
