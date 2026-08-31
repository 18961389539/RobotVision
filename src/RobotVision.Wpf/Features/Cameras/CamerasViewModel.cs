using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.Infrastructure.Cameras;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Cameras;

public sealed record CameraListItem(
    string Id,
    string Title,
    string? Subtitle,
    string Type,
    string Summary,
    string Status,
    bool Registered,
    string? UnregisteredReason = null);

/// <summary>
/// 相机管理：配置增删改（CameraConfigStore 写回 appsettings.json）、
/// 运行时注册/下线（CameraManager 立即生效，无需重启）、测试取图预览。
/// 支持 File（回放）/ Basler（pylon）/ Virtual（程序生成）三种类型，
/// 编辑面板按类型切换字段；测试取图按当前编辑内容临时构造对应相机，支持"先试后存"。
/// Basler 相机注册成功后额外提供运行时调光（曝光/增益滑块 + 即时取图）。
/// </summary>
public partial class CamerasViewModel : ObservableObject, ICommitPendingEdits
{
    /// <summary>类型下拉数据源：来自相机工厂注册表，接入新品牌自动出现在下拉框。</summary>
    public IReadOnlyList<string> TypeOptions => _registry.TypeNames;

    public IReadOnlyList<string> PatternOptions { get; } = ["Chessboard", "Shapes", "Bars"];

    public ObservableCollection<CameraListItem> Items { get; } = [];

    public ObservableCollection<string> BaslerDevices { get; } = [];

    [ObservableProperty]
    private CameraListItem? _selected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editId = "";

    /// <summary>可选显示名；留空时列表与下拉仅显示 Id。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editType = "File";

    // File 字段
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editFolder = "";

    // Basler 字段
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editDeviceId = "";

    /// <summary>曝光（µs），空 = 不下发（沿用相机当前值）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editExposureUs = "";

    /// <summary>增益（dB），空 = 不下发。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editGain = "";

    /// <summary>硬件相机：单帧取图超时（ms），须小于服务总超时 TimeoutMs。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private int _editGrabTimeoutMs = AppConfig.DefaultGrabTimeoutMs;

    [ObservableProperty]
    private string? _selectedBaslerDevice;

    /// <summary>是否枚举到 Basler 设备（决定设备下拉框可见性）。</summary>
    [ObservableProperty]
    private bool _hasBaslerDevices;

    // Virtual 字段
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private int _editWidth = 1280;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private int _editHeight = 960;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editPattern = "Chessboard";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private int _editCellPx = 40;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private int _editIntervalMs = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private string _editNoiseSigma = "0";

    /// <summary>新建/复制模式下 Id 可编辑；编辑已有相机不允许改 Id。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private bool _isNew;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private ImageSource? _previewImage;

    /// <summary>预览图来源标识（测试取图 / 运行时调光预览），空 = 无预览。</summary>
    [ObservableProperty]
    private string _previewCaption = "";

    /// <summary>预览角标可见性。</summary>
    public bool PreviewVisible => !string.IsNullOrEmpty(PreviewCaption);

    partial void OnPreviewCaptionChanged(string value) => OnPropertyChanged(nameof(PreviewVisible));

    /// <summary>预览角标 ToolTip：采集时刻等次要信息。</summary>
    [ObservableProperty]
    private string _previewToolTip = "";

    /// <summary>取图/调光操作进行中（按钮防抖，防止并发临时实例）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>连续实时预览进行中（驱动预览按钮文案）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewButtonText))]
    private bool _isPreviewing;

    /// <summary>预览按钮文案：切换 实时预览 / 停止预览。</summary>
    public string PreviewButtonText => IsPreviewing ? "停止预览" : "实时预览";

    /// <summary>相机列表浮动面板展开状态（与右侧参数面板同构，图像主导布局）。</summary>
    [ObservableProperty]
    private bool _isListPanelVisible = true;

    [RelayCommand]
    private void ToggleListPanel() => IsListPanelVisible = !IsListPanelVisible;

    /// <summary>参数浮动面板展开状态（方案2：图像主导 + 可折叠参数抽屉）。</summary>
    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;

    // ---- 未保存修改保护 ----

    /// <summary>进入当前编辑状态时的配置快照（脏标记比较基准；新建/复制时为空）。</summary>
    private CameraConfig? _baseline;

    /// <summary>防止 OnSelectedChanged 重入（程序内设置 Selected 时置位）。</summary>
    private bool _switching;

    /// <summary>从磁盘/列表回填编辑区时置位：跳过切类型时的字段清空，避免把刚填入的 SN/目录冲掉。</summary>
    private bool _applyingEditor;

    /// <inheritdoc />
    public Action? FlushPendingEdits { get; set; }

    /// <summary>连续实时预览定时器（Dispatcher 线程，150ms 一帧）。</summary>
    private readonly DispatcherTimer _previewTimer;

    /// <summary>预览帧在途标记（防 Tick 重入：取帧耗时超过间隔时跳过后续 Tick）。</summary>
    private bool _previewTickBusy;

    private const int PreviewFpsSampleCount = 10;

    /// <summary>实时预览帧完成时刻（TickCount64），用于滑动 FPS / 平均帧间隔。</summary>
    private readonly Queue<long> _previewFrameTicks = new();

