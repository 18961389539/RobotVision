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
/// 网络端点（IP/端口）先热重启监听，成功后再落盘（失败则回滚运行时、不写入 appsettings）。
/// 校验集中在 AppSettingsStore（值域 + 相机取图超时联动），本页只负责展示与交互。
///
/// 保存失败时**不再重载表单**：用户填的一屏内容原样保留，只把出错字段与所属分组标出来
/// （见 SettingsViewModel.Groups.cs 的 ShowError），避免「改了端口想重试却要从头再填一遍」。
/// 脏状态、分组锚点与「需重启」留痕同样在 Groups 分部中实现。
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
    private const int DefaultFailureRetainedDays = 2;
    private const bool DefaultCaptureSuccessEnabled = false;
    private const int DefaultCaptureSuccessRetainedDays = 2;
    private const int DefaultCaptureSuccessMaxWidth = 0;
    private const bool DefaultResultLogEnabled = true;
    private const bool DefaultResultLogJsonl = true;
    private const bool DefaultResultLogSqlite = true;
    private const int DefaultResultLogRetainedDays = 30;
    private const bool DefaultRetryEnabled = true;
    private const int DefaultRetryMaxAttempts = 3;
    private const int DefaultRetryDelayMs = 200;
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
    [NotifyPropertyChangedFor(nameof(ShowFailureRetention))]
    private bool _failureSaveOverlay;

    [ObservableProperty]
    private int _failureRetainedCount;

    [ObservableProperty]
    private int _failureRetainedDays;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCaptureSuccessFields))]
    private bool _captureSuccessEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCaptureSuccessFields))]
    private bool _captureSuccessSaveOverlay;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRetryFields))]
    private bool _retryEnabled = DefaultRetryEnabled;

    [ObservableProperty]
    private int _retryMaxAttempts = DefaultRetryMaxAttempts;

    [ObservableProperty]
    private int _retryDelayMs = DefaultRetryDelayMs;

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
    public bool ShowFailureRetention => FailureEnabled || FailureSaveOverlay;
    public bool ShowCaptureSuccessFields => CaptureSuccessEnabled || CaptureSuccessSaveOverlay;
    public bool ShowResultLogFields => ResultLogEnabled;
    public bool ShowRetryFields => RetryEnabled;
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
        _suspendRecompute = true;
        try
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
            FailureSaveOverlay = _failures.SaveOverlay;
            FailureRetainedCount = _failures.RetainedCount;
            FailureRetainedDays = _failures.RetainedDays;
            CaptureSuccessEnabled = _captures.Enabled;
            CaptureSuccessSaveOverlay = _captures.SaveOverlay;
            CaptureSuccessRetainedDays = _captures.RetainedDays;
            CaptureSuccessMaxWidth = _captures.MaxWidth;
            ResultLogEnabled = _results.Enabled;
            ResultLogJsonl = _results.JsonlEnabled;
            ResultLogSqlite = _results.SqliteEnabled;
            ResultLogRetainedDays = _results.RetainedDays;
            RetryEnabled = _cfg.Retry.Enabled;
            RetryMaxAttempts = _cfg.Retry.MaxAttempts;
            RetryDelayMs = _cfg.Retry.DelayMs;
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

            // 表单已回到运行时值：基线重置、错误与「需重启」留痕一并清空
            _baseline = CurrentValues();
            ClearError();
            _savedRestartLabels = [];
            RefreshStatus();
        }
        finally
        {
            _suspendRecompute = false;
        }

        RecomputeDirtyState();
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
            ClearError();
            var values = CurrentValues();
            var rollbackBaseline = _baseline ?? values;

            if (values.PlcDebugAlwaysOk && rollbackBaseline is { PlcDebugAlwaysOk: false } &&
                !_dialogs.ConfirmYesNo(
                    "启用后 TCP 将不再向 PLC 返回 ERR（失败时回设置的默认 OK 坐标）。\n" +
                    "视觉仍会照常采图推理，仅协议线伪装成功。\n\n" +
                    "仅供 PLC 联调，正式产线务必关闭。继续保存？",
                    "PLC 调试模式"))
                return;

            // 校验不过时只标记出错字段并保留用户输入（不再重载表单清空整屏编辑），
            // 用户可直接修正后重试；分组自动展开与滚动见 ShowError
            if (_store.TryValidate(values) is { } invalid)
            {
                ShowError(invalid.Field, invalid.Message);
                return;
            }

            var endpointChanged = !string.Equals(rollbackBaseline.IpAddress, values.IpAddress, StringComparison.OrdinalIgnoreCase)
                || rollbackBaseline.TcpPort != values.TcpPort;
            var restartNeeded = values.MaxConcurrent != rollbackBaseline.MaxConcurrent ||
                                values.TcpBacklog != rollbackBaseline.TcpBacklog;
            var restartForConfig = NeedsProgramRestart(rollbackBaseline, values);
            var endpointText = $"{values.IpAddress}:{values.TcpPort}";
            var restartLabels = CurrentRestartLabels();
            var hadChanges = _dirtyFields.Count > 0;

            ApplyHotRuntime(values);

            if (endpointChanged && !_tcp.Restart(values.IpAddress, values.TcpPort))
            {
                // 运行时回滚到旧监听，但表单保留用户填的端点，便于改端口后直接重试
                ApplyHotRuntime(rollbackBaseline);
                ShowError(SettingsField.TcpPort,
                    $"监听 {endpointText} 启动失败，未保存（当前仍监听 {_tcp.ListenEndPoint}，请检查端口占用）");
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

            // 保存成功：把待重启项转为底栏常驻留痕（Message 里的那一次会被后续轮询/操作冲掉）。
            // 无改动的重复点击不清空留痕，避免「保存过重启项后又点一下保存」把提醒弄丢。
            if (restartLabels.Count > 0)
                _savedRestartLabels = restartLabels;
            else if (hadChanges)
                _savedRestartLabels = [];

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
            RecomputeDirtyState();
        }
        catch (Exception ex)
        {
            // 落盘/IO 类失败：保留用户输入与已回滚的运行时状态，只提示原因
            ShowError("", ex.Message);
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
        _failures.SaveOverlay = values.FailureSaveOverlay;
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
            SaveOverlay = values.CaptureSuccessSaveOverlay,
            RetainedDays = values.CaptureSuccessRetainedDays,
            MaxWidth = values.CaptureSuccessMaxWidth,
            Folder = _cfg.CaptureSuccess.Folder,
        });
        _vision.ApplyRetry(new RetryConfig
        {
            Enabled = values.RetryEnabled,
            MaxAttempts = values.RetryMaxAttempts,
            DelayMs = values.RetryDelayMs,
            ErrorCodes = [.. _cfg.Retry.ErrorCodes],
        });
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        // 一键清空全部编辑（含监听 IP/端口、白名单、主题），必须二次确认：
        // 之前它与「重新载入」并列在服务状态卡里，误点即丢失整屏编辑
        if (!_dialogs.ConfirmYesNo(
                "将把本页所有参数填回出厂默认值（含监听 IP/端口、IP 白名单、界面主题），" +
                "并丢弃当前未保存的编辑。\n\n填入后仍需点「保存并应用」才会真正生效。继续？",
                "恢复出厂默认值"))
            return;

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
        FailureSaveOverlay = false;
        FailureRetainedCount = DefaultFailureRetainedCount;
        FailureRetainedDays = DefaultFailureRetainedDays;
        CaptureSuccessEnabled = DefaultCaptureSuccessEnabled;
        CaptureSuccessSaveOverlay = false;
        CaptureSuccessRetainedDays = DefaultCaptureSuccessRetainedDays;
        CaptureSuccessMaxWidth = DefaultCaptureSuccessMaxWidth;
        ResultLogEnabled = DefaultResultLogEnabled;
        ResultLogJsonl = DefaultResultLogJsonl;
        ResultLogSqlite = DefaultResultLogSqlite;
        ResultLogRetainedDays = DefaultResultLogRetainedDays;
        RetryEnabled = DefaultRetryEnabled;
        RetryMaxAttempts = DefaultRetryMaxAttempts;
        RetryDelayMs = DefaultRetryDelayMs;
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
        ClearError();
        _savedRestartLabels = [];
        Message = "已填入出厂默认值，点击「保存并应用」生效";
    }

    /// <summary>放弃未保存的修改，回到上一次载入/保存的状态。</summary>
    [RelayCommand]
    private void Discard()
    {
        if (!HasUnsavedChanges)
            return;
        if (!_dialogs.ConfirmDiscard(
                "将丢弃本页所有未保存的修改，回到上一次载入或保存的状态。继续？",
                "放弃改动"))
            return;

        LoadFromRuntime();
        Message = "已放弃未保存的修改";
    }

    [RelayCommand]
    private void Reload()
    {
        LoadFromRuntime();
        Message = "已重新载入运行时参数";
    }

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

    /// <summary>白名单文本框 → 条目列表（去空行、去首尾空白）。脏状态比对与保存共用同一口径。</summary>
    private List<string> WhitelistEntries() =>
        WhitelistText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .ToList();

    private ServiceSettingsValues CurrentValues()
    {
        var whitelist = WhitelistEntries();
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
            PlcDebugAlwaysOk, PlcDebugDefaultX, PlcDebugDefaultY, PlcDebugDefaultRz,
            FailureSaveOverlay, CaptureSuccessSaveOverlay,
            RetryEnabled, RetryMaxAttempts, RetryDelayMs);
    }

    private static bool Same(ServiceSettingsValues a, ServiceSettingsValues b) =>
        a.TimeoutMs == b.TimeoutMs &&
        a.MaxQueueDepth == b.MaxQueueDepth &&
        a.MaxConcurrent == b.MaxConcurrent && a.TcpBacklog == b.TcpBacklog &&
        a.MaxConnections == b.MaxConnections &&
        a.FailureEnabled == b.FailureEnabled &&
        a.FailureSaveOverlay == b.FailureSaveOverlay &&
        a.FailureRetainedCount == b.FailureRetainedCount &&
        a.FailureRetainedDays == b.FailureRetainedDays &&
        a.CaptureSuccessEnabled == b.CaptureSuccessEnabled &&
        a.CaptureSuccessSaveOverlay == b.CaptureSuccessSaveOverlay &&
        a.CaptureSuccessRetainedDays == b.CaptureSuccessRetainedDays &&
        a.CaptureSuccessMaxWidth == b.CaptureSuccessMaxWidth &&
        a.ResultLogEnabled == b.ResultLogEnabled &&
        a.ResultLogJsonl == b.ResultLogJsonl &&
        a.ResultLogSqlite == b.ResultLogSqlite &&
        a.ResultLogRetainedDays == b.ResultLogRetainedDays &&
        a.RetryEnabled == b.RetryEnabled &&
        a.RetryMaxAttempts == b.RetryMaxAttempts &&
        a.RetryDelayMs == b.RetryDelayMs &&
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
