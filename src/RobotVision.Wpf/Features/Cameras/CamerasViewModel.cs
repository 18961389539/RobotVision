using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Cameras;

/// <summary>
/// 相机管理：配置增删改（CameraConfigStore 写回 appsettings.json）、
/// 运行时注册/下线（CameraManager 立即生效，无需重启）、测试取图预览。
/// 支持 File（回放）/ Basler（pylon）/ Virtual（程序生成）三种类型，
/// 编辑面板按类型切换字段；测试取图按当前编辑内容临时构造对应相机，支持"先试后存"。
/// Basler 相机注册成功后额外提供运行时调光（曝光/增益滑块 + 即时取图）。
/// </summary>
public partial class CamerasViewModel : ObservableObject, ICommitPendingEdits, IDisposable
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
    private CancellationTokenSource? _refreshCts;

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

    /// <summary>停止预览时取消在途取图，避免迟到帧写回 UI。</summary>
    private CancellationTokenSource? _previewCts;

    /// <summary>预览取帧/会话释放串行锁（创建、Grab、Dispose 同生命周期）。</summary>
    private readonly object _previewSessionLock = new();

    /// <summary>当前预览 Tick 异步体（Stop 时等待其结束再 Dispose 会话相机）。</summary>
    private Task _previewInFlightTask = Task.CompletedTask;

    /// <summary>未注册相机的预览会话（单实例复用，避免每帧开关 pylon）。</summary>
    private ICamera? _previewSessionCamera;

    private string? _previewSessionId;

    private CameraConfig? _previewSessionConfig;

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
        ICameraRuntime cameras,
        RecipeLoader recipes,
        CameraTypeRegistry registry,
        IDialogService dialogs,
        ILogger<CamerasViewModel> log)
    {
        _cfg = cfg;
        _store = store;
        _cameras = cameras;
        _recipes = recipes;
        _registry = registry;
        _dialogs = dialogs;
        _log = log;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _previewTimer.Tick += OnPreviewTick;
    }

    private readonly AppConfig _cfg;
    private readonly CameraConfigStore _store;
    private readonly ICameraRuntime _cameras;
    private readonly RecipeLoader _recipes;
    private readonly CameraTypeRegistry _registry;
    private readonly IDialogService _dialogs;
    private readonly ILogger<CamerasViewModel> _log;

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
}