    /// <summary>编辑内容相对保存态是否有差异（切换/刷新/删除前据此弹确认）。</summary>
    public bool HasUnsavedChanges
    {
        get
        {
            if (_baseline is null || IsNew)
                return false;
            try
            {
                return !SameCamera(_baseline, BuildConfig(_baseline.Id));
            }
            catch (FormatException)
            {
                // 编辑框里有非法数字：视为有未保存修改
                return true;
            }
        }
    }

    /// <summary>有未保存修改时的常驻提示（UI 提示条）。</summary>
    public string UnsavedHint => HasUnsavedChanges
        ? "有未保存的修改：产线/测试触发仍使用磁盘旧配置，请先保存"
        : "";

    /// <summary>
    /// 按当前类型比较「会写入该类型配置」的字段。
    /// Basler 的 BuildConfig 不会带上 Folder/Width/Pattern，若拿全字段比，磁盘残留会导致永远“未保存”。
    /// </summary>
    private static bool SameCamera(CameraConfig a, CameraConfig b)
    {
        if (!string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(a.Name.Trim(), b.Name.Trim(), StringComparison.Ordinal) ||
            !string.Equals(a.Type, b.Type, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(a.Type, "File", StringComparison.OrdinalIgnoreCase))
            return string.Equals(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase) &&
                   a.IntervalMs == b.IntervalMs;
        if (IsHardwareType(a.Type))
            return string.Equals(a.DeviceId, b.DeviceId, StringComparison.OrdinalIgnoreCase) &&
                   a.ExposureTimeUs == b.ExposureTimeUs &&
                   a.Gain == b.Gain &&
                   EffectiveGrabTimeout(a.GrabTimeoutMs) == EffectiveGrabTimeout(b.GrabTimeoutMs);
        if (string.Equals(a.Type, "Virtual", StringComparison.OrdinalIgnoreCase))
            return a.Width == b.Width &&
                   a.Height == b.Height &&
                   string.Equals(a.Pattern, b.Pattern, StringComparison.OrdinalIgnoreCase) &&
                   a.ChessCellPx == b.ChessCellPx &&
                   a.IntervalMs == b.IntervalMs &&
                   a.NoiseSigma == b.NoiseSigma;
        return true;
    }

    private static int EffectiveGrabTimeout(int ms) =>
        ms > 0 ? ms : AppConfig.DefaultGrabTimeoutMs;

    /// <summary>
    /// HasUnsavedChanges 是计算属性，依赖 _baseline。保存后编辑框值不变时
    /// Edit* 不会再发 PropertyChanged，界面会一直显示已过期的“未保存”条。
    /// </summary>
    private void NotifyDirtyState()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
    }

    // ---- 已注册相机的运行时调光（经 IExposureControl 接口查询，不依赖具体品牌） ----

    /// <summary>当前选中相机是否为已注册且支持调光（决定调光卡片可见性）。</summary>
    [ObservableProperty]
    private bool _hasLiveExposureControl;

    [ObservableProperty]
    private double _exposureUs = 10000;

    [ObservableProperty]
    private double _gain = 1;

    private double _exposureMin = 100;
    private double _exposureMax = 1_000_000;
    private double _gainMin;
    private double _gainMax = 24;

    public double ExposureMin => _exposureMin;
    public double ExposureMax => _exposureMax;
    public double GainMin => _gainMin;
    public double GainMax => _gainMax;

    public string ExposureText => $"{ExposureUs:0} µs";
    public string GainText => $"{Gain:0.00} dB";

    /// <summary>曝光对数滑块（0~1000 → min·(max/min)^(v/1000)），跨 3~4 个数量级时线性滑块不可用。</summary>
    public double ExposureSlider
    {
        get
        {
            if (_exposureMax <= _exposureMin)
                return 0;
            var clamped = Math.Clamp(ExposureUs, _exposureMin, _exposureMax);
            return Math.Log(clamped / _exposureMin) / Math.Log(_exposureMax / _exposureMin) * 1000;
        }
        set
        {
            if (_exposureMax <= _exposureMin)
                return;
            ExposureUs = _exposureMin * Math.Pow(_exposureMax / _exposureMin, Math.Clamp(value, 0, 1000) / 1000);
        }
    }

    public CamerasViewModel(
        AppConfig cfg,
        CameraConfigStore store,
        CameraManager cameras,
        RecipeLoader recipes,
        CameraTypeRegistry registry)
    {
        _cfg = cfg;
        _store = store;
        _cameras = cameras;
        _recipes = recipes;
        _registry = registry;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _previewTimer.Tick += OnPreviewTick;
        Refresh();
    }

    private readonly AppConfig _cfg;
    private readonly CameraConfigStore _store;
    private readonly CameraManager _cameras;
    private readonly RecipeLoader _recipes;
    private readonly CameraTypeRegistry _registry;

    public bool IsFile => string.Equals(EditType, "File", StringComparison.OrdinalIgnoreCase);
    public bool IsBasler => string.Equals(EditType, "Basler", StringComparison.OrdinalIgnoreCase);
    public bool IsGigEVision => string.Equals(EditType, "GigEVision", StringComparison.OrdinalIgnoreCase);
    public bool IsVirtual => string.Equals(EditType, "Virtual", StringComparison.OrdinalIgnoreCase);

    /// <summary>Basler / GigEVision 共用序列号、曝光、增益、取图超时编辑面板。</summary>
    public bool IsHardwareCamera => IsBasler || IsGigEVision;

    public bool ShowGrabTimeoutField => IsHardwareCamera;

