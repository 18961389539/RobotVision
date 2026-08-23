using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.UI;

public sealed record ConfigRow(string Key, string Value);

/// <summary>相机资产徽章：配置中的全部相机 + 运行时注册状态（未注册项在列表中标出，避免误判资产）。</summary>
public sealed record CameraBadge(string Id, bool Registered);

/// <summary>系统总览：TCP 监听/管线健康/资产计数/目录一览（可打开）/配置摘要。</summary>
public partial class SystemViewModel : ObservableObject
{
    private readonly AppConfig _cfg;
    private readonly TcpServerManager _tcp;
    private readonly VisionService _vision;
    private readonly CameraManager _cameras;
    private readonly RecipeLoader _recipes;
    private readonly FailureImageStore _failures;
    private readonly ModelManager _models;
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
        TcpServerManager tcp,
        VisionService vision,
        CameraManager cameras,
        RecipeLoader recipes,
        FailureImageStore failures,
        ModelManager models)
    {
        _cfg = cfg;
        _tcp = tcp;
        _vision = vision;
        _cameras = cameras;
        _recipes = recipes;
        _failures = failures;
        _models = models;

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
            _cfg.IpAddress, _cfg.TcpPort, _cfg.TimeoutMs, _cfg.MaxQueueDepth, _cfg.MaxConcurrent,
            _cfg.TcpBacklog, _cfg.MaxConnections, string.Join(",", _cfg.IpWhitelist),
            _cfg.FileLogging.Enabled, _cfg.FileLogging.RetainedDays,
            _failures.Enabled, _cfg.FailureImage.RetainedCount);
        if (digest == _settingsDigest)
            return;
        _settingsDigest = digest;

        Settings.Clear();
        Settings.Add(new("TCP 监听", $"{_cfg.IpAddress}:{_cfg.TcpPort}（重启生效）"));
        Settings.Add(new("请求超时", $"{_cfg.TimeoutMs} ms"));
        Settings.Add(new("最大排队深度", $"{_cfg.MaxQueueDepth}"));
        Settings.Add(new("并发推理槽位", $"{_cfg.MaxConcurrent}"));
        Settings.Add(new("TCP backlog", $"{_cfg.TcpBacklog}"));
        Settings.Add(new("最大连接数", _cfg.MaxConnections <= 0 ? "不限" : $"{_cfg.MaxConnections}"));
        Settings.Add(new("IP 白名单", _cfg.IpWhitelist.Count == 0
            ? "关闭（允许所有）"
            : $"开启 · {_cfg.IpWhitelist.Count} 条（保存后立即生效）"));
        Settings.Add(new("文件日志", _cfg.FileLogging.Enabled
            ? $"开启 · 保留 {_cfg.FileLogging.RetainedDays} 天" : "关闭"));
        Settings.Add(new("失败留存", _failures.Enabled
            ? $"开启 · 保留 {_cfg.FailureImage.RetainedCount} 张" : "关闭"));
    }

    /// <summary>重建目录列表（配置路径变化才更新，避免显示过期路径）。</summary>
    private void RebuildDirectories()
    {
        var digest = string.Join("|",
            _recipes.Folder,
            AppConfigExtensions.ResolveFolder(_cfg.ModelsFolder),
            AppConfigExtensions.ResolveFolder(_cfg.CalibrationFolder),
            _failures.Folder,
            AppConfigExtensions.ResolveFolder(_cfg.FileLogging.Folder));
        if (digest == _directoriesDigest)
            return;
        _directoriesDigest = digest;

        Directories.Clear();
        Directories.Add(new("配方目录", _recipes.Folder));
        Directories.Add(new("模型目录", AppConfigExtensions.ResolveFolder(_cfg.ModelsFolder)));
        Directories.Add(new("标定目录", AppConfigExtensions.ResolveFolder(_cfg.CalibrationFolder)));
        Directories.Add(new("失败现场目录", _failures.Folder));
        Directories.Add(new("日志目录", AppConfigExtensions.ResolveFolder(_cfg.FileLogging.Folder)));
    }

    /// <summary>页面进入时启动实时刷新（单例 VM，可反复启停）。</summary>
    public void StartTimer() => _timer.Start();

    /// <summary>页面卸载/窗口关闭时停止刷新（VM 为单例，避免空转）。</summary>
    public void StopTimer() => _timer.Stop();

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
    private void OpenFolder(ConfigRow row)
    {
        if (row is null)
            return;
        RecipeViewModel.ShellOpen(row.Value);
    }
}
