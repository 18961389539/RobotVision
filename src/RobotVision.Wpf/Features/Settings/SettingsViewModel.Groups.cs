using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.Settings;

/// <summary>
/// 服务设置页的分组 / 脏状态 / 校验定位部分（partial）。
///
/// 设计要点：
/// 1. 脏状态与「是否有未保存改动」是两套独立推导。安全判断仍用 <c>HasUnsavedChanges</c>
///    （走 record 全字段比对 <c>Same</c>，不依赖本文件的字段清单）；本文件的 <c>ComputeDirtyFields</c>
///    只用于徽章计数、锚点提示点与「需重启」留痕 —— 即使清单漏项，也只会少显示一个提示点，
///    不会让未保存的改动被误判成已保存。
/// 2. 任何属性变更都会重算，<c>_recomputing</c> 防重入，<c>_suspendRecompute</c> 让 LoadFromRuntime
///    批量赋值期间不反复重算。
/// 3. 校验失败时 <see cref="ErrorField"/> 记下出错字段，页面通过索引器绑定（<c>{Binding [MaxConcurrent]}</c>）
///    把错误落到对应标签上，并自动展开所属分组、把锚点切过去（页面据此滚动）。
/// </summary>
public partial class SettingsViewModel
{
    private HashSet<string> _dirtyFields = new(StringComparer.Ordinal);
    private HashSet<string> _dirtyGroups = new(StringComparer.Ordinal);
    private bool _recomputing;
    private bool _suspendRecompute;

    /// <summary>上次保存时被判定为「需重启才生效」的参数名（保存后仍留痕，直到重载或清空）。</summary>
    private List<string> _savedRestartLabels = [];

    /// <summary>左侧锚点栏数据源（顺序即页面自上而下的分组顺序）。</summary>
    public ObservableCollection<SettingsAnchor> Anchors { get; } =
    [
        new(SettingsGroup.Appearance, "界面外观"),
        new(SettingsGroup.RunParams, "运行参数"),
        new(SettingsGroup.Pose, "拍照位姿"),
        new(SettingsGroup.PlcDebug, "PLC 调试"),
        new(SettingsGroup.Retention, "现场留存"),
        new(SettingsGroup.ResultLog, "结果留档"),
        new(SettingsGroup.FileLog, "运行日志"),
        new(SettingsGroup.Health, "过程能力"),
        new(SettingsGroup.Whitelist, "IP 白名单"),
        new(SettingsGroup.Endpoint, "网络端点"),
    ];

    /// <summary>
    /// 当前选中的锚点。点击时页面滚动到对应分组；校验失败时由 <see cref="ShowError"/> 赋值，
    /// 页面收到选中变更后自动滚动过去（滚动逻辑集中在页面 SelectionChanged 一处）。
    /// </summary>
    [ObservableProperty] private SettingsAnchor? _selectedAnchor;

    // ---- 分组展开状态（错误定位时自动展开；用户手动折叠后保持折叠）----

    [ObservableProperty] private bool _isAppearanceExpanded = true;

    [ObservableProperty] private bool _isRunParamsExpanded = true;

    [ObservableProperty] private bool _isPoseExpanded = true;

    [ObservableProperty] private bool _isPlcDebugExpanded;

    [ObservableProperty] private bool _isRetentionExpanded = true;

    [ObservableProperty] private bool _isSuccessExpanded;

    [ObservableProperty] private bool _isRetryExpanded;

    [ObservableProperty] private bool _isResultLogExpanded = true;

    [ObservableProperty] private bool _isFileLogExpanded;

    [ObservableProperty] private bool _isHealthExpanded = true;

    [ObservableProperty] private bool _isWhitelistExpanded;

    [ObservableProperty] private bool _isEndpointExpanded;

    // ---- 校验定位结果 ----

    /// <summary>出错字段标识（空 = 当前无错误）。取值见 <see cref="SettingsField"/>。</summary>
    [ObservableProperty] private string _errorField = "";

    /// <summary>出错原因（不含「保存失败:」前缀，供底栏红字提示）。</summary>
    [ObservableProperty] private string _errorMessage = "";

