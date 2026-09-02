using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Settings;

/// <summary>
/// 服务参数管理：运行参数（超时/队列/连接上限/失败留存/白名单）保存即热生效；
/// 并发槽位（MaxConcurrent）与 TCP backlog 修改需重启程序生效；
/// 网络端点（IP/端口）先热重启监听，成功后再落盘（失败则回滚运行时、不写入 appsettings、UI 保持脏标记）。
/// 校验集中在 AppSettingsStore（值域 + 相机取图超时联动），本页只负责展示与交互。
/// </summary>
public partial class SettingsViewModel : ObservableObject, ICommitPendingEdits, IDisposable
{
    // 出厂默认值（与 appsettings.json 初始一致）
    private const long DefaultIdleTimeoutMs = 0;
    private const int DefaultMaxQueueDepth = 4;
    private const int DefaultMaxConcurrent = 2;
    private const int DefaultTcpBacklog = 16;
    private const int DefaultMaxConnections = 0;
    private const bool DefaultFailureEnabled = true;
    private const int DefaultFailureRetainedCount = 200;
    private const int DefaultFailureRetainedDays = 0;
    private const bool DefaultCaptureSuccessEnabled = false;
    private const int DefaultCaptureSuccessRetainedDays = 30;
    private const int DefaultCaptureSuccessMaxWidth = 0;
    private const bool DefaultResultLogEnabled = true;
    private const bool DefaultResultLogJsonl = true;
    private const bool DefaultResultLogSqlite = true;
    private const int DefaultResultLogRetainedDays = 30;
    private const string DefaultInferenceProvider = "OpenVinoGpu";
    private const int DefaultInferenceMaxSessions = 8;
    private const bool DefaultFileLoggingEnabled = true;
    private const int DefaultFileLoggingRetainedDays = 30;
    private const int DefaultProcessHealthRetainedDays = 90;
    private const string DefaultIpAddress = "0.0.0.0";
    private const int DefaultTcpPort = 9999;
    private const string DefaultUiTheme = UiThemes.Dark;

    public IReadOnlyList<UiThemeChoice> UiThemeChoices { get; } =
    [
        new(UiThemes.Dark, "深色"),
        new(UiThemes.Light, "浅色"),
    ];

    private readonly AppConfig _cfg;
    private readonly ITcpRuntime _tcp;
    private readonly VisionService _vision;
    private readonly FailureImageStore _failures;
    private readonly ResultLogStore _results;
    private readonly SuccessCaptureStore _captures;
    private readonly AppSettingsStore _store;
    private readonly IInferenceRuntime? _inference;
    private readonly IDialogService _dialogs;
    private readonly ILogger<SettingsViewModel> _log;
    private readonly DispatcherTimer _timer;

    /// <summary>最近一次载入/保存时的参数快照（脏标记基准）。</summary>
    private ServiceSettingsValues? _baseline;

    public Action? FlushPendingEdits { get; set; }

    [ObservableProperty]
    private double _requestTimeoutMs = AppConfig.DefaultRequestTimeoutMs;

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

    [ObservableProperty]
    private int _failureRetainedCount;

