using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using RobotVision.Core.Models;

namespace RobotVision.UI;

/// <summary>配方列表项（含有效性/启用状态/描述，供列表展示与过滤）。</summary>
public sealed record RecipeListItem(
    string Name, string Summary, bool IsValid, bool IsEnabled = true, string? Description = null);

/// <summary>枚举选项（界面显示中文标签）。</summary>
public sealed record EnumItem<T>(T Value, string Label) where T : struct, Enum;

public sealed record RecipeAngleModeItem(string Value, string Label);

/// <summary>
/// 配方管理：列表（可搜索）+ 编辑表单。编辑始终作用于克隆副本，保存时校验、写回文件并刷新缓存；
/// 未保存的修改在切换配方/删除时弹确认，防止误丢；新建/改名与已有配方重名时弹覆盖确认。
/// </summary>
public partial class RecipeViewModel : ObservableObject
{
    private readonly RecipeLoader _loader;
    private readonly CameraManager _cameras;
    private readonly ModelManager _models;
    private readonly CalibrationManager _calibration;
    private readonly VisionService _vision;
    private readonly LightingManager _lighting;
    private readonly AngleStrategyTypeRegistry _angleRegistry;

    /// <summary>进入当前编辑状态时的基线副本（脏标记比较基准）。</summary>
    private RecipeConfig? _baseline;

    /// <summary>当前编辑配方的磁盘原名（覆盖确认与测试触发用；新建/复制时为空）。</summary>
    private string _originalName = "";

    /// <summary>最近一次确认切换的列表项（未保存确认失败时恢复选中）。</summary>
    private RecipeListItem? _lastConfirmed;

    /// <summary>防止 OnSelectedChanged 重入（程序内设置 Selected 时置位）。</summary>
    private bool _switching;

    public ObservableCollection<RecipeListItem> Recipes { get; } = [];

    [ObservableProperty]
    private RecipeListItem? _selected;

    [ObservableProperty]
    private RecipeConfig _editor = new();

    /// <summary>新建/复制模式下名称可编辑（已有配方改名 = 复制 + 删旧）。</summary>
    [ObservableProperty]
    private bool _isNew;

    [ObservableProperty]
    private string _message = "";

    /// <summary>测试触发进行中（防抖：连点不并发推理）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _searchText = "";

    public RecipeViewModel(
        RecipeLoader loader,
        CameraManager cameras,
        ModelManager models,
        CalibrationManager calibration,
        VisionService vision,
        LightingManager lighting,
        AngleStrategyTypeRegistry angleRegistry)
    {
        _loader = loader;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _vision = vision;
        _lighting = lighting;
        _angleRegistry = angleRegistry;
        Refresh();
    }

    /// <summary>相机增删后通知下拉重新求值（页面 Loaded 时调用）。</summary>
    public void RefreshCameras() => OnPropertyChanged(nameof(CameraIds));

    public IReadOnlyList<string> CameraIds => _cameras.CameraIds.ToList();

    /// <summary>models 目录 .onnx 文件列表（下拉选择，避免手填拼错）。</summary>
    public IReadOnlyList<string> ModelFiles => _models.ModelFileNames;

    /// <summary>已标定工位列表（外参档案去重，下拉选择避免手填触发 1004）。</summary>
    public IReadOnlyList<string> StationIds => _calibration.ExtrinsicProfiles
        .Select(p => p.StationId)
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>已注册光源控制器列表（下拉选择；为空 = 未配置任何光源）。</summary>
    public IReadOnlyList<string> LightControllerIds => _lighting.ControllerIds.ToList();

    // ---- 光源（扁平编辑字段，直接作用于 Editor.Lighting/LightControllerId，参与脏标记）----