    /// <summary>出错字段所属分组（空 = 无错误，或错误与具体分组无关，如落盘 IO 失败）。</summary>
    public string ErrorGroup => ErrorField.Length == 0 ? "" : GroupOf(ErrorField) ?? "";

    public bool HasError => ErrorField.Length > 0;

    /// <summary>字段级错误查询：XAML 用 <c>{Binding [MaxConcurrent]}</c> 命中该字段所属标签。</summary>
    public bool this[string field] => ErrorField.Length > 0 && ErrorField == field;

    // ---- 底栏状态 ----

    /// <summary>是否有未保存改动（与 <see cref="HasUnsavedChanges"/> 同义，供 XAML 直读）。</summary>
    public bool IsDirty => HasUnsavedChanges;

    /// <summary>底栏徽章文案。</summary>
    [ObservableProperty] private string _dirtySummary = "无未保存改动";

    /// <summary>底栏常驻的「需重启」提示；无需重启时为空字符串。</summary>
    [ObservableProperty] private string _pendingRestartSummary = "";

    /// <summary>「放弃改动」按钮可用性。</summary>
    public bool CanDiscard => HasUnsavedChanges;

    // ---- 分组长提示点（锚点栏黄/红点）----

    public bool IsAppearanceDirty => _dirtyGroups.Contains(SettingsGroup.Appearance);

    public bool IsRunParamsDirty => _dirtyGroups.Contains(SettingsGroup.RunParams);

    public bool IsPoseDirty => _dirtyGroups.Contains(SettingsGroup.Pose);

    public bool IsPlcDebugDirty => _dirtyGroups.Contains(SettingsGroup.PlcDebug);

    public bool IsRetentionDirty => _dirtyGroups.Contains(SettingsGroup.Retention);

    public bool IsResultLogDirty => _dirtyGroups.Contains(SettingsGroup.ResultLog);

    public bool IsFileLogDirty => _dirtyGroups.Contains(SettingsGroup.FileLog);

    public bool IsHealthDirty => _dirtyGroups.Contains(SettingsGroup.Health);

    public bool IsWhitelistDirty => _dirtyGroups.Contains(SettingsGroup.Whitelist);

    public bool IsEndpointDirty => _dirtyGroups.Contains(SettingsGroup.Endpoint);

    /// <summary>由脏字段集合推出所属分组集合。</summary>
    private static HashSet<string> MapGroups(HashSet<string> fields)
    {
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (GroupOf(field) is { Length: > 0 } group)
                groups.Add(group);
        }