    [ObservableProperty]
    private int _failureRetainedDays;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCaptureSuccessFields))]
    private bool _captureSuccessEnabled;

    [ObservableProperty]
    private int _captureSuccessRetainedDays = DefaultCaptureSuccessRetainedDays;

    [ObservableProperty]
    private int _captureSuccessMaxWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResultLogFields))]
    private bool _resultLogEnabled = true;

    [ObservableProperty]
    private bool _resultLogJsonl = true;

    [ObservableProperty]
    private bool _resultLogSqlite = true;

    [ObservableProperty]
    private int _resultLogRetainedDays = DefaultResultLogRetainedDays;

    public IReadOnlyList<string> InferenceProviderOptions { get; } = ["OpenVinoGpu", "OpenVinoCpu"];

    [ObservableProperty]
    private string _inferenceProvider = DefaultInferenceProvider;

    [ObservableProperty]
    private int _inferenceMaxSessions = DefaultInferenceMaxSessions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFileLoggingFields))]
    private bool _fileLoggingEnabled = DefaultFileLoggingEnabled;

    [ObservableProperty]
    private int _fileLoggingRetainedDays = DefaultFileLoggingRetainedDays;

    [ObservableProperty]
    private int _processHealthRetainedDays = DefaultProcessHealthRetainedDays;

    public bool ShowPoseToleranceFields => PoseCheckEnabled;
    public bool ShowProcessHealthFields => ProcessHealthEnabled;
    public bool ShowFailureRetention => FailureEnabled;
    public bool ShowCaptureSuccessFields => CaptureSuccessEnabled;
    public bool ShowResultLogFields => ResultLogEnabled;
    public bool ShowFileLoggingFields => FileLoggingEnabled;

    public string FailureFolderPath => _cfg.ResolveDataPath(_cfg.FailureImage.Folder);
    public string CaptureSuccessFolderPath => _cfg.ResolveDataPath(_cfg.CaptureSuccess.Folder);
    public string ResultLogFolderPath => _cfg.ResolveDataPath(_cfg.ResultLog.Folder);
    public string FileLoggingFolderPath => _cfg.ResolveDataPath(_cfg.FileLogging.Folder);
    public string ProcessHealthFolderPath => _cfg.ResolveDataPath(_cfg.ProcessHealth.Folder);
    public string DataRootPath =>
        string.IsNullOrWhiteSpace(_cfg.DataRoot)
            ? ApplicationPaths.DefaultDataRoot
            : _cfg.ResolveDataRoot();
    public bool HasDataRoot => !string.IsNullOrWhiteSpace(_cfg.DataRoot);

    [ObservableProperty]
    private string _ipAddress = "";

    [ObservableProperty]
    private int _tcpPort;

    [ObservableProperty]
    private string _whitelistText = "";

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string _inferenceStatus = "";

    [ObservableProperty]
    private string _uiTheme = DefaultUiTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlcDebugFields))]
    private bool _plcDebugAlwaysOk;

    [ObservableProperty]
    private double _plcDebugDefaultX;

    [ObservableProperty]
    private double _plcDebugDefaultY;

    [ObservableProperty]
    private double _plcDebugDefaultRz;

    public bool ShowPlcDebugFields => PlcDebugAlwaysOk;

    partial void OnUiThemeChanged(string value) => AppThemeManager.Apply(value);

    public SettingsViewModel(
        AppConfig cfg,
        ITcpRuntime tcp,
        VisionService vision,
        FailureImageStore failures,
        ResultLogStore results,
        SuccessCaptureStore captures,
        AppSettingsStore store,
        IDialogService dialogs,
        ILogger<SettingsViewModel> log,
        IInferenceRuntime? inference = null)
    {
        _cfg = cfg;
        _tcp = tcp;
        _vision = vision;
        _failures = failures;
        _results = results;
        _captures = captures;
        _store = store;
        _dialogs = dialogs;
        _log = log;
        _inference = inference;

        LoadFromRuntime();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
    }

    public bool HasUnsavedChanges =>
        _baseline is not null && !Same(_baseline, CurrentValues());

    public void LoadFromRuntime()
    {
        RequestTimeoutMs = _cfg.TimeoutMs;
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
        FailureRetainedDays = _failures.RetainedDays;
        CaptureSuccessEnabled = _captures.Enabled;
        CaptureSuccessRetainedDays = _captures.RetainedDays;
        CaptureSuccessMaxWidth = _captures.MaxWidth;
        ResultLogEnabled = _results.Enabled;
        ResultLogJsonl = _results.JsonlEnabled;
        ResultLogSqlite = _results.SqliteEnabled;
        ResultLogRetainedDays = _results.RetainedDays;
        InferenceProvider = string.IsNullOrWhiteSpace(_cfg.Inference.Provider)
            ? DefaultInferenceProvider
            : _cfg.Inference.Provider;
        InferenceMaxSessions = _cfg.Inference.MaxSessions;
        FileLoggingEnabled = _cfg.FileLogging.Enabled;
        FileLoggingRetainedDays = _cfg.FileLogging.RetainedDays;
        ProcessHealthRetainedDays = _cfg.ProcessHealth.RetainedDays;
        IpAddress = _cfg.IpAddress;
        TcpPort = _cfg.TcpPort;
        UiTheme = UiThemes.Normalize(_cfg.UiTheme);
        WhitelistText = string.Join(Environment.NewLine, _cfg.IpWhitelist);
        PlcDebugAlwaysOk = _cfg.PlcDebug.AlwaysOk;
        PlcDebugDefaultX = _cfg.PlcDebug.DefaultX;
        PlcDebugDefaultY = _cfg.PlcDebug.DefaultY;
        PlcDebugDefaultRz = _cfg.PlcDebug.DefaultRz;
        _baseline = CurrentValues();
        RefreshStatus();
        OnPropertyChanged(nameof(FailureFolderPath));
        OnPropertyChanged(nameof(CaptureSuccessFolderPath));
        OnPropertyChanged(nameof(ResultLogFolderPath));
        OnPropertyChanged(nameof(FileLoggingFolderPath));
        OnPropertyChanged(nameof(ProcessHealthFolderPath));
        OnPropertyChanged(nameof(DataRootPath));
        OnPropertyChanged(nameof(HasDataRoot));
    }

    public void StartTimer() => _timer.Start();

    public void StopTimer() => _timer.Stop();

    /// <summary>进程退出时由 DI 容器级联调用（单例 VM）：停止 1 秒状态轮询。</summary>
    public void Dispose() => _timer.Stop();

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
            var rollbackBaseline = _baseline ?? values;

            if (values.PlcDebugAlwaysOk && rollbackBaseline is { PlcDebugAlwaysOk: false } &&
                !_dialogs.ConfirmYesNo(
                    "启用后 TCP 将不再向 PLC 返回 ERR（失败时回设置的默认 OK 坐标）。\n" +
                    "视觉仍会照常采图推理，仅协议线伪装成功。\n\n" +
                    "仅供 PLC 联调，正式产线务必关闭。继续保存？",
                    "PLC 调试模式"))
                return;

            AppSettingsStore.Validate(values);

            var endpointChanged = !string.Equals(rollbackBaseline.IpAddress, values.IpAddress, StringComparison.OrdinalIgnoreCase)
                || rollbackBaseline.TcpPort != values.TcpPort;
            var restartNeeded = values.MaxConcurrent != rollbackBaseline.MaxConcurrent ||
                                values.TcpBacklog != rollbackBaseline.TcpBacklog;
            var restartForConfig = NeedsProgramRestart(rollbackBaseline, values);
            var endpointText = $"{values.IpAddress}:{values.TcpPort}";

            ApplyHotRuntime(values);

            if (endpointChanged && !_tcp.Restart(values.IpAddress, values.TcpPort))
            {
                ApplyHotRuntime(rollbackBaseline);
                LoadFromRuntime();
                Message = $"监听 {endpointText} 启动失败，未保存（当前仍监听 {_tcp.ListenEndPoint}，请检查端口占用）";
                return;
            }

            try
            {
                _store.Save(values);
            }
            catch
            {
                ApplyHotRuntime(rollbackBaseline);
                if (endpointChanged)
                    _tcp.Restart(rollbackBaseline.IpAddress, rollbackBaseline.TcpPort);
                throw;
            }

            if (endpointChanged)
            {
                Message = $"已保存并应用；监听已热重启到 {endpointText}（客户端将短暂断开）"
                          + RestartSuffix(restartNeeded, restartForConfig);
            }
            else
            {
                Message = "已保存并应用" + RestartSuffix(restartNeeded, restartForConfig);
            }

            _baseline = CurrentValues();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            LoadFromRuntime();
            Message = $"保存失败: {ex.Message}";
        }
    }

    /// <summary>把可热生效的运行时参数同步到管理器（落盘前试探性应用；失败时由调用方回滚）。</summary>
    private void ApplyHotRuntime(ServiceSettingsValues values)
    {
        _tcp.TimeoutMs = values.TimeoutMs;
        _tcp.IdleTimeoutMs = values.IdleTimeoutMs;
        _vision.MaxQueueDepth = values.MaxQueueDepth;
        _tcp.MaxConnections = values.MaxConnections;
        _tcp.IpWhitelist = values.IpWhitelist;
        _tcp.PlcAlwaysOkMode = values.PlcDebugAlwaysOk;
        _tcp.PlcDebugDefaultX = values.PlcDebugDefaultX;
        _tcp.PlcDebugDefaultY = values.PlcDebugDefaultY;
        _tcp.PlcDebugDefaultRz = values.PlcDebugDefaultRz;
        _failures.Enabled = values.FailureEnabled;
        _failures.RetainedCount = values.FailureRetainedCount;
        _failures.RetainedDays = values.FailureRetainedDays;
        _results.ApplyConfig(new ResultLogConfig
        {
            Enabled = values.ResultLogEnabled,
            Jsonl = values.ResultLogJsonl,
            Sqlite = values.ResultLogSqlite,
            RetainedDays = values.ResultLogRetainedDays,
            Folder = _cfg.ResultLog.Folder,
        });
        _captures.ApplyConfig(new CaptureSuccessConfig
        {
            Enabled = values.CaptureSuccessEnabled,
            RetainedDays = values.CaptureSuccessRetainedDays,
            MaxWidth = values.CaptureSuccessMaxWidth,
            Folder = _cfg.CaptureSuccess.Folder,
        });
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        RequestTimeoutMs = AppConfig.DefaultRequestTimeoutMs;
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
        FailureRetainedDays = DefaultFailureRetainedDays;
        CaptureSuccessEnabled = DefaultCaptureSuccessEnabled;
        CaptureSuccessRetainedDays = DefaultCaptureSuccessRetainedDays;
        CaptureSuccessMaxWidth = DefaultCaptureSuccessMaxWidth;
        ResultLogEnabled = DefaultResultLogEnabled;
        ResultLogJsonl = DefaultResultLogJsonl;
        ResultLogSqlite = DefaultResultLogSqlite;
        ResultLogRetainedDays = DefaultResultLogRetainedDays;
        InferenceProvider = DefaultInferenceProvider;
        InferenceMaxSessions = DefaultInferenceMaxSessions;
        FileLoggingEnabled = DefaultFileLoggingEnabled;
        FileLoggingRetainedDays = DefaultFileLoggingRetainedDays;
        ProcessHealthRetainedDays = DefaultProcessHealthRetainedDays;
        IpAddress = DefaultIpAddress;
        TcpPort = DefaultTcpPort;
        UiTheme = DefaultUiTheme;
        WhitelistText = "";
        PlcDebugAlwaysOk = false;
        PlcDebugDefaultX = 0;
        PlcDebugDefaultY = 0;
        PlcDebugDefaultRz = 0;
        Message = "已填入出厂默认值，点击「保存并应用」生效";
    }

    [RelayCommand]
    private void Reload() => LoadFromRuntime();

    [RelayCommand]
    private void SetIdleNever() => IdleTimeoutMs = 0;

    [RelayCommand]
    private void SetIdleThirtyDays() => IdleTimeoutMs = ITcpRuntime.IdleTimeoutThirtyDaysMs;

    [RelayCommand]
    private void OpenDataRoot() =>
        Explorer.OpenFolder(HasDataRoot ? _cfg.ResolveDataRoot() : AppContext.BaseDirectory);

    [RelayCommand]
    private void OpenSettingsFolder() =>
        Explorer.OpenFolder(System.IO.Path.GetDirectoryName(_store.SettingsPath)!);

    [RelayCommand]
    private void OpenFailureFolder() => Explorer.OpenFolder(FailureFolderPath);

    [RelayCommand]
    private void OpenCaptureSuccessFolder() => Explorer.OpenFolder(CaptureSuccessFolderPath);

    [RelayCommand]
    private void OpenResultLogFolder() => Explorer.OpenFolder(ResultLogFolderPath);

    [RelayCommand]
    private void OpenFileLoggingFolder() => Explorer.OpenFolder(FileLoggingFolderPath);

    [RelayCommand]
    private void OpenProcessHealthFolder() => Explorer.OpenFolder(ProcessHealthFolderPath);

    private static bool NeedsProgramRestart(ServiceSettingsValues? baseline, ServiceSettingsValues values)
    {
        if (baseline is null)
            return false;
        return !string.Equals(baseline.InferenceProvider, values.InferenceProvider, StringComparison.OrdinalIgnoreCase)
               || baseline.InferenceMaxSessions != values.InferenceMaxSessions
               || baseline.FileLoggingEnabled != values.FileLoggingEnabled
               || baseline.FileLoggingRetainedDays != values.FileLoggingRetainedDays;
    }

    private static string RestartSuffix(bool restartNeeded, bool restartForConfig)
    {
        if (restartNeeded && restartForConfig)
            return "（并发槽位/TCP backlog、推理 Provider/会话上限、文件日志改动需重启程序生效）";
        if (restartNeeded)
            return "（并发槽位/TCP backlog 修改需重启程序生效）";
        if (restartForConfig)
            return "（推理 Provider/会话上限、文件日志改动需重启程序生效）";
        return "";
    }

    private ServiceSettingsValues CurrentValues()
    {
        var whitelist = WhitelistText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .ToList();
        return new ServiceSettingsValues(
            (int)Math.Round(RequestTimeoutMs),
            MaxQueueDepth, MaxConcurrent, TcpBacklog, MaxConnections,
            FailureEnabled, FailureRetainedCount,
            IpAddress.Trim(), TcpPort, whitelist,
            (long)Math.Round(IdleTimeoutMs),
            PoseCheckEnabled, PoseXyToleranceMm, PoseRzToleranceDeg,
            ProcessHealthEnabled, ConsecutiveFailLimit, InhibitOnLimit,
            FailureRetainedDays,
            CaptureSuccessEnabled, CaptureSuccessRetainedDays, CaptureSuccessMaxWidth,
            ResultLogEnabled, ResultLogJsonl, ResultLogSqlite, ResultLogRetainedDays,
            InferenceProvider, InferenceMaxSessions,
            FileLoggingEnabled, FileLoggingRetainedDays,
            ProcessHealthRetainedDays,
            UiTheme,
            PlcDebugAlwaysOk, PlcDebugDefaultX, PlcDebugDefaultY, PlcDebugDefaultRz);
    }

    private static bool Same(ServiceSettingsValues a, ServiceSettingsValues b) =>
        a.TimeoutMs == b.TimeoutMs &&
        a.MaxQueueDepth == b.MaxQueueDepth &&
        a.MaxConcurrent == b.MaxConcurrent && a.TcpBacklog == b.TcpBacklog &&
        a.MaxConnections == b.MaxConnections && a.FailureEnabled == b.FailureEnabled &&
        a.FailureRetainedCount == b.FailureRetainedCount &&
        a.FailureRetainedDays == b.FailureRetainedDays &&
        a.CaptureSuccessEnabled == b.CaptureSuccessEnabled &&
        a.CaptureSuccessRetainedDays == b.CaptureSuccessRetainedDays &&
        a.CaptureSuccessMaxWidth == b.CaptureSuccessMaxWidth &&
        a.ResultLogEnabled == b.ResultLogEnabled &&
        a.ResultLogJsonl == b.ResultLogJsonl &&
        a.ResultLogSqlite == b.ResultLogSqlite &&
        a.ResultLogRetainedDays == b.ResultLogRetainedDays &&
        string.Equals(a.InferenceProvider, b.InferenceProvider, StringComparison.OrdinalIgnoreCase) &&
        a.InferenceMaxSessions == b.InferenceMaxSessions &&
        a.FileLoggingEnabled == b.FileLoggingEnabled &&
        a.FileLoggingRetainedDays == b.FileLoggingRetainedDays &&
        a.ProcessHealthRetainedDays == b.ProcessHealthRetainedDays &&
        string.Equals(UiThemes.Normalize(a.UiTheme), UiThemes.Normalize(b.UiTheme), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.IpAddress, b.IpAddress, StringComparison.OrdinalIgnoreCase) &&
        a.TcpPort == b.TcpPort &&
        a.IdleTimeoutMs == b.IdleTimeoutMs &&
        a.PoseCheckEnabled == b.PoseCheckEnabled &&
        Math.Abs(a.PoseXyToleranceMm - b.PoseXyToleranceMm) < 1e-9 &&
        Math.Abs(a.PoseRzToleranceDeg - b.PoseRzToleranceDeg) < 1e-9 &&
        a.ProcessHealthEnabled == b.ProcessHealthEnabled &&
        a.ConsecutiveFailLimit == b.ConsecutiveFailLimit &&
        a.InhibitOnLimit == b.InhibitOnLimit &&
        a.PlcDebugAlwaysOk == b.PlcDebugAlwaysOk &&
        Math.Abs(a.PlcDebugDefaultX - b.PlcDebugDefaultX) < 1e-9 &&
        Math.Abs(a.PlcDebugDefaultY - b.PlcDebugDefaultY) < 1e-9 &&
        Math.Abs(a.PlcDebugDefaultRz - b.PlcDebugDefaultRz) < 1e-9 &&
        a.IpWhitelist.SequenceEqual(b.IpWhitelist, StringComparer.OrdinalIgnoreCase);
}

public sealed record UiThemeChoice(string Key, string Label);
