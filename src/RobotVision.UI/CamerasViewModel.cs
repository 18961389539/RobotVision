using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.UI;

public sealed record CameraListItem(
    string Id, string Type, string Summary, string Status, bool Registered, string? UnregisteredReason = null);

/// <summary>
/// 相机管理：配置增删改（CameraConfigStore 写回 appsettings.json）、
/// 运行时注册/下线（CameraManager 立即生效，无需重启）、测试取图预览。
/// 支持 File（回放）/ Basler（pylon）/ Virtual（程序生成）三种类型，
/// 编辑面板按类型切换字段；测试取图按当前编辑内容临时构造对应相机，支持"先试后存"。
/// Basler 相机注册成功后额外提供运行时调光（曝光/增益滑块 + 即时取图）。
/// </summary>
public partial class CamerasViewModel : ObservableObject
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(UnsavedHint))]
    private int _editGrabTimeoutMs = 3000;

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

    /// <summary>取图/调光操作进行中（按钮防抖，防止并发临时实例）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    // ---- 未保存修改保护 ----

    /// <summary>进入当前编辑状态时的配置快照（脏标记比较基准；新建/复制时为空）。</summary>
    private CameraConfig? _baseline;

    /// <summary>防止 OnSelectedChanged 重入（程序内设置 Selected 时置位）。</summary>
    private bool _switching;

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

    /// <summary>编辑字段与磁盘配置逐字段比较（脏标记用）。</summary>
    private static bool SameCamera(CameraConfig a, CameraConfig b) =>
        string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Type, b.Type, StringComparison.OrdinalIgnoreCase) &&
        a.Folder == b.Folder &&
        a.DeviceId == b.DeviceId &&
        a.ExposureTimeUs == b.ExposureTimeUs &&
        a.Gain == b.Gain &&
        a.GrabTimeoutMs == b.GrabTimeoutMs &&
        a.Width == b.Width &&
        a.Height == b.Height &&
        string.Equals(a.Pattern, b.Pattern, StringComparison.OrdinalIgnoreCase) &&
        a.ChessCellPx == b.ChessCellPx &&
        a.IntervalMs == b.IntervalMs &&
        a.NoiseSigma == b.NoiseSigma;

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
        Refresh();
    }

    private readonly AppConfig _cfg;
    private readonly CameraConfigStore _store;
    private readonly CameraManager _cameras;
    private readonly RecipeLoader _recipes;
    private readonly CameraTypeRegistry _registry;

    public bool IsFile => string.Equals(EditType, "File", StringComparison.OrdinalIgnoreCase);
    public bool IsBasler => string.Equals(EditType, "Basler", StringComparison.OrdinalIgnoreCase);
    public bool IsVirtual => string.Equals(EditType, "Virtual", StringComparison.OrdinalIgnoreCase);

    /// <summary>当前类型无内置编辑面板（外部工厂注册的类型）→ 显示通用提示卡片。</summary>
    public bool IsUnknownType => !IsFile && !IsBasler && !IsVirtual;

    partial void OnEditTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsFile));
        OnPropertyChanged(nameof(IsBasler));
        OnPropertyChanged(nameof(IsVirtual));
        OnPropertyChanged(nameof(IsUnknownType));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        // 切换类型时把其他类型的字段重置为默认，避免残留值误存进新类型配置
        ResetFieldsForOtherTypes();
    }

    /// <summary>重置非当前类型的编辑字段（EditIntervalMs 为 File/Virtual 共用，保留）。</summary>
    private void ResetFieldsForOtherTypes()
    {
        if (!IsFile)
            EditFolder = "";
        if (!IsBasler)
        {
            EditDeviceId = "";
            EditExposureUs = "";
            EditGain = "";
            EditGrabTimeoutMs = 3000;
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
    public void Refresh()
    {
        // 未保存修改：确认后才丢弃（页面 Loaded 自动刷新同样生效）
        if (HasUnsavedChanges && !ConfirmDiscard("刷新列表"))
            return;

        Items.Clear();
        foreach (var camera in _cfg.Cameras)
        {
            var registered = _cameras.IsRegistered(camera.Id);
            Items.Add(new CameraListItem(
                camera.Id, camera.Type, Summarize(camera),
                registered ? "已注册" : "未注册", registered,
                registered ? null : UnregisterReason(camera)));
        }

        _switching = true;
        Selected = Items.FirstOrDefault(i => i.Id == Selected?.Id) ?? Items.FirstOrDefault();
        _switching = false;
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
        "Basler" => string.IsNullOrWhiteSpace(c.DeviceId) ? "自动选择第一台" : $"SN {c.DeviceId}",
        "Virtual" => $"{c.Width}×{c.Height} · {c.Pattern}",
        _ => c.Folder,
    };

    /// <summary>未注册原因（列表展示，让"未注册"在列表层面即可区分原因）。</summary>
    private static string? UnregisterReason(CameraConfig c)
    {
        if (string.Equals(c.Type, "File", StringComparison.OrdinalIgnoreCase))
        {
            var folder = c.ResolveCameraFolder();
            if (!Directory.Exists(folder))
                return "回放目录不存在";
            return Directory.EnumerateFiles(folder)
                .Any(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                ? "初始化失败"
                : "目录中没有图片";
        }
        if (string.Equals(c.Type, "Basler", StringComparison.OrdinalIgnoreCase))
            return "pylon 未安装或相机未连接";
        return "初始化失败";
    }

    private static readonly string[] ImageExtensions = [".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    [RelayCommand]
    private void New()
    {
        if (HasUnsavedChanges && !ConfirmDiscard("新建相机"))
            return;
        IsNew = true;
        _baseline = null;
        EditId = "";
        EditType = "File";
        EditFolder = "";
        EditDeviceId = "";
        EditExposureUs = "";
        EditGain = "";
        EditGrabTimeoutMs = 3000;
        EditWidth = 1280;
        EditHeight = 960;
        EditPattern = "Chessboard";
        EditCellPx = 40;
        EditIntervalMs = 0;
        EditNoiseSigma = "0";
        PreviewImage = null;
        PreviewCaption = "";
        Message = "新建相机：选择类型并填写参数后保存";
    }

    [RelayCommand]
    private void Copy()
    {
        if (HasUnsavedChanges && !ConfirmDiscard("复制相机"))
            return;
        var id = EditId.Trim();
        IsNew = true;
        _baseline = null;
        EditId = id.Length > 0 ? id + "_copy" : "";
        PreviewImage = null;
        PreviewCaption = "";
        Message = "已复制：改 Id 后保存即新相机";
    }

    [RelayCommand]
    private void Save()
    {
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

        // 超时预算校验：GrabTimeoutMs 必须小于总超时 TimeoutMs，且为正数
        if (string.Equals(entry.Type, "Basler", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.GrabTimeoutMs <= 0)
            {
                Message = "保存失败: 采集超时 GrabTimeoutMs 必须大于 0";
                return;
            }
            if (entry.GrabTimeoutMs >= _cfg.TimeoutMs)
            {
                Message = $"保存失败: 采集超时 {entry.GrabTimeoutMs}ms 不小于总超时 {_cfg.TimeoutMs}ms，取图超时将表现为 1008 而非 1003";
                return;
            }
        }

        try
        {
            var list = _cfg.Cameras
                .Where(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            list.Add(entry);

            _store.Save(list);

            // 运行时注册失败不影响配置保存（如 Basler 未接相机，之后接上重新保存即可）
            var error = TryRegister(entry);
            IsNew = false;
            _baseline = entry; // 保存后的配置即新基线（Refresh 的脏检查不会误弹）
            Message = error is null ? $"已保存 {id}（运行时已注册）" : $"已保存 {id}（运行时注册失败: {error}）";
            Refresh();
            _switching = true;
            Selected = Items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
            _switching = false;
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
                return clone;
            }
            return new CameraConfig { Id = id, Type = EditType };
        }

        var entry = new CameraConfig { Id = id, Type = EditType };
        switch (EditType)
        {
            case "Basler":
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
            Message = $"取图中 · {id}（{EditType}）";
            BitmapSource source;
            if (_cameras.TryGet(id, out var existing) && existing is not null)
            {
                // 已注册：直接用运行时实例（与产线链路同一实例，避免重复连接同一台相机）
                source = await Task.Run(() =>
                {
                    using var frame = existing.Grab();
                    return ImageConverter.ToBitmapSource(frame.Image);
                });
            }
            else
            {
                // 未注册：按当前编辑内容临时构造（先试后存）
                source = await Task.Run(() =>
                {
                    using var camera = CreateCamera(entry);
                    using var frame = camera.Grab();
                    return ImageConverter.ToBitmapSource(frame.Image);
                });
            }
            PreviewImage = source;
            PreviewCaption = $"测试取图 · {id}（{EditType}）";
            Message = $"取图成功 · {id}（{source.PixelWidth}×{source.PixelHeight}）";
        }
        catch (Exception ex)
        {
            PreviewImage = null;
            PreviewCaption = "";
            Message = $"取图失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EnumerateBaslerAsync()
    {
        BaslerDevices.Clear();
        HasBaslerDevices = false;
        try
        {
            Message = "枚举 Basler 设备中…";
            // 设备枚举能力经工厂注册表查询（IDeviceEnumerableFactory），不依赖具体品牌类；
            // pylon 枚举可能耗时（网络相机超时），后台执行避免冻结 UI
            var devices = await Task.Run(() => _registry.EnumerateDevices("Basler"));
            foreach (var d in devices)
                BaslerDevices.Add(d);
            HasBaslerDevices = devices.Count > 0;
            SelectedBaslerDevice = BaslerDevices.FirstOrDefault();
            Message = devices.Count > 0
                ? $"发现 {devices.Count} 台 Basler 相机，选择后自动填入序列号"
                : "未发现 Basler 相机（检查 pylon 安装、相机供电与网线）";
        }
        catch (Exception ex)
        {
            Message = $"枚举 Basler 设备失败: {ex.Message}（检查 pylon 运行库安装）";
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
        var resolved = AppConfigExtensions.ResolveFolder(EditFolder.Trim());
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
        var resolved = AppConfigExtensions.ResolveFolder(EditFolder.Trim());
        if (Directory.Exists(resolved))
            RecipeViewModel.ShellOpen(resolved);
        else
            Message = $"目录不存在: {resolved}";
    }

    [RelayCommand]
    private void OpenSettingsFolder() =>
        RecipeViewModel.ShellOpen(Path.GetDirectoryName(_store.SettingsPath)!);

    // ---- Basler 运行时调光 ----

    [RelayCommand]
    private async Task ReadLiveParamsAsync()
    {
        if (LiveExposureControl is not { } camera)
            return;
        var id = Selected?.Id ?? EditId.Trim();
        await LoadLiveParamsCore(camera, id);
    }

    /// <summary>后台读取 pylon 光度参数并回写（仅当选中相机未变时生效，避免串扰）。</summary>
    private async Task LoadLiveParamsCore(IExposureControl camera, string cameraId)
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
            Message = exposure is null && gain is null
                ? "读取相机参数失败（相机可能已断开）"
                : $"当前曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB";
        }
        catch (Exception ex)
        {
            if (string.Equals(Selected?.Id, cameraId, StringComparison.OrdinalIgnoreCase))
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
            Message = $"下发光度参数并取图中 · {id}";
            var source = await Task.Run(() =>
            {
                exposure.TrySetExposureTimeUs(ExposureUs);
                exposure.TrySetGain(Gain);
                using var frame = camera.Grab();
                return ImageConverter.ToBitmapSource(frame.Image);
            });
            PreviewImage = source;
            PreviewCaption = $"运行时调光预览 · {id}";
            Message = $"取图成功 · 曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB";
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
        if (entry is null || !string.Equals(entry.Type, "Basler", StringComparison.OrdinalIgnoreCase))
        {
            Message = $"相机 {id} 不是 Basler 类型，无法写回光度参数";
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

        // 未保存修改：确认后才切换（拒绝时恢复原选中）
        if (HasUnsavedChanges && !ConfirmDiscard("切换相机"))
        {
            _switching = true;
            Selected = Items.FirstOrDefault(i => string.Equals(i.Id, _baseline?.Id, StringComparison.OrdinalIgnoreCase));
            _switching = false;
            return;
        }

        IsNew = false;
        PreviewImage = null;
        PreviewCaption = "";

        var config = _cfg.Cameras.FirstOrDefault(
            c => string.Equals(c.Id, value.Id, StringComparison.OrdinalIgnoreCase));
        if (config is null)
            return;

        _baseline = config;
        EditId = config.Id;
        EditType = config.Type;
        EditFolder = config.Folder;
        EditDeviceId = config.DeviceId;
        EditExposureUs = config.ExposureTimeUs?.ToString("0") ?? "";
        EditGain = config.Gain?.ToString("0.##") ?? "";
        EditGrabTimeoutMs = config.GrabTimeoutMs;
        EditWidth = config.Width;
        EditHeight = config.Height;
        EditPattern = PatternOptions.Contains(config.Pattern, StringComparer.OrdinalIgnoreCase)
            ? config.Pattern
            : "Chessboard";
        EditCellPx = config.ChessCellPx;
        EditIntervalMs = config.IntervalMs;
        EditNoiseSigma = config.NoiseSigma.ToString("0.##");

        var live = LiveExposureControl;
        HasLiveExposureControl = live is not null;
        if (live is not null)
            // pylon 参数读取可能阻塞（断线/网络延迟），后台执行避免选中即卡 UI
            _ = LoadLiveParamsCore(live, value.Id);
        else
            Message = value.Registered
                ? ""
                : $"{value.Id} 未注册（" + (string.Equals(config.Type, "Basler", StringComparison.OrdinalIgnoreCase)
                    ? "未安装 pylon、相机未连接或序列号不符"
                    : "目录不存在或没有图片") + "）";
    }
}