    /// <summary>是否启用光源（取图前点亮）。</summary>
    public bool UseLighting
    {
        get => Editor.Lighting is not null;
        set
        {
            if (value)
            {
                Editor.Lighting ??= NewLightingConfig();
                Editor.LightControllerId ??= LightControllerIds.FirstOrDefault();
            }
            else
            {
                Editor.Lighting = null;
                Editor.LightControllerId = null;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    public string? SelectedLightControllerId
    {
        get => Editor.LightControllerId;
        set
        {
            Editor.LightControllerId = string.IsNullOrWhiteSpace(value) ? null : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    public int LightChannel
    {
        get => Editor.Lighting?.Channels.FirstOrDefault()?.Channel ?? 1;
        set
        {
            if (Editor.Lighting is { } l)
            {
                Channel0(l).Channel = Math.Max(1, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    public int LightBrightness
    {
        get => Editor.Lighting?.Channels.FirstOrDefault()?.Brightness ?? 128;
        set
        {
            if (Editor.Lighting is { } l)
            {
                Channel0(l).Brightness = Math.Clamp(value, 0, 255);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    public int LightStabilizeDelayMs
    {
        get => Editor.Lighting?.StabilizeDelayMs ?? 0;
        set
        {
            if (Editor.Lighting is { } l)
            {
                l.StabilizeDelayMs = Math.Max(0, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    public bool LightTurnOffAfterGrab
    {
        get => Editor.Lighting?.TurnOffAfterGrab ?? true;
        set
        {
            if (Editor.Lighting is { } l)
            {
                l.TurnOffAfterGrab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    private static LightingConfig NewLightingConfig() => new()
    {
        Channels = [new LightingChannelConfig { Channel = 1, Brightness = 128 }],
        StabilizeDelayMs = 0,
        TurnOffAfterGrab = true,
    };

    private static LightingChannelConfig Channel0(LightingConfig lighting)
    {
        if (lighting.Channels.Count == 0)
            lighting.Channels.Add(new LightingChannelConfig());
        return lighting.Channels[0];
    }

    // ---- 检测区域 ROI（相对比例 0~1；null = 全图推理）----

    /// <summary>是否启用检测区域（裁剪后推理，大图局部检测显著降低 CPU 耗时）。</summary>
    public bool UseRoi
    {
        get => Editor.Roi is not null;
        set
        {
            if (value)
                Editor.Roi ??= new Roi(0, 0, 1, 1);
            else
                Editor.Roi = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    public double RoiX
    {
        get => Editor.Roi?.X ?? 0;
        set => SetRoi(r => r with { X = Math.Clamp(value, 0, 1) });
    }

    public double RoiY
    {
        get => Editor.Roi?.Y ?? 0;
        set => SetRoi(r => r with { Y = Math.Clamp(value, 0, 1) });
    }

    public double RoiWidth
    {
        get => Editor.Roi?.Width ?? 1;
        set => SetRoi(r => r with { Width = Math.Clamp(value, 0, 1) });
    }

    public double RoiHeight
    {
        get => Editor.Roi?.Height ?? 1;
        set => SetRoi(r => r with { Height = Math.Clamp(value, 0, 1) });
    }

    private void SetRoi(Func<Roi, Roi> update)
    {
        if (Editor.Roi is { } roi)
        {
            Editor.Roi = update(roi);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    /// <summary>按名称/描述过滤后的列表（搜索框驱动）。</summary>
    public IEnumerable<RecipeListItem> VisibleRecipes =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Recipes
            : Recipes.Where(r =>
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>编辑内容相对保存态是否有差异（脏标记）。</summary>
    public bool HasUnsavedChanges =>
        _baseline is not null && !SameRecipe(Editor, _baseline);

    /// <summary>有未保存修改时的常驻提示（UI 提示条）。</summary>
    public string UnsavedHint => HasUnsavedChanges
        ? "有未保存的修改：测试触发仍用磁盘旧配方，切换/刷新将丢弃"
        : "";

    /// <summary>未保存修改确认框；用户拒绝时返回 false 并中止当前操作。</summary>
    private bool ConfirmDiscard(string action)
    {
        return MessageBox.Show($"配方 {_originalName} 有未保存的修改，{action}将丢弃这些修改。继续？",
            "未保存修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    /// <summary>仅当选中配方有效时可测试触发。</summary>
    public bool CanTestTrigger => Selected is { IsValid: true };

    /// <summary>角度模式下拉数据源：来自策略工厂注册表（新注册的工厂自动出现，与相机类型下拉同构）。</summary>
    public IReadOnlyList<EnumItem<AngleMode>> AngleModeOptions =>
        _angleRegistry.Factories
            .Select(f => new EnumItem<AngleMode>(f.Mode, f.Label))
            .ToList();

    public IReadOnlyList<EnumItem<Core.Recipe.RotationCompensationMode>> RotationOptions { get; } =
    [
        new(Core.Recipe.RotationCompensationMode.None, "不补偿"),
        new(Core.Recipe.RotationCompensationMode.EccentricTool, "偏心工具补偿（需旋转中心标定）"),
    ];

    public string PrimaryModel
    {
        get => Editor.Models.Count > 0 ? Editor.Models[0] : "";
        set
        {
            while (Editor.Models.Count < 1)
                Editor.Models.Add("");
            Editor.Models[0] = value;
        }
    }

    public string SecondaryModel
    {
        get => Editor.Models.Count > 1 ? Editor.Models[1] : "";
        set
        {
            while (Editor.Models.Count < 2)
                Editor.Models.Add("");
            Editor.Models[1] = value;
        }
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(VisibleRecipes));

    partial void OnEditorChanged(RecipeConfig value) => OnPropertyChanged(nameof(HasUnsavedChanges));

    [RelayCommand]
    public void Refresh()
    {
        // 未保存修改：确认后才丢弃（与相机页一致）
        if (HasUnsavedChanges && !ConfirmDiscard("刷新列表"))
            return;

        Recipes.Clear();
        foreach (var name in _loader.ListNames())
            Recipes.Add(DescribeItem(name));

        // 下拉数据源：models 目录 / 外参档案 / 光源控制器可能已变化
        OnPropertyChanged(nameof(ModelFiles));
        OnPropertyChanged(nameof(StationIds));
        OnPropertyChanged(nameof(LightControllerIds));

        _switching = true;
        Selected = Recipes.FirstOrDefault(r => r.Name == Selected?.Name) ?? Recipes.FirstOrDefault();
        _switching = false;
        _lastConfirmed = Selected;
        Message = $"共 {Recipes.Count} 个配方";
        OnPropertyChanged(nameof(VisibleRecipes));
        OnPropertyChanged(nameof(CanTestTrigger));
        TestTriggerCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void New()
    {
        // 未保存修改：确认后才丢弃
        if (HasUnsavedChanges && !ConfirmDiscard("新建配方"))
            return;
        IsNew = true;
        _originalName = "";
        Editor = new RecipeConfig
        {
            Name = "",
            CameraId = CameraIds.FirstOrDefault() ?? "",
            Models = [""],
        };
        _baseline = Editor.Clone();
        NotifyEditorBindings();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(CanTestTrigger));
        TestTriggerCommand.NotifyCanExecuteChanged();
        Message = "新建配方：填写名称与参数后保存";
    }

    [RelayCommand]
    private void Copy()
    {
        // 未保存修改：确认后才丢弃
        if (HasUnsavedChanges && !ConfirmDiscard("复制配方"))
            return;
        var source = Editor;
        var copy = source.Clone();
        copy.Name = source.Name.Length > 0 ? source.Name + "_copy" : "";
        IsNew = true;
        _originalName = "";
        Editor = copy;
        _baseline = copy.Clone();
        NotifyEditorBindings();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(CanTestTrigger));
        TestTriggerCommand.NotifyCanExecuteChanged();
        Message = $"已复制 {source.Name}：改名后保存即新配方";
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            // 空名/非法名先给友好提示（RecipeLoader 的校验文案面向配置格式，不面向表单）
            if (string.IsNullOrWhiteSpace(Editor.Name))
            {
                Message = "保存失败：请先填写配方名称";
                return;
            }
            if (!RecipeLoader.IsValidRecipeName(Editor.Name))
            {
                Message = "保存失败：名称只允许字母、数字、下划线、中划线（长度 ≤ 64）";
                return;
            }

            // 重名覆盖确认：新建/改名后目标名已存在且非当前原名时提示
            var isRename = IsNew || !string.Equals(Editor.Name, _originalName, StringComparison.OrdinalIgnoreCase);
            if (isRename && _loader.FileExists(Editor.Name) &&
                MessageBox.Show($"配方 {Editor.Name} 已存在，保存将覆盖现有内容。继续？",
                    "覆盖确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // 双模型模式保留两个条目，其余压缩为单个（空串过滤）
            var models = new[] { PrimaryModel, SecondaryModel }
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Editor.Models = models;

            // 光源成对校验：启用光源必须有控制器（值域校验由 RecipeLoader 兜底）
            if (Editor.Lighting is not null && string.IsNullOrWhiteSpace(Editor.LightControllerId))
            {
                Message = "保存失败：已启用光源但未选择光源控制器（appsettings LightControllers 未配置时先添加 None 类型）";
                return;
            }

            _loader.Save(Editor);
            IsNew = false;
            _originalName = Editor.Name;
            _baseline = Editor.Clone();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            Message = $"已保存 {Editor.Name}";
            Refresh();
        }
        catch (Exception ex)
        {
            Message = $"保存失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is null)
            return;

        var prompt = HasUnsavedChanges
            ? $"配方 {_originalName} 有未保存的修改，删除将一并丢弃。确定删除配方 {Selected.Name}？（不可恢复）"
            : $"确定删除配方 {Selected.Name}？（不可恢复）";

        if (MessageBox.Show(prompt, "删除配方",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            _loader.Delete(Selected.Name);
            Message = $"已删除 {Selected.Name}";
            _lastConfirmed = null;
            Refresh();
            // 重新载入列表首项，让编辑器离开被删配方
            if (Selected is { } item)
            {
                LoadIntoEditor(item.Name);
                _lastConfirmed = Selected;
            }
        }
        catch (Exception ex)
        {
            Message = $"删除失败：{ex.Message}";
        }
    }

    /// <summary>测试触发：用磁盘上的配方跑一次完整链路（取图→去畸变→推理→外参）。</summary>
    private bool CanOperate => !IsBusy && CanTestTrigger;

    partial void OnIsBusyChanged(bool value) => TestTriggerCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task TestTriggerAsync()
    {
        if (string.IsNullOrEmpty(_originalName))
            return;

        IsBusy = true;
        try
        {
            Message = $"测试触发中：{_originalName} …";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await _vision.RunAsync(_originalName, cts.Token);
            Message = result.Ok
                ? $"测试通过：{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms"
                : $"测试失败：ERR {result.ErrorCode} · {result.Message}";
        }
        catch (Exception ex)
        {
            Message = $"测试异常：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFolder() => ShellOpen(_loader.Folder);

    partial void OnSelectedChanged(RecipeListItem? value)
    {
        if (_switching || value is null)
            return;

        // 未保存修改：确认后才丢弃
        if (_lastConfirmed is not null && HasUnsavedChanges &&
            MessageBox.Show($"配方 {_originalName} 有未保存的修改，切换将丢弃这些修改。继续？",
                "未保存修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            _switching = true;
            Selected = _lastConfirmed;
            _switching = false;
            return;
        }

        LoadIntoEditor(value.Name);
        _lastConfirmed = value;
    }

    /// <summary>把磁盘配方载入编辑器并设置脏标记基线。</summary>
    private void LoadIntoEditor(string name)
    {
        IsNew = false;
        _originalName = name;
        try
        {
            var loaded = _loader.Get(name);
            Editor = loaded.Clone();
            _baseline = Editor.Clone();
            NotifyEditorBindings();
            Message = loaded.Enabled ? "" : $"配方 {name} 已停用（Enabled=false），触发将返回 1001";
        }
        catch (Exception ex)
        {
            Editor = new RecipeConfig { Name = name };
            _baseline = Editor.Clone();
            NotifyEditorBindings();
            Message = $"读取失败：{ex.Message}";
        }
        finally
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            OnPropertyChanged(nameof(CanTestTrigger));
            TestTriggerCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Editor 替换后，通知派生绑定（模型文件名代理属性、光源扁平字段）重新求值。</summary>
    private void NotifyEditorBindings()
    {
        OnPropertyChanged(nameof(PrimaryModel));
        OnPropertyChanged(nameof(SecondaryModel));
        OnPropertyChanged(nameof(UseLighting));
        OnPropertyChanged(nameof(SelectedLightControllerId));
        OnPropertyChanged(nameof(LightChannel));
        OnPropertyChanged(nameof(LightBrightness));
        OnPropertyChanged(nameof(LightStabilizeDelayMs));
        OnPropertyChanged(nameof(LightTurnOffAfterGrab));
        OnPropertyChanged(nameof(UseRoi));
        OnPropertyChanged(nameof(RoiX));
        OnPropertyChanged(nameof(RoiY));
        OnPropertyChanged(nameof(RoiWidth));
        OnPropertyChanged(nameof(RoiHeight));
    }

    private RecipeListItem DescribeItem(string name)
    {
        try
        {
            var r = _loader.Get(name);
            var mode = r.AngleMode switch
            {
                AngleMode.MaskMinAreaRect => "分割",
                AngleMode.DualCenterLine => "双模型",
                AngleMode.KeyPointLine => "关键点",
                _ => r.AngleMode.ToString(),
            };
            return new RecipeListItem(
                name, $"{mode} · {r.CameraId} · {r.Models.FirstOrDefault("")}",
                true, r.Enabled, r.Description);
        }
        catch (Exception ex)
        {
            return new RecipeListItem(name, ex.Message, false);
        }
    }

    /// <summary>配方逐字段比较（脏标记用）。</summary>
    private static bool SameRecipe(RecipeConfig a, RecipeConfig b)
    {
        if (a.Name != b.Name || a.CameraId != b.CameraId || a.StationId != b.StationId ||
            a.DebugPassthrough != b.DebugPassthrough || a.AngleMode != b.AngleMode ||
            a.Confidence != b.Confidence || a.Iou != b.Iou || a.PixelConfidence != b.PixelConfidence ||
            a.KeypointIndexA != b.KeypointIndexA || a.KeypointIndexB != b.KeypointIndexB ||
            a.KeypointMinConfidence != b.KeypointMinConfidence ||
            a.PairingMaxDistancePx != b.PairingMaxDistancePx ||
            a.RotationCompensation != b.RotationCompensation || a.SchemaVersion != b.SchemaVersion ||
            a.Enabled != b.Enabled || a.Description != b.Description ||
            a.LightControllerId != b.LightControllerId || a.Roi != b.Roi || !SameLighting(a.Lighting, b.Lighting))
            return false;
        return a.Models.SequenceEqual(b.Models, StringComparer.OrdinalIgnoreCase);
    }

    private static bool SameLighting(LightingConfig? a, LightingConfig? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return a.StabilizeDelayMs == b.StabilizeDelayMs &&
               a.TurnOffAfterGrab == b.TurnOffAfterGrab &&
               a.Channels.Select(c => (c.Channel, c.Brightness))
                   .SequenceEqual(b.Channels.Select(c => (c.Channel, c.Brightness)));
    }

    internal static void ShellOpen(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"")
        {
            UseShellExecute = true,
        });
    }
}