        return groups;
    }

    /// <summary>字段 → 分组；未登记的字段返回 null（仅影响提示点，不影响保存安全判断）。</summary>
    public static string? GroupOf(string field) =>
        FieldToGroup.TryGetValue(field, out var group) ? group : null;

    private static readonly Dictionary<string, string> FieldToGroup =
        new(StringComparer.Ordinal)
        {
            [SettingsField.UiTheme] = SettingsGroup.Appearance,
            [SettingsField.Timeout] = SettingsGroup.RunParams,
            [SettingsField.IdleTimeout] = SettingsGroup.RunParams,
            [SettingsField.MaxQueueDepth] = SettingsGroup.RunParams,
            [SettingsField.MaxConcurrent] = SettingsGroup.RunParams,
            [SettingsField.TcpBacklog] = SettingsGroup.RunParams,
            [SettingsField.MaxConnections] = SettingsGroup.RunParams,
            [SettingsField.InferenceProvider] = SettingsGroup.RunParams,
            [SettingsField.InferenceMaxSessions] = SettingsGroup.RunParams,
            [SettingsField.PoseTolerance] = SettingsGroup.Pose,
            [SettingsField.PlcDebugCoordinates] = SettingsGroup.PlcDebug,
            [SettingsField.FailureRetainedCount] = SettingsGroup.Retention,
            [SettingsField.FailureRetainedDays] = SettingsGroup.Retention,
            [SettingsField.CaptureSuccessRetainedDays] = SettingsGroup.Retention,
            [SettingsField.CaptureSuccessMaxWidth] = SettingsGroup.Retention,
            [SettingsField.RetryMaxAttempts] = SettingsGroup.Retention,
            [SettingsField.RetryDelayMs] = SettingsGroup.Retention,
            [SettingsField.ResultLogRetainedDays] = SettingsGroup.ResultLog,
            [SettingsField.ResultLogSink] = SettingsGroup.ResultLog,
            [SettingsField.FileLoggingRetainedDays] = SettingsGroup.FileLog,
            [SettingsField.FileLoggingEnabled] = SettingsGroup.FileLog,
            [SettingsField.ConsecutiveFailLimit] = SettingsGroup.Health,
            [SettingsField.ProcessHealthRetainedDays] = SettingsGroup.Health,
            [SettingsField.IpWhitelist] = SettingsGroup.Whitelist,
            [SettingsField.TcpPort] = SettingsGroup.Endpoint,
            [SettingsField.IpAddress] = SettingsGroup.Endpoint,
        };

    /// <summary>改动后须重启程序才生效的参数（底栏留痕用；与 <c>NeedsProgramRestart</c> 判定范围一致）。</summary>
    private static readonly (string Field, string Label)[] RestartRequiredFields =
    [
        (SettingsField.MaxConcurrent, "并发执行上限"),
        (SettingsField.TcpBacklog, "监听 backlog"),
        (SettingsField.InferenceProvider, "推理 Provider"),
        (SettingsField.InferenceMaxSessions, "推理会话上限"),
        (SettingsField.FileLoggingEnabled, "文件日志开关"),
        (SettingsField.FileLoggingRetainedDays, "文件日志保留天数"),
    ];

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_suspendRecompute || _recomputing || e.PropertyName is null)
            return;

        // 轮询刷新的状态行与一次性提示不参与脏状态推导，避免每秒无谓重算
        if (e.PropertyName is nameof(Status) or nameof(InferenceStatus) or nameof(Message))
            return;

        RecomputeDirtyState();
    }

    /// <summary>重算脏字段 / 徽章文案 / 锚点提示点，并通知相关绑定。</summary>
    private void RecomputeDirtyState()
    {
        if (_recomputing)
            return;

        _recomputing = true;
        try
        {
            _dirtyFields = ComputeDirtyFields();
            _dirtyGroups = MapGroups(_dirtyFields);

            DirtySummary = _dirtyFields.Count > 0
                ? $"{_dirtyFields.Count} 项未保存"
                : HasUnsavedChanges ? "有未保存改动" : "无未保存改动";

            PendingRestartSummary = BuildRestartSummary();

            foreach (var anchor in Anchors)
            {
                anchor.IsDirty = _dirtyGroups.Contains(anchor.Key);
                anchor.HasError = ErrorGroup.Length > 0 && ErrorGroup == anchor.Key;
            }

            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(CanDiscard));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorGroup));
            OnPropertyChanged(nameof(IsAppearanceDirty));
            OnPropertyChanged(nameof(IsRunParamsDirty));
            OnPropertyChanged(nameof(IsPoseDirty));
            OnPropertyChanged(nameof(IsPlcDebugDirty));
            OnPropertyChanged(nameof(IsRetentionDirty));
            OnPropertyChanged(nameof(IsResultLogDirty));
            OnPropertyChanged(nameof(IsFileLogDirty));
            OnPropertyChanged(nameof(IsHealthDirty));
            OnPropertyChanged(nameof(IsWhitelistDirty));
            OnPropertyChanged(nameof(IsEndpointDirty));

            // 索引器绑定的标准通知键：让所有 {Binding [字段名]} 重新求值
            OnPropertyChanged("Item[]");
        }
        finally
        {
            _recomputing = false;
        }
    }

    /// <summary>
    /// 与基线逐字段比对，返回发生变化的字段标识集合。
    /// 判定口径与 <c>CurrentValues()</c> 一致（如 IP 大小写不敏感、白名单按解析后的条目比对）。
    /// </summary>
    private HashSet<string> ComputeDirtyFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        if (_baseline is not { } b)
            return fields;

        void Add(string field, bool changed)
        {
            if (changed)
                fields.Add(field);
        }

        Add(SettingsField.UiTheme,
            !string.Equals(UiThemes.Normalize(UiTheme), UiThemes.Normalize(b.UiTheme), StringComparison.OrdinalIgnoreCase));

        Add(SettingsField.Timeout, RequestTimeoutMs != b.TimeoutMs);
        Add(SettingsField.IdleTimeout, (long)Math.Round(IdleTimeoutMs) != b.IdleTimeoutMs);
        Add(SettingsField.MaxQueueDepth, MaxQueueDepth != b.MaxQueueDepth);
        Add(SettingsField.MaxConcurrent, MaxConcurrent != b.MaxConcurrent);
        Add(SettingsField.TcpBacklog, TcpBacklog != b.TcpBacklog);
        Add(SettingsField.MaxConnections, MaxConnections != b.MaxConnections);
        Add(SettingsField.InferenceProvider,
            !string.Equals(InferenceProvider, b.InferenceProvider, StringComparison.OrdinalIgnoreCase));
        Add(SettingsField.InferenceMaxSessions, InferenceMaxSessions != b.InferenceMaxSessions);

        Add(SettingsField.PoseTolerance,
            PoseCheckEnabled != b.PoseCheckEnabled ||
            Math.Abs(PoseXyToleranceMm - b.PoseXyToleranceMm) > 1e-9 ||
            Math.Abs(PoseRzToleranceDeg - b.PoseRzToleranceDeg) > 1e-9);

        Add(SettingsField.PlcDebugCoordinates,
            PlcDebugAlwaysOk != b.PlcDebugAlwaysOk ||
            Math.Abs(PlcDebugDefaultX - b.PlcDebugDefaultX) > 1e-9 ||
            Math.Abs(PlcDebugDefaultY - b.PlcDebugDefaultY) > 1e-9 ||
            Math.Abs(PlcDebugDefaultRz - b.PlcDebugDefaultRz) > 1e-9);

        Add(SettingsField.FailureRetainedCount,
            FailureEnabled != b.FailureEnabled ||
            FailureSaveOverlay != b.FailureSaveOverlay ||
            FailureRetainedCount != b.FailureRetainedCount);
        Add(SettingsField.FailureRetainedDays, FailureRetainedDays != b.FailureRetainedDays);
        Add(SettingsField.CaptureSuccessRetainedDays,
            CaptureSuccessEnabled != b.CaptureSuccessEnabled ||
            CaptureSuccessSaveOverlay != b.CaptureSuccessSaveOverlay ||
            CaptureSuccessRetainedDays != b.CaptureSuccessRetainedDays);
        Add(SettingsField.CaptureSuccessMaxWidth, CaptureSuccessMaxWidth != b.CaptureSuccessMaxWidth);

        Add(SettingsField.ResultLogSink,
            ResultLogEnabled != b.ResultLogEnabled ||
            ResultLogJsonl != b.ResultLogJsonl ||
            ResultLogSqlite != b.ResultLogSqlite);
        Add(SettingsField.ResultLogRetainedDays, ResultLogRetainedDays != b.ResultLogRetainedDays);

        Add(SettingsField.FileLoggingEnabled, FileLoggingEnabled != b.FileLoggingEnabled);
        Add(SettingsField.FileLoggingRetainedDays, FileLoggingRetainedDays != b.FileLoggingRetainedDays);

        Add(SettingsField.ConsecutiveFailLimit,
            ProcessHealthEnabled != b.ProcessHealthEnabled ||
            ConsecutiveFailLimit != b.ConsecutiveFailLimit ||
            InhibitOnLimit != b.InhibitOnLimit);
        Add(SettingsField.ProcessHealthRetainedDays, ProcessHealthRetainedDays != b.ProcessHealthRetainedDays);

        Add(SettingsField.IpAddress, !string.Equals(IpAddress.Trim(), b.IpAddress, StringComparison.OrdinalIgnoreCase));
        Add(SettingsField.TcpPort, TcpPort != b.TcpPort);
        Add(SettingsField.IpWhitelist,
            !WhitelistEntries().SequenceEqual(b.IpWhitelist, StringComparer.OrdinalIgnoreCase));

        return fields;
    }

    /// <summary>底栏「需重启」提示：优先显示本次尚未保存的待重启项，其次显示上次保存遗留的待重启项。</summary>
    private string BuildRestartSummary()
    {
        var pending = RestartRequiredFields
            .Where(f => _dirtyFields.Contains(f.Field))
            .Select(f => f.Label)
            .ToList();
        if (pending.Count > 0)
            return $"改动中的 {string.Join("、", pending)} 保存后须重启程序才生效";

        if (_savedRestartLabels.Count > 0)
            return $"已保存，但 {string.Join("、", _savedRestartLabels)} 须重启程序后才生效";

        return "";
    }

    /// <summary>当前待重启项（保存时快照，供底栏留痕）。</summary>
    private List<string> CurrentRestartLabels() =>
        RestartRequiredFields
            .Where(f => _dirtyFields.Contains(f.Field))
            .Select(f => f.Label)
            .ToList();

    /// <summary>记录校验错误：定位到字段与分组、自动展开该分组、并把锚点切到该分组（页面据此滚动）。</summary>
    private void ShowError(string field, string message)
    {
        ErrorField = field;
        ErrorMessage = message;
        Message = $"保存失败: {message}";

        if (ErrorGroup is { Length: > 0 } group)
        {
            ExpandGroup(group);
            SelectedAnchor = Anchors.FirstOrDefault(a => a.Key == group);
        }
    }

    /// <summary>清除字段级错误标记。</summary>
    private void ClearError()
    {
        ErrorField = "";
        ErrorMessage = "";
    }

    /// <summary>展开指定分组下的全部卡片（错误定位用；成功留存/白名单/端点属同组的次要卡片）。</summary>
    private void ExpandGroup(string group)
    {
        switch (group)
        {
            case SettingsGroup.Appearance:
                IsAppearanceExpanded = true;
                break;
            case SettingsGroup.RunParams:
                IsRunParamsExpanded = true;
                break;
            case SettingsGroup.Pose:
                IsPoseExpanded = true;
                break;
            case SettingsGroup.PlcDebug:
                IsPlcDebugExpanded = true;
                break;
            case SettingsGroup.Retention:
                IsRetentionExpanded = true;
                IsSuccessExpanded = true;
                IsRetryExpanded = true;
                break;
            case SettingsGroup.ResultLog:
                IsResultLogExpanded = true;
                break;
            case SettingsGroup.FileLog:
                IsFileLogExpanded = true;
                break;
            case SettingsGroup.Health:
                IsHealthExpanded = true;
                break;
            case SettingsGroup.Whitelist:
                IsWhitelistExpanded = true;
                break;
            case SettingsGroup.Endpoint:
                IsEndpointExpanded = true;
                break;
        }
    }
}

/// <summary>左侧锚点栏的一项：分组标识 + 显示名 + 是否含未保存改动 / 校验错误。</summary>
public sealed partial class SettingsAnchor(string key, string label) : ObservableObject
{
    public string Key { get; } = key;

    public string Label { get; } = label;

    [ObservableProperty] private bool _isDirty;

    [ObservableProperty] private bool _hasError;
}

/// <summary>服务设置页的分组标识（与 XAML 中卡片容器的锚点名 <c>anchor_&lt;key&gt;</c> 一致）。</summary>
public static class SettingsGroup
{
    public const string Appearance = "appearance";
    public const string RunParams = "runparams";
    public const string Pose = "pose";
    public const string PlcDebug = "plcdebug";
    public const string Retention = "retention";
    public const string ResultLog = "resultlog";
    public const string FileLog = "filelog";
    public const string Health = "health";
    public const string Whitelist = "whitelist";
    public const string Endpoint = "endpoint";
}