    /// <summary>当前类型无内置编辑面板（外部工厂注册的类型）→ 显示通用提示卡片。</summary>
    public bool IsUnknownType => !IsFile && !IsBasler && !IsGigEVision && !IsVirtual;

    /// <summary>类型参数区标题（随 EditType 变化）。</summary>
    public string TypeParamsHeader => EditType switch
    {
        "File" => "File 回放",
        "Basler" => "Basler（pylon）",
        "GigEVision" => "GigE Vision",
        "Virtual" => "Virtual 仿真",
        _ => string.IsNullOrWhiteSpace(EditType) ? "类型参数" : $"{EditType} 参数",
    };

    /// <summary>当前类型一行说明（显示在类型参数区顶部）。</summary>
    public string TypeParamsHint => EditType switch
    {
        "File" => "从目录循环读图，适合离线调试；无需真实相机。",
        "Basler" => "需安装 pylon 运行库；多台相机时必须填写序列号或 IP（点「枚举设备」后点选）。",
        "GigEVision" => "纯托管 GigE 协议，不依赖 pylon；多台时必须填写序列号或 IP，并确认网卡与 UDP 防火墙。",
        "Virtual" => "程序生成测试图，无需硬件；可用于标定/流程联调。",
        _ => "该类型由外部工厂注册，高级参数请编辑 appsettings.json。",
    };

    /// <summary>当前类型是否支持设备枚举（File/Virtual 无）。</summary>
    public bool CanEnumerateDevices =>
        IsHardwareCamera && _registry.CanEnumerateDevices(EditType);

    /// <summary>「打开目录」仅 File 回放有效。</summary>
    public bool ShowOpenFolderButton => IsFile;

    /// <summary>棋盘格单元格大小仅 Virtual + Chessboard 图案需要。</summary>
    public bool ShowChessCellField =>
        IsVirtual && string.Equals(EditPattern, "Chessboard", StringComparison.OrdinalIgnoreCase);

    public string EnumerateDevicesLabel =>
        IsGigEVision ? "发现 GigE" : "枚举设备";

    public string EnumeratedDevicesCaption =>
        IsGigEVision ? "本机 GigE 相机（选中填入序列号/IP）" : "本机 Basler 相机（选中填入序列号）";

    private void NotifyTypePanelProperties()
    {
        OnPropertyChanged(nameof(IsFile));
        OnPropertyChanged(nameof(IsBasler));
        OnPropertyChanged(nameof(IsGigEVision));
        OnPropertyChanged(nameof(IsHardwareCamera));
        OnPropertyChanged(nameof(IsVirtual));
        OnPropertyChanged(nameof(IsUnknownType));
        OnPropertyChanged(nameof(TypeParamsHeader));
        OnPropertyChanged(nameof(TypeParamsHint));
        OnPropertyChanged(nameof(CanEnumerateDevices));
        OnPropertyChanged(nameof(ShowOpenFolderButton));
        OnPropertyChanged(nameof(ShowChessCellField));
        OnPropertyChanged(nameof(ShowGrabTimeoutField));
        OnPropertyChanged(nameof(EnumerateDevicesLabel));
        OnPropertyChanged(nameof(EnumeratedDevicesCaption));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
    }

    partial void OnEditTypeChanged(string value)
    {
        NotifyTypePanelProperties();
        if (_applyingEditor)
            return;
        // 切换类型时把其他类型的字段重置为默认，避免残留值误存进新类型配置
        ResetFieldsForOtherTypes();
        BaslerDevices.Clear();
        HasBaslerDevices = false;
        SelectedBaslerDevice = null;
    }

    partial void OnEditPatternChanged(string value)
    {
        OnPropertyChanged(nameof(ShowChessCellField));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
    }

    /// <summary>ComboBox 切换类型时若绑定未触发 PropertyChanged，由页面 code-behind 调用。</summary>
    public void OnEditTypeSelectionChanged() => NotifyTypePanelProperties();

    /// <summary>重置非当前类型的编辑字段（EditIntervalMs 为 File/Virtual 共用，保留）。</summary>
    private void ResetFieldsForOtherTypes()
    {
        if (!IsFile)
            EditFolder = "";
        if (!IsHardwareCamera)
        {
            EditDeviceId = "";
            EditExposureUs = "";
            EditGain = "";
            EditGrabTimeoutMs = AppConfig.DefaultGrabTimeoutMs;
        }
        if (!IsVirtual)
        {
            EditWidth = 1280;
            EditHeight = 960;
            EditPattern = "Chessboard";
            EditCellPx = 40;
            EditNoiseSigma = "0";
        }
    }

    partial void OnExposureUsChanged(double value)
    {
        OnPropertyChanged(nameof(ExposureSlider));
        OnPropertyChanged(nameof(ExposureText));
    }

    partial void OnGainChanged(double value) => OnPropertyChanged(nameof(GainText));

    [RelayCommand]
    public void Refresh() => Refresh(preferId: null);

