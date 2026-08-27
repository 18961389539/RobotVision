using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.WpfHost.Features.Settings;

/// <summary>
/// 服务参数管理：运行参数（超时/队列/连接上限/失败留存/白名单）保存即热生效；
/// 并发槽位（MaxConcurrent）与 TCP backlog 修改需重启程序生效；
/// 网络端点（IP/端口）保存后热重启监听（无需重启程序，失败自动回滚并提示）。
/// 校验集中在 AppSettingsStore（值域 + 相机取图超时联动），本页只负责展示与交互。
/// </summary>
public partial class SettingsViewModel : ObservableObject, ICommitPendingEdits
{
    // 出厂默认值（与 appsettings.json 初始一致）
    private const long DefaultIdleTimeoutMs = 0;
    private const int DefaultMaxQueueDepth = 4;
    private const int DefaultMaxConcurrent = 2;
    private const int DefaultTcpBacklog = 16;
    private const int DefaultMaxConnections = 0;
    private const bool DefaultFailureEnabled = true;
    private const int DefaultFailureRetainedCount = 200;
    private const string DefaultIpAddress = "0.0.0.0";
    private const int DefaultTcpPort = 9999;

    private readonly AppConfig _cfg;
    private readonly TcpServerManager _tcp;
    private readonly VisionService _vision;
    private readonly FailureImageStore _failures;
    private readonly AppSettingsStore _store;
    private readonly IInferenceEngineFactory? _inference;
    private readonly DispatcherTimer _timer;

    /// <summary>最近一次载入/保存时的参数快照（脏标记基准）。</summary>
    private ServiceSettingsValues? _baseline;

    public Action? FlushPendingEdits { get; set; }