    /// <param name="preferId">刷新后优先选中的 Id；空则尽量保持当前选中。</param>
    /// <param name="resetPreview">
    /// true：同步编辑区时清掉预览图（F5 / 进页 / 删除后换机）。
    /// false：保存后刷新列表时保留当前预览——图仍是这台相机的，清掉只增加再取一次的成本。
    /// </param>
    /// <param name="loadLiveParams">
    /// true：后台读曝光/增益填滑块（选中/F5）。
    /// false：保存后不要连相机读参——读失败会把「已保存」盖成「读取相机参数失败」。
    /// </param>
    public void Refresh(string? preferId, bool resetPreview = true, bool loadLiveParams = true)
    {
        // 未保存修改：确认后才丢弃（页面 Loaded 自动刷新同样生效）
        if (HasUnsavedChanges && !ConfirmDiscard("刷新列表"))
            return;

        // ListBox 双向绑定：Items.Clear() 会把 Selected 置 null，必须先记下要恢复的 Id
        var keepId = preferId ?? Selected?.Id ?? EditId.Trim();

        _switching = true;
        try
        {
            Items.Clear();
            foreach (var camera in _cfg.Cameras)
            {
                var registered = _cameras.IsRegistered(camera.Id);
                var title = CameraLabels.ListTitle(camera);
                var subtitle = string.IsNullOrWhiteSpace(camera.Name) ? null : camera.Id;
                Items.Add(new CameraListItem(
                    camera.Id, title, subtitle, camera.Type, Summarize(camera),
                    registered ? "已注册" : "未注册", registered,
                    registered ? null : UnregisterReason(camera)));
            }

            Selected = string.IsNullOrWhiteSpace(keepId)
                ? Items.FirstOrDefault()
                : Items.FirstOrDefault(i => string.Equals(i.Id, keepId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _switching = false;
        }

        // Refresh 在 _switching 内改 Selected 会跳过 OnSelectedChanged，须显式同步编辑区与调光面板
        if (Selected is not null)
            ApplySelectedItem(Selected, resetPreview, loadLiveParams);
        Message = $"共 {Items.Count} 台相机";
    }

    /// <summary>未保存修改确认框；用户拒绝时返回 false 并中止当前操作。</summary>
    private bool ConfirmDiscard(string action)
    {
        return MessageBox.Show($"相机 {_baseline?.Id} 有未保存的修改，{action}将丢弃这些修改。继续？",
            "未保存修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static string Summarize(CameraConfig c) => c.Type switch
    {
        "Basler" or "GigEVision" => string.IsNullOrWhiteSpace(c.DeviceId)
            ? "未指定设备（仅单台时可开）"
            : $"SN/IP {c.DeviceId}",
        "Virtual" => $"{c.Width}×{c.Height} · {c.Pattern}",
        _ => c.Folder,
    };

    /// <summary>未注册原因（列表展示，让"未注册"在列表层面即可区分原因）。</summary>
    private string? UnregisterReason(CameraConfig c)
    {
        if (string.Equals(c.Type, "File", StringComparison.OrdinalIgnoreCase))
        {
            var folder = c.ResolveCameraFolder(_cfg);
            if (!Directory.Exists(folder))
                return "回放目录不存在";
            return Directory.EnumerateFiles(folder)
                .Any(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                ? "初始化失败"
                : "目录中没有图片";
        }
        if (string.Equals(c.Type, "Basler", StringComparison.OrdinalIgnoreCase))
            return "pylon 未安装或相机未连接";
        if (string.Equals(c.Type, "GigEVision", StringComparison.OrdinalIgnoreCase))
            return "网口未发现 GigE Vision 相机";
        return "初始化失败";
    }

    private static readonly string[] ImageExtensions = [".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    [RelayCommand]
    private void New()
    {
        if (HasUnsavedChanges && !ConfirmDiscard("新建相机"))
            return;
        StopPreview();
        IsNew = true;
        _baseline = null;
        EditId = "";
        EditName = "";
        EditType = "File";
        EditFolder = "";
        EditDeviceId = "";
        EditExposureUs = "";
        EditGain = "";
        EditGrabTimeoutMs = AppConfig.DefaultGrabTimeoutMs;
        EditWidth = 1280;
        EditHeight = 960;
        EditPattern = "Chessboard";
        EditCellPx = 40;
        EditIntervalMs = 0;
        EditNoiseSigma = "0";
        PreviewImage = null;
        PreviewCaption = "";
        PreviewToolTip = "";
        Message = "新建相机：选择类型并填写参数后保存";
    }

    [RelayCommand]
    private void Copy()
    {
        if (HasUnsavedChanges && !ConfirmDiscard("复制相机"))
            return;
        StopPreview();
        var id = EditId.Trim();
        IsNew = true;
        _baseline = null;
        EditId = id.Length > 0 ? id + "_copy" : "";
        PreviewImage = null;
        PreviewCaption = "";
        PreviewToolTip = "";
        Message = "已复制：改 Id 后保存即新相机";
    }

    [RelayCommand]
    private void Save()
    {
        this.Commit();
        var id = EditId.Trim();
        if (id.Length == 0)
        {
            Message = "保存失败：Id 不能为空";
            return;
        }
        if (IsNew && _cfg.Cameras.Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            Message = $"保存失败：相机 {id} 已存在";
            return;
        }

        CameraConfig entry;
        try
        {
            entry = BuildConfig(id);
        }
        catch (FormatException ex)
        {
            Message = $"保存失败: {ex.Message}";
            return;
        }

        // 超时预算校验：硬件相机取图超时须小于服务总超时
        if (IsHardwareType(entry.Type))
        {
            if (entry.GrabTimeoutMs <= 0)
            {
                Message = "保存失败: 取图超时须为正数（ms）";
                return;
            }
            if (entry.GrabTimeoutMs >= _cfg.TimeoutMs)
            {
                Message = $"保存失败: 取图超时 {entry.GrabTimeoutMs}ms 不小于服务总超时 {_cfg.TimeoutMs}ms（将表现为 1008 而非 1003），请调小取图超时或在服务设置调大总超时";
                return;
            }
        }

        try
        {
            var list = _cfg.Cameras.ToList();
            var index = list.FindIndex(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                list[index] = entry;
            else
                list.Add(entry);

            _store.Save(list);

            // 运行时注册失败不影响配置保存（如 Basler 未接相机，之后接上重新保存即可）
            var error = TryRegister(entry);
            IsNew = false;
            _baseline = entry; // 保存后的配置即新基线（Refresh 的脏检查不会误弹）
            Refresh(id, resetPreview: false, loadLiveParams: false);
            Message = error is null ? $"已保存 {id}（运行时已注册）" : $"已保存 {id}（运行时注册失败: {error}）";
            NotifyDirtyState();
        }
        catch (Exception ex)
        {
            Message = $"保存失败: {ex.Message}";
        }
    }

    /// <summary>按当前编辑字段构造配置；数值格式非法抛 FormatException。
    /// 未知类型（外部工厂注册、无内置编辑面板）：保留磁盘已有字段（克隆），仅应用 Id/Type。</summary>
    private CameraConfig BuildConfig(string id)
    {
        CameraConfig entry;
        if (!_registry.IsKnown(EditType))
        {
            var existing = _cfg.Cameras.FirstOrDefault(
                c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var clone = JsonSerializer.Deserialize<CameraConfig>(
                    JsonSerializer.Serialize(existing))!;
                clone.Id = id;
                clone.Type = EditType;
                entry = clone;
            }
            else
                entry = new CameraConfig { Id = id, Type = EditType };
        }
        else
        {
            entry = new CameraConfig { Id = id, Type = EditType };
            switch (EditType)
            {
                case "Basler":
                case "GigEVision":
                    entry.DeviceId = EditDeviceId.Trim();
                    entry.ExposureTimeUs = ParseOptional(EditExposureUs, "曝光时间");
                    entry.Gain = ParseOptional(EditGain, "增益");
                    entry.GrabTimeoutMs = EditGrabTimeoutMs;
                    break;
                case "Virtual":
                    entry.Width = EditWidth;
                    entry.Height = EditHeight;
                    entry.Pattern = EditPattern;
                    entry.ChessCellPx = EditCellPx;
                    entry.IntervalMs = EditIntervalMs;
                    entry.NoiseSigma = ParseDouble(EditNoiseSigma, "噪声");
                    break;
                default:
                    entry.Folder = EditFolder.Trim();
                    entry.IntervalMs = EditIntervalMs;
                    break;
            }
        }

        entry.Name = EditName.Trim();
        return entry;
    }

    private static double? ParseOptional(string text, string label) =>
        string.IsNullOrWhiteSpace(text) ? null : ParseDouble(text, label);

    private static double ParseDouble(string text, string label)
    {
        if (!double.TryParse(text.Trim(), out var value))
            throw new FormatException($"{label} 不是有效数字: {text}");
        return value;
    }

    [RelayCommand]
    private void Delete()
    {
        if (IsNew || Selected is null)
        {
            Message = "请先选择要删除的相机";
            return;
        }
        var id = Selected.Id;

        var referenced = _recipes.ListNames()
            .Where(n =>
            {
                try { return _recipes.Get(n).CameraId == id; }
                catch { return false; }
            })
            .ToList();
        var warn = referenced.Count > 0
            ? $"\n有 {referenced.Count} 个配方引用该相机（{string.Join("、", referenced)}），删除后触发将返回相机未注册错误。"
            : "";

        var prompt = HasUnsavedChanges
            ? $"相机 {_baseline?.Id} 有未保存的修改，删除将一并丢弃。确定删除相机 {id}？{warn}"
            : $"确定删除相机 {id}？{warn}";

        if (MessageBox.Show(prompt, "删除相机",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            var list = _cfg.Cameras
                .Where(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _store.Save(list);
            _cameras.Unregister(id);
            Refresh();
            Message = $"已删除 {id}";
        }
        catch (Exception ex)
        {
            Message = $"删除失败: {ex.Message}";
        }
    }

    /// <summary>取图/调光操作未进行中（防抖，避免并发临时相机实例）。</summary>
    private bool CanOperate => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        TestGrabCommand.NotifyCanExecuteChanged();
        ApplyAndGrabCommand.NotifyCanExecuteChanged();
        TogglePreviewCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task TestGrabAsync()
    {
        var id = EditId.Trim();
        if (id.Length == 0)
        {
            Message = "测试取图：请先填写 Id";
            return;
        }

        CameraConfig entry;
        try
        {
            entry = BuildConfig(id);
        }
        catch (FormatException ex)
        {
            Message = $"测试取图: {ex.Message}";
            return;
        }

        IsBusy = true;
        try
        {
            var label = PreviewCameraLabel();
            Message = $"取图中 · {label}";
            var snap = await Task.Run(() => GrabFrameSnapshot(entry, id));
            ApplyPreviewSnapshot("测试取图", label, snap);
            Message = $"取图成功 · {label} · {snap.Width}×{snap.Height} · {snap.ElapsedMs:0} ms";
        }
        catch (Exception ex)
        {
            ClearPreview();
            Message = $"取图失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void TogglePreview()
    {
        if (IsPreviewing)
            StopPreview();
        else
            StartPreview();
    }

    private void StartPreview()
    {
        var id = EditId.Trim();
        if (id.Length == 0)
        {
            Message = "实时预览：请先填写 Id";
            return;
        }
        IsPreviewing = true;
        _previewFrameTicks.Clear();
        Message = $"实时预览中 · {PreviewCameraLabel()}";
        _previewTimer.Start();
    }

    /// <summary>停止实时预览。页面离开时（Unloaded）也必须调用——定时器属于进程级单例
    /// ViewModel，不随页面销毁，不停就会在后台持续 Grab 占用相机。</summary>
    public void StopPreview()
    {
        _previewTimer.Stop();
        _previewTickBusy = false;
        _previewFrameTicks.Clear();
        if (IsPreviewing)
        {
            IsPreviewing = false;
            Message = "已停止实时预览";
        }
    }

    /// <summary>定时取帧：已注册相机用运行时实例，未注册按编辑内容临时构造（与测试取图一致）。</summary>
    private void OnPreviewTick(object? sender, EventArgs e)
    {
        // 不用 async void：事件处理器保持 void，async 主体挪到返回 Task 的方法。
        // _previewTickBusy 在首个 await 前同步置位（Task 方法 await 前同步执行），
        // 重入保护语义与原 async void 完全一致；异常由 OnPreviewTickAsync 全捕获。
        _ = OnPreviewTickAsync();
    }

    private async Task OnPreviewTickAsync()
    {
        // IsBusy（手动取图/调光进行中）或上一帧未完成时跳过，避免并发 Grab 与堆帧
        if (_previewTickBusy || IsBusy)
            return;
        _previewTickBusy = true;
        try
        {
            var id = EditId.Trim();
            if (id.Length == 0)
            {
                StopPreview();
                Message = "实时预览：请先填写 Id";
                return;
            }

            CameraConfig entry;
            try
            {
                entry = BuildConfig(id);
            }
            catch (FormatException ex)
            {
                StopPreview();
                Message = $"实时预览: {ex.Message}";
                return;
            }

            var label = PreviewCameraLabel();
            var snap = await Task.Run(() => GrabFrameSnapshot(entry, id));
            RecordPreviewFrameTiming();
            var (fps, avgMs) = ComputePreviewRates();
            ApplyPreview(
                snap,
                FormatLivePreviewCaption(label, snap.Width, snap.Height, snap.ElapsedMs, fps, avgMs));
        }
        catch (Exception ex)
        {
            ClearPreview();
            StopPreview();
            Message = $"实时预览失败: {ex.Message}";
        }
        finally
        {
            _previewTickBusy = false;
        }
    }

    private void RecordPreviewFrameTiming()
    {
        _previewFrameTicks.Enqueue(Environment.TickCount64);
        while (_previewFrameTicks.Count > PreviewFpsSampleCount)
            _previewFrameTicks.Dequeue();
    }

    private (double? Fps, double? AvgMs) ComputePreviewRates()
    {
        if (_previewFrameTicks.Count < 2)
            return (null, null);

        var span = _previewFrameTicks.Last() - _previewFrameTicks.First();
        if (span <= 0)
            return (null, null);

        var intervals = _previewFrameTicks.Count - 1;
        return (intervals * 1000.0 / span, span / intervals);
    }

    [RelayCommand]
    private async Task EnumerateBaslerAsync()
    {
        BaslerDevices.Clear();
        HasBaslerDevices = false;
        try
        {
            Message = "枚举设备中…";
            var type = EditType;
            var devices = await Task.Run(() => _registry.EnumerateDevices(type));
            foreach (var d in devices)
                BaslerDevices.Add(d);
            HasBaslerDevices = devices.Count > 0;
            SelectedBaslerDevice = BaslerDevices.FirstOrDefault();
            if (!string.IsNullOrEmpty(SelectedBaslerDevice))
                EditDeviceId = SelectedBaslerDevice.Split('|')[0].Trim();
            Message = devices.Count > 0
                ? $"发现 {devices.Count} 台设备，选择后自动填入序列号/IP"
                : string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase)
                    ? "未发现 GigE Vision 相机（检查网线、IP 网段与 UDP 防火墙）"
                    : "未发现 Basler 相机。若 pylon Viewer 能看到，请先完全退出 Viewer 再枚举，并填写序列号";
        }
        catch (Exception ex)
        {
            Message = $"枚举设备失败: {ex.Message}";
        }
    }

    partial void OnSelectedBaslerDeviceChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        // 条目格式 "SN | 名称 | 型号"
        EditDeviceId = value.Split('|')[0].Trim();
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择回放目录" };
        var resolved = _cfg.ResolveDataPath(EditFolder.Trim());
        if (Directory.Exists(resolved))
            dialog.InitialDirectory = resolved;
        if (dialog.ShowDialog() == true)
            EditFolder = dialog.FolderName;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (!IsFile)
        {
            Message = "仅 File 类型相机有回放目录";
            return;
        }
        var resolved = _cfg.ResolveDataPath(EditFolder.Trim());
        if (Directory.Exists(resolved))
            Explorer.OpenFolder(resolved);
        else
            Message = $"目录不存在: {resolved}";
    }

    // ---- Basler 运行时调光 ----

    [RelayCommand]
    private async Task ReadLiveParamsAsync()
    {
        if (LiveExposureControl is not { } camera)
            return;
        var id = Selected?.Id ?? EditId.Trim();
        await LoadLiveParamsCore(camera, id, reportToMessage: true);
    }

    /// <summary>后台读取 pylon 光度参数并回写（仅当选中相机未变时生效，避免串扰）。</summary>
    /// <param name="reportToMessage">
    /// true：用户点了「读取当前值」，读失败写到 Message。
    /// false：选中/刷新时的静默回填，失败不改 Message（避免盖住「已保存」）。
    /// </param>
    private async Task LoadLiveParamsCore(IExposureControl camera, string cameraId, bool reportToMessage)
    {
        try
        {
            var (exposure, gain, exposureRange, gainRange) = await Task.Run(() => (
                camera.GetExposureTimeUs(), camera.GetGain(),
                camera.GetExposureRange(), camera.GetGainRange()));

            // 读取期间用户可能已切换相机：只回写仍选中的相机
            if (!string.Equals(Selected?.Id, cameraId, StringComparison.OrdinalIgnoreCase))
                return;

            if (exposureRange is { } er)
            {
                _exposureMin = Math.Max(1, er.Min);
                _exposureMax = er.Max;
                OnPropertyChanged(nameof(ExposureMin));
                OnPropertyChanged(nameof(ExposureMax));
            }
            if (gainRange is { } gr)
            {
                _gainMin = gr.Min;
                _gainMax = gr.Max;
                OnPropertyChanged(nameof(GainMin));
                OnPropertyChanged(nameof(GainMax));
            }

            ExposureUs = exposure ?? ExposureUs;
            Gain = gain ?? Gain;
            if (!reportToMessage)
                return;
            Message = exposure is null && gain is null
                ? "读取相机参数失败（无法连接相机或机型不支持读回曝光/增益，可先点「测试取图」确认在线）"
                : $"当前曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB";
        }
        catch (Exception ex)
        {
            if (reportToMessage
                && string.Equals(Selected?.Id, cameraId, StringComparison.OrdinalIgnoreCase))
                Message = $"读取相机参数失败: {ex.Message}";
        }
    }

    /// <summary>应用滑块值到已注册相机并取一帧预览（与产线链路同一实例，立即生效）。</summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ApplyAndGrabAsync()
    {
        var id = Selected?.Id ?? EditId.Trim();
        if (id.Length == 0 || !_cameras.TryGet(id, out var camera) || camera is not IExposureControl exposure)
            return;

        IsBusy = true;
        try
        {
            var label = PreviewCameraLabel();
            Message = $"下发光度参数并取图中 · {label}";
            var entry = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? BuildConfig(id);
            var snap = await Task.Run(() =>
            {
                exposure.TrySetExposureTimeUs(ExposureUs);
                exposure.TrySetGain(Gain);
                return GrabFrameSnapshot(entry, id);
            });
            ApplyPreviewSnapshot("调光取图", label, snap);
            Message = $"取图成功 · 曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB · {snap.ElapsedMs:0} ms";
        }
        catch (Exception ex)
        {
            Message = $"调光取图失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 把当前滑块值填入编辑框（曝光/增益），点"保存"后持久化。
    /// 与"保存"职责分离：滑块只做运行时调光，持久化统一走"保存"。
    /// </summary>
    [RelayCommand]
    private void SaveLiveParams()
    {
        var id = Selected?.Id ?? EditId.Trim();
        if (id.Length == 0)
            return;

        var entry = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null || !IsHardwareType(entry.Type))
        {
            Message = $"相机 {id} 不支持写回光度参数";
            return;
        }

        EditExposureUs = ExposureUs.ToString("0");
        EditGain = Gain.ToString("0.##");
        Message = $"已填入编辑框：曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB（点「保存」持久化到配置）";
    }

    /// <summary>已注册且实现 IExposureControl 的相机（不限品牌；未注册或类型不支持时返回 null）。</summary>
    private IExposureControl? LiveExposureControl
    {
        get
        {
            var id = Selected?.Id ?? EditId.Trim();
            if (id.Length == 0 || !_cameras.TryGet(id, out var camera))
                return null;
            return camera as IExposureControl;
        }
    }

    /// <summary>按配置创建相机（临时实例，先试后存）：经工厂注册表，支持外部注册的类型。</summary>
    private ICamera CreateCamera(CameraConfig camera) =>
        _registry.Create(camera)
        ?? throw new InvalidOperationException($"相机类型 {camera.Type} 未注册工厂（CameraTypeRegistry.Register）");

    private string? TryRegister(CameraConfig camera)
    {
        try
        {
            _cameras.Register(CreateCamera(camera));
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    partial void OnSelectedChanged(CameraListItem? value)
    {
        if (_switching || value is null)
            return;

        // 未保存修改：确认后才切换（拒绝时恢复原选中）。预览停在确认之后，
        // 避免点了另一台又取消时把当前实时预览一并停掉。
        if (HasUnsavedChanges && !ConfirmDiscard("切换相机"))
        {
            _switching = true;
            Selected = Items.FirstOrDefault(i => string.Equals(i.Id, _baseline?.Id, StringComparison.OrdinalIgnoreCase));
            _switching = false;
            return;
        }

        ApplySelectedItem(value, resetPreview: true, loadLiveParams: true);
    }

    /// <summary>选中项 → 编辑区 + 运行时调光面板（Refresh 与 OnSelectedChanged 共用）。</summary>
    private void ApplySelectedItem(CameraListItem value, bool resetPreview, bool loadLiveParams)
    {
        IsNew = false;
        if (resetPreview)
        {
            StopPreview();
            ClearPreview();
        }

        var config = _cfg.Cameras.FirstOrDefault(
            c => string.Equals(c.Id, value.Id, StringComparison.OrdinalIgnoreCase));
        if (config is null)
            return;

        _applyingEditor = true;
        try
        {
            _baseline = config;
            EditId = config.Id;
            EditName = config.Name;
            EditType = config.Type;
            EditFolder = config.Folder;
            EditDeviceId = config.DeviceId;
            EditExposureUs = config.ExposureTimeUs?.ToString("0") ?? "";
            EditGain = config.Gain?.ToString("0.##") ?? "";
            if (IsHardwareType(config.Type))
                EditGrabTimeoutMs = config.GrabTimeoutMs > 0
                    ? config.GrabTimeoutMs
                    : AppConfig.DefaultGrabTimeoutMs;
            EditWidth = config.Width;
            EditHeight = config.Height;
            EditPattern = PatternOptions.Contains(config.Pattern, StringComparer.OrdinalIgnoreCase)
                ? config.Pattern
                : "Chessboard";
            EditCellPx = config.ChessCellPx;
            EditIntervalMs = config.IntervalMs;
            EditNoiseSigma = config.NoiseSigma.ToString("0.##");

            if (config.ExposureTimeUs is > 0)
                ExposureUs = config.ExposureTimeUs.Value;
            if (config.Gain is >= 0)
                Gain = config.Gain.Value;
        }
        finally
        {
            _applyingEditor = false;
            NotifyDirtyState();
        }

        var live = LiveExposureControl;
        HasLiveExposureControl = live is not null;
        if (live is not null)
        {
            if (loadLiveParams)
                // pylon 参数读取可能阻塞（断线/网络延迟），后台执行避免选中即卡 UI
                _ = LoadLiveParamsCore(live, value.Id, reportToMessage: false);
        }
        else if (value.Registered && IsHardwareType(config.Type))
            Message = $"{value.Id} 已注册但无法调光（初始化占位或类型不支持 IExposureControl）";
        else if (!value.Registered)
            Message = $"{value.Id} 未注册（" + UnregisteredHint(config.Type) + "）";
    }

    private static bool IsHardwareType(string type) =>
        string.Equals(type, "Basler", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase);

    private sealed record GrabSnapshot(
        BitmapSource Image,
        int Width,
        int Height,
        double ElapsedMs,
        DateTime CapturedAtLocal);

    private string PreviewCameraLabel()
    {
        var id = EditId.Trim();
        var name = EditName.Trim();
        return name.Length > 0 ? $"{name} ({id})" : id;
    }

    private GrabSnapshot GrabFrameSnapshot(CameraConfig entry, string id)
    {
        var sw = Stopwatch.StartNew();
        if (_cameras.TryGet(id, out var existing) && existing is not null)
        {
            using var registeredFrame = _cameras.Grab(id);
            sw.Stop();
            return ToGrabSnapshot(registeredFrame, sw.Elapsed.TotalMilliseconds);
        }

        using var camera = CreateCamera(entry);
        using var tempFrame = _cameras.Grab(camera);
        sw.Stop();
        return ToGrabSnapshot(tempFrame, sw.Elapsed.TotalMilliseconds);
    }

    private static GrabSnapshot ToGrabSnapshot(CameraFrame frame, double elapsedMs)
    {
        var image = frame.Image;
        var source = ImageConverter.ToBitmapSource(image);
        return new GrabSnapshot(
            source,
            image.Width,
            image.Height,
            elapsedMs,
            frame.CapturedAtUtc.ToLocalTime());
    }

    private void ApplyPreviewSnapshot(string mode, string label, GrabSnapshot snap) =>
        ApplyPreview(snap, FormatSingleGrabCaption(mode, label, snap.Width, snap.Height, snap.ElapsedMs));

    private void ApplyPreview(GrabSnapshot snap, string caption)
    {
        PreviewImage = snap.Image;
        PreviewCaption = caption;
        PreviewToolTip = FormatCaptureToolTip(snap.CapturedAtLocal);
    }

    private void ClearPreview()
    {
        PreviewImage = null;
        PreviewCaption = "";
        PreviewToolTip = "";
    }

    private static string FormatSingleGrabCaption(string mode, string label, int width, int height, double elapsedMs) =>
        $"{mode} · {label} · {width}×{height} · {elapsedMs:0} ms";

    private static string FormatLivePreviewCaption(
        string label,
        int width,
        int height,
        double elapsedMs,
        double? fps,
        double? avgMs)
    {
        var parts = new List<string> { "实时预览", label, $"{width}×{height}" };
        if (fps is not null && avgMs is not null)
        {
            parts.Add($"{fps:0.#} fps");
            parts.Add($"{avgMs:0} ms/帧");
        }
        else
            parts.Add($"{elapsedMs:0} ms");

        return string.Join(" · ", parts);
    }

    private static string FormatCaptureToolTip(DateTime capturedAtLocal) =>
        $"采集时刻 {capturedAtLocal:yyyy-MM-dd HH:mm:ss.fff}";

    private static string UnregisteredHint(string type) => type switch
    {
        _ when string.Equals(type, "Basler", StringComparison.OrdinalIgnoreCase)
            => "未安装 pylon、相机未连接或序列号不符",
        _ when string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase)
            => "网口未发现相机、IP 网段不符或 UDP 被防火墙拦截",
        _ => "目录不存在或没有图片",
    };
}