    /// <summary>0 = 永久保持连接；2592000000 = 30 天。</summary>
    [ObservableProperty]
    private double _idleTimeoutMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPoseToleranceFields))]
    private bool _poseCheckEnabled;

    [ObservableProperty]
    private double _poseXyToleranceMm = 0.5;

    [ObservableProperty]
    private double _poseRzToleranceDeg = 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProcessHealthFields))]
    private bool _processHealthEnabled = true;

    [ObservableProperty]
    private int _consecutiveFailLimit = 5;

    [ObservableProperty]
    private bool _inhibitOnLimit = true;

    [ObservableProperty]
    private int _maxQueueDepth;

    [ObservableProperty]
    private int _maxConcurrent;

    [ObservableProperty]
    private int _tcpBacklog;

    [ObservableProperty]
    private int _maxConnections;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFailureRetention))]
    private bool _failureEnabled;

    /// <summary>位姿校验关闭时隐藏容差字段（仅调试场景关校验）。</summary>
    public bool ShowPoseToleranceFields => PoseCheckEnabled;

    /// <summary>过程能力关闭时隐藏联锁次数。</summary>
    public bool ShowProcessHealthFields => ProcessHealthEnabled;

    /// <summary>失败留存关闭时隐藏保留数量。</summary>
    public bool ShowFailureRetention => FailureEnabled;

    [ObservableProperty]
    private int _failureRetainedCount;

    [ObservableProperty]
    private string _ipAddress = "";

    [ObservableProperty]
    private int _tcpPort;

    /// <summary>白名单多行文本（每行一条，空 = 允许所有，支持 192.168.* 通配）。</summary>
    [ObservableProperty]
    private string _whitelistText = "";

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string _inferenceStatus = "";

    public SettingsViewModel(
        AppConfig cfg,
        TcpServerManager tcp,
        VisionService vision,
        FailureImageStore failures,
        AppSettingsStore store,
        IInferenceEngineFactory? inference = null)
    {
        _cfg = cfg;
        _tcp = tcp;
        _vision = vision;
        _failures = failures;
        _store = store;
        _inference = inference;

        LoadFromRuntime();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
        // 定时器由页面 Loaded/Unloaded 启停（与 System/Communication 页一致），
        // 不在构造启动：单例 VM 常驻，页面从未打开时不应空转。
    }

    /// <summary>编辑内容相对最近一次载入/保存是否有差异（未保存修改提示用）。</summary>
    public bool HasUnsavedChanges =>
        _baseline is not null && !Same(_baseline, CurrentValues());

    /// <summary>从运行中的管理器读取当前值（再次进入页面时同步外部改动）。</summary>
    public void LoadFromRuntime()
    {
        IdleTimeoutMs = _tcp.IdleTimeoutMs;
        PoseCheckEnabled = _cfg.PoseCheck.Enabled;
        PoseXyToleranceMm = _cfg.PoseCheck.XyToleranceMm;
        PoseRzToleranceDeg = _cfg.PoseCheck.RzToleranceDeg;
        ProcessHealthEnabled = _cfg.ProcessHealth.Enabled;
        ConsecutiveFailLimit = _cfg.ProcessHealth.ConsecutiveFailLimit;
        InhibitOnLimit = _cfg.ProcessHealth.InhibitOnLimit;
        MaxQueueDepth = _vision.MaxQueueDepth;
        MaxConcurrent = _vision.MaxConcurrent;
        TcpBacklog = _tcp.Backlog;
        MaxConnections = _tcp.MaxConnections;
        FailureEnabled = _failures.Enabled;
        FailureRetainedCount = _failures.RetainedCount;
        IpAddress = _cfg.IpAddress;
        TcpPort = _cfg.TcpPort;
        WhitelistText = string.Join(Environment.NewLine, _cfg.IpWhitelist);
        _baseline = CurrentValues();
        RefreshStatus();
    }

    public void StartTimer() => _timer.Start();

    public void StopTimer() => _timer.Stop();

    private void RefreshStatus()
    {
        Status = $"监听 {_tcp.ListenEndPoint} · {(_tcp.IsRunning ? "运行中" : "已停止")} · " +
                 $"当前连接 {_tcp.ConnectedClients} · 累计接入 {_tcp.TotalConnections} · " +
                 $"拒绝 {_tcp.RejectedConnections} · 累计请求 {_tcp.TotalRequests}";
        var configured = string.IsNullOrWhiteSpace(_cfg.Inference.Provider) ? "OpenVinoGpu" : _cfg.Inference.Provider;
        if (_inference is null)
            InferenceStatus = $"推理 {configured}（改 appsettings Inference:Provider 后重启）";
        else if (_inference.GpuUnavailable)
            InferenceStatus = $"推理配置 {configured} · 实际 OpenVINO CPU（GPU 不可用，重启后才再试）";
        else if (string.IsNullOrEmpty(_inference.ActiveDevice))
            InferenceStatus = $"推理配置 {configured} · 尚未加载模型";
        else
            InferenceStatus = $"推理配置 {configured} · 实际 OpenVINO {_inference.ActiveDevice}";
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            this.Commit();
            var values = CurrentValues();

            // 校验集中在 Store（值域 + 相机取图超时联动），非法值抛 InvalidDataException
            // 落盘会同步内存 AppConfig（含 IP/端口），端点比较必须用保存前的基线
            var endpointChanged = _baseline is null
                || !string.Equals(_baseline.IpAddress, values.IpAddress, StringComparison.OrdinalIgnoreCase)
                || _baseline.TcpPort != values.TcpPort;

            _store.Save(values);

            // RuntimeSync 已热应用超时/空闲/队列等；此处再写一遍与基线对齐，失败已在 Save 抛出
            _tcp.TimeoutMs = values.TimeoutMs;
            _tcp.IdleTimeoutMs = values.IdleTimeoutMs;
            _vision.MaxQueueDepth = values.MaxQueueDepth;
            _tcp.MaxConnections = values.MaxConnections;
            _tcp.IpWhitelist = values.IpWhitelist;
            _failures.Enabled = values.FailureEnabled;
            _failures.RetainedCount = values.FailureRetainedCount;
            // 并发槽位/backlog 首次固化或监听启动时读取，运行中修改需重启程序生效
            var restartNeeded = values.MaxConcurrent != _baseline?.MaxConcurrent ||
                                values.TcpBacklog != _baseline?.TcpBacklog;
            var endpointText = $"{values.IpAddress}:{values.TcpPort}";
            if (endpointChanged)
            {
                var ok = _tcp.Restart(values.IpAddress, values.TcpPort);
                Message = ok
                    ? $"已保存并应用；监听已热重启到 {endpointText}（客户端将短暂断开）" + RestartSuffix(restartNeeded)
                    : $"已保存；监听 {endpointText} 启动失败，已回滚到 {_tcp.ListenEndPoint}（请检查端口占用）";
            }
            else
            {
                Message = "已保存并应用" + (restartNeeded
                    ? RestartSuffix(true)
                    : "（可热生效的参数已立即生效；推理 Provider 需改 appsettings 后重启）");
            }

            _baseline = CurrentValues();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            Message = $"保存失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        IdleTimeoutMs = DefaultIdleTimeoutMs;
        PoseCheckEnabled = true;
        PoseXyToleranceMm = 0.5;
        PoseRzToleranceDeg = 0.5;
        ProcessHealthEnabled = true;
        ConsecutiveFailLimit = 5;
        InhibitOnLimit = true;
        MaxQueueDepth = DefaultMaxQueueDepth;
        MaxConcurrent = DefaultMaxConcurrent;
        TcpBacklog = DefaultTcpBacklog;
        MaxConnections = DefaultMaxConnections;
        FailureEnabled = DefaultFailureEnabled;
        FailureRetainedCount = DefaultFailureRetainedCount;
        IpAddress = DefaultIpAddress;
        TcpPort = DefaultTcpPort;
        WhitelistText = "";
        Message = "已填入出厂默认值，点击「保存并应用」生效";
    }

    [RelayCommand]
    private void Reload() => LoadFromRuntime();

    [RelayCommand]
    private void SetIdleNever() => IdleTimeoutMs = 0;

    [RelayCommand]
    private void SetIdleThirtyDays() => IdleTimeoutMs = TcpServerManager.IdleTimeoutThirtyDaysMs;

    [RelayCommand]
    private void OpenSettingsFolder() =>
        Explorer.OpenFolder(System.IO.Path.GetDirectoryName(_store.SettingsPath)!);

    private static string RestartSuffix(bool restartNeeded) =>
        restartNeeded ? "（并发槽位/TCP backlog 修改需重启程序生效）" : "";

    private ServiceSettingsValues CurrentValues()
    {
        var whitelist = WhitelistText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .ToList();
        return new ServiceSettingsValues(
            _cfg.TimeoutMs, MaxQueueDepth, MaxConcurrent, TcpBacklog, MaxConnections,
            FailureEnabled, FailureRetainedCount,
            IpAddress.Trim(), TcpPort, whitelist,
            (long)Math.Round(IdleTimeoutMs),
            PoseCheckEnabled, PoseXyToleranceMm, PoseRzToleranceDeg,
            ProcessHealthEnabled, ConsecutiveFailLimit, InhibitOnLimit);
    }

    private static bool Same(ServiceSettingsValues a, ServiceSettingsValues b) =>
        a.MaxQueueDepth == b.MaxQueueDepth &&
        a.MaxConcurrent == b.MaxConcurrent && a.TcpBacklog == b.TcpBacklog &&
        a.MaxConnections == b.MaxConnections && a.FailureEnabled == b.FailureEnabled &&
        a.FailureRetainedCount == b.FailureRetainedCount &&
        string.Equals(a.IpAddress, b.IpAddress, StringComparison.OrdinalIgnoreCase) &&
        a.TcpPort == b.TcpPort &&
        a.IdleTimeoutMs == b.IdleTimeoutMs &&
        a.PoseCheckEnabled == b.PoseCheckEnabled &&
        Math.Abs(a.PoseXyToleranceMm - b.PoseXyToleranceMm) < 1e-9 &&
        Math.Abs(a.PoseRzToleranceDeg - b.PoseRzToleranceDeg) < 1e-9 &&
        a.ProcessHealthEnabled == b.ProcessHealthEnabled &&
        a.ConsecutiveFailLimit == b.ConsecutiveFailLimit &&
        a.InhibitOnLimit == b.InhibitOnLimit &&
        a.IpWhitelist.SequenceEqual(b.IpWhitelist, StringComparer.OrdinalIgnoreCase);
}
