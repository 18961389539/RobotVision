using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方列表项（含有效性/启用状态/描述，供列表展示与过滤）。</summary>
public sealed record RecipeListItem(
    string Name, string Summary, bool IsValid, bool IsEnabled = true, string? Description = null);

/// <summary>枚举选项（界面显示中文标签）。</summary>
public sealed record EnumItem<T>(T Value, string Label) where T : struct, Enum;

public sealed record RecipeAngleModeItem(string Value, string Label);

/// <summary>
/// 配方管理：列表 + 编辑表单。ROI / 光源 / 试触发由协作对象承担，本类保留 DataContext。
/// </summary>
public partial class RecipeViewModel : ObservableObject, ICommitPendingEdits, IRecipeWorkspace, IDisposable
{
    private readonly RecipeLoader _loader;
    private readonly AppConfig _cfg;
    private readonly CameraManager _cameras;
    private readonly ModelManager _models;
    private readonly CalibrationManager _calibration;
    private readonly AngleStrategyTypeRegistry _angleRegistry;
    private readonly AssetIntegrityChecker _assets;
    private readonly DispatcherTimer _dirtyTimer;

    private RecipeConfig? _baseline;
    private string _originalName = "";
    private RecipeListItem? _lastConfirmed;
    private bool _switching;

    public Action? FlushPendingEdits { get; set; }

    public RecipeRoiEditor Roi { get; }
    public RecipeLightingEditor Lighting { get; }
    public RecipeTestSession Test { get; }

    public ObservableCollection<RecipeListItem> Recipes { get; } = [];

    [ObservableProperty]
    private RecipeListItem? _selected;

    [ObservableProperty]
    private RecipeConfig _editor = new();

    [ObservableProperty]
    private bool _isNew;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isListPanelVisible = true;

    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    [ObservableProperty]
    private bool _showTestImage = true;

    [ObservableProperty]
    private string _searchText = "";

    string IRecipeWorkspace.OriginalName => _originalName;

    public RecipeViewModel(
        RecipeLoader loader,
        AppConfig cfg,
        CameraManager cameras,
        ModelManager models,
        CalibrationManager calibration,
        VisionService vision,
        LightingManager lighting,
        AngleStrategyTypeRegistry angleRegistry,
        TcpServerManager tcp,
        AssetIntegrityChecker assets)
    {
        _loader = loader;
        _cfg = cfg;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _angleRegistry = angleRegistry;
        _assets = assets;
        Roi = new RecipeRoiEditor(this, cameras);
        Lighting = new RecipeLightingEditor(this, lighting);
        Test = new RecipeTestSession(this, vision, cameras, models, calibration, lighting, tcp);
        Roi.PropertyChanged += OnRoiOrTestChanged;
        Test.PropertyChanged += OnRoiOrTestChanged;
        _dirtyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _dirtyTimer.Tick += (_, _) => NotifyEditorMutated();
        Refresh();
    }

    public bool HasAnyImage => Test.ResultImage is not null || Roi.PreviewImage is not null;

    public bool ShowTestImageViewer => ShowTestImage && (Test.ResultImage is not null || IsBusy);

    public bool ShowRoiImageViewer => !ShowTestImage && Roi.PreviewImage is not null;

    [RelayCommand]
    private void ToggleListPanel() => IsListPanelVisible = !IsListPanelVisible;

    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;

    [RelayCommand]
    private void ShowTestImageView() => ShowTestImage = true;

    [RelayCommand]
    private void ShowRoiPreviewView() => ShowTestImage = false;

    public void StartDirtyWatch() => _dirtyTimer.Start();

    public void StopDirtyWatch() => _dirtyTimer.Stop();

    public void NotifyEditorMutated()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(RotationCenterHint));
        OnPropertyChanged(nameof(MappingHint));
        OnPropertyChanged(nameof(AngleModeHint));
        OnPropertyChanged(nameof(IsDualMode));
        OnPropertyChanged(nameof(IsKeyPointMode));
        OnPropertyChanged(nameof(IsSegmentationMode));
        OnPropertyChanged(nameof(IsMaskTemplateMode));
        OnPropertyChanged(nameof(IsDualBlobMode));
        OnPropertyChanged(nameof(IsTemplateMethod));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(ShowBlobFixedThreshold));
        OnPropertyChanged(nameof(ShowDualCropExpand));
        OnPropertyChanged(nameof(RefineMethodHint));
        OnPropertyChanged(nameof(PrimaryModel));
        OnPropertyChanged(nameof(SecondaryModel));
        OnPropertyChanged(nameof(AssetPinStatus));
        Test.NotifyCanExecuteChanged();
    }

    void IRecipeWorkspace.CommitEdits() => this.Commit();

    void IRecipeWorkspace.NotifyDirty()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
    }

    void IRecipeWorkspace.OnTestStarting() => ShowTestImage = true;

    public void RefreshCameras()
    {
        OnPropertyChanged(nameof(CameraIds));
        OnPropertyChanged(nameof(CameraOptions));
    }

    public void RefreshStationIds() => OnPropertyChanged(nameof(StationIds));

    public IReadOnlyList<string> CameraIds => _cameras.CameraIds.ToList();

    public IReadOnlyList<CameraOption> CameraOptions =>
        CameraOption.FromRegistered(_cfg.Cameras, _cameras.CameraIds);

    public IReadOnlyList<string> ModelFiles => _models.ModelFileNames;

    public IReadOnlyList<string> StationIds =>
        _calibration.ExtrinsicProfiles
            .Select(p => p.StationId)
            .Concat(_calibration.PolynomialProfiles.Select(p => p.StationId))
            .Concat(_calibration.ScaleProfiles.Select(p => p.StationId))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public string RotationCenterHint =>
        Editor.RotationCompensation == RotationCompensationMode.EccentricTool &&
        (string.IsNullOrWhiteSpace(Editor.StationId) ||
         !_calibration.RotationCenterProfiles.Any(p =>
             string.Equals(p.StationId, Editor.StationId, StringComparison.OrdinalIgnoreCase)))
            ? $"工位 {Editor.StationId ?? "（空）"} 未做旋转轴心标定：偏心补偿保存/触发将被拒绝，请先在标定向导完成轴心标定"
            : "";

    public string MappingHint
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Editor.StationId))
                return "未选工位：检出目标后将返回 1004，请选外参/多项式/比例标定档案";
            var station = Editor.StationId!;
            var poly = _calibration.PolynomialProfiles.FirstOrDefault(p =>
                string.Equals(p.StationId, station, StringComparison.OrdinalIgnoreCase));
            var hasExt = _calibration.HasExtrinsic(station);
            if (poly is not null && hasExt)
                return $"工位 {station} 同时有多项式与外参：生产只用多项式（原图），外参被忽略";
            if (poly is not null &&
                string.Equals(poly.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
                return $"工位 {station} 为棋盘毫米系（非机器人基座标），PLC 不能直接当 TCP 坐标使用";
            if (poly is not null &&
                string.Equals(poly.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                poly.HasTeachPose)
                return $"工位 {station} 为末端相机：触发行必须带 X,Y,RZ，否则 1014";
            var ext = _calibration.ExtrinsicProfiles.FirstOrDefault(p =>
                string.Equals(p.StationId, station, StringComparison.OrdinalIgnoreCase));
            if (ext is not null &&
                string.Equals(ext.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                ext.HasTeachPose)
                return $"工位 {station} 为末端相机：触发行必须带 X,Y,RZ，否则 1014";
            if (_calibration.GetScale(station) is not null && poly is null && !hasExt)
                return $"工位 {station} 为比例标定（图像平面 mm，非机器人基座标），PLC 不能直接当 TCP 坐标使用";
            return "";
        }
    }

    public string AngleModeHint => Editor.AngleMode switch
    {
        AngleMode.MaskMinAreaRect => "最小外接矩形角度为 [0,180)，无头尾方向；偏心工具补偿可能差 180°",
        AngleMode.DualCenterLine => "默认全局就近配对，多目标间距接近时可能配错；开「窗口配对」后 B 只在 A 外扩窗口内检测，多目标不配错",
        AngleMode.MaskTemplate => Editor.Template.RefineMethod == SegmentRefineMethod.Template
            ? "模板匹配失败会回退粗角度 [0,180)（无方向）；示教可单独框选特征（不必等于检测 ROI）；须与生产同一套照明"
            : "分割给粗框，精修方法在下方选择。非模板方法免示教模板；精修失败回退粗角度 [0,180)",
        AngleMode.DualBlobCenterLine => "主BLOB质心定位、主→次质心定向（有方向）；次BLOB缺失该目标不输出；无需模型",
        _ => "",
    };

    public string RefineMethodHint =>
        Editor.AngleMode != AngleMode.MaskTemplate
            ? ""
            : Editor.Template.RefineMethod switch
            {
                SegmentRefineMethod.LineFit =>
                    "直线拟合吃掩码长边，角度无方向 [0,180)，不需要示教模板。",
                SegmentRefineMethod.CentroidHoleLine =>
                    "质心连到掩码内最大孔/槽，有头尾。分割须能画出孔或槽。免示教模板。",
                SegmentRefineMethod.CaliperTab =>
                    "卡尺自动放在两条长边上（躲开端头与凸起），短轴中心取两线中线；黄线指向暗凸起一侧。配方测试画面会叠加青色探针、抓边点与品红拟合边；监控页默认不画。切到此方法后抓取原点与模板中心不同，需重新对示教。",
                _ => "",
            };

    public IEnumerable<RecipeListItem> VisibleRecipes =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Recipes
            : Recipes.Where(r =>
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

    public bool HasUnsavedChanges =>
        _baseline is not null && !RecipeCompare.Same(Editor, _baseline);

    public string UnsavedHint => HasUnsavedChanges
        ? "有未保存的修改：测试触发仍用磁盘旧配方，切换/刷新将丢弃"
        : "";

    public bool CanTestTrigger => Selected is { IsValid: true };

    public IReadOnlyList<EnumItem<AngleMode>> AngleModeOptions =>
        _angleRegistry.Factories
            .Select(f => new EnumItem<AngleMode>(f.Mode, f.Label))
            .ToList();

    public IReadOnlyList<EnumItem<RotationCompensationMode>> RotationOptions { get; } =
    [
        new(RotationCompensationMode.None, "不补偿"),
        new(RotationCompensationMode.EccentricTool, "偏心工具补偿（需旋转中心标定）"),
    ];

    public string PrimaryModel
    {
        get => Editor.Models.Count > 0 ? Editor.Models[0] : "";
        set
        {
            while (Editor.Models.Count < 1)
                Editor.Models.Add("");
            Editor.Models[0] = value;
            NotifyEditorMutated();
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
            NotifyEditorMutated();
        }
    }

    public bool IsDualMode => Editor.AngleMode == AngleMode.DualCenterLine;
    public bool IsKeyPointMode => Editor.AngleMode == AngleMode.KeyPointLine;
    public bool IsSegmentationMode => Editor.AngleMode == AngleMode.MaskMinAreaRect;
    public bool IsMaskTemplateMode => Editor.AngleMode == AngleMode.MaskTemplate;
    public bool IsDualBlobMode => Editor.AngleMode == AngleMode.DualBlobCenterLine;
    public bool ShowBlobFixedThreshold => IsDualBlobMode && !Editor.Blob.UseOtsu;
    public bool HasTemplate => !string.IsNullOrEmpty(Editor.Template.TemplateImageBase64);

    public IReadOnlyList<EnumItem<SegmentRefineMethod>> RefineMethodOptions { get; } =
    [
        new(SegmentRefineMethod.Template, "模板匹配（需示教，可判头尾）"),
        new(SegmentRefineMethod.LineFit, "直线拟合（弱纹理矩形，免示教）"),
        new(SegmentRefineMethod.CentroidHoleLine, "质心-内标连线（掩码有孔/槽，有方向）"),
        new(SegmentRefineMethod.CaliperTab, "卡尺长边+凸起极性（免示教，有方向）"),
    ];

    public bool IsTemplateMethod => Editor.Template.RefineMethod == SegmentRefineMethod.Template;

    public bool ShowDualCropExpand => IsDualMode && Editor.DualModel.CropWindowPairing;

    public void NotifyAngleModeChanged() => NotifyEditorMutated();

    public string AssetPinStatus
    {
        get
        {
            var pinnedModels = Editor.ModelSha256.Count(h => !string.IsNullOrWhiteSpace(h));
            var pinnedStation = !string.IsNullOrWhiteSpace(Editor.StationSha256);
            if (pinnedModels == 0 && !pinnedStation)
                return "未钉扎：拷错同名 ONNX 或覆盖标定档案时不会被拦截。验证通过后请钉死哈希。";

            try
            {
                var (hashes, station) = _assets.Snapshot(Editor);
                var modelOk = true;
                for (var i = 0; i < Editor.ModelSha256.Count; i++)
                {
                    var pin = Editor.ModelSha256[i];
                    if (string.IsNullOrWhiteSpace(pin))
                        continue;
                    var actual = i < hashes.Count ? hashes[i] : "";
                    if (!RobotVision.Core.Assets.FileSha256.EqualsHex(pin, actual))
                    {
                        modelOk = false;
                        break;
                    }
                }

                var stationOk = !pinnedStation ||
                    RobotVision.Core.Assets.FileSha256.EqualsHex(Editor.StationSha256, station);
                if (modelOk && stationOk)
                    return pinnedStation
                        ? $"已钉扎 {pinnedModels} 个模型 + 工位，与当前文件一致"
                        : $"已钉扎 {pinnedModels} 个模型，与当前文件一致";
                return "钉扎与当前文件不一致：TRIGGER 将返回 1017。请核对文件或重新钉扎后保存。";
            }
            catch (Exception ex)
            {
                return $"无法核对当前哈希：{ex.Message}";
            }
        }
    }

    [RelayCommand]
    public void Refresh() => Refresh(preferName: null, reloadEditor: true);

    public void Refresh(string? preferName, bool reloadEditor, bool ignoreUnsaved = false)
    {
        if (!ignoreUnsaved && HasUnsavedChanges && !ConfirmDiscard("刷新列表"))
            return;

        var keepName = preferName ?? Selected?.Name ?? Editor.Name;

        Recipes.Clear();
        foreach (var name in _loader.ListNames())
            Recipes.Add(DescribeItem(name));

        OnPropertyChanged(nameof(ModelFiles));
        OnPropertyChanged(nameof(StationIds));
        Lighting.RefreshControllerIds();

        _switching = true;
        Selected = string.IsNullOrWhiteSpace(keepName)
            ? Recipes.FirstOrDefault()
            : Recipes.FirstOrDefault(r => string.Equals(r.Name, keepName, StringComparison.OrdinalIgnoreCase));
        _switching = false;
        _lastConfirmed = Selected;
        if (reloadEditor && Selected is not null)
            LoadIntoEditor(Selected.Name);
        Message = $"共 {Recipes.Count} 个配方";
        OnPropertyChanged(nameof(VisibleRecipes));
        OnPropertyChanged(nameof(CanTestTrigger));
        Test.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void New()
    {
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
        Test.NotifyCanExecuteChanged();
        Message = "新建配方：填写名称与参数后保存";
    }

    [RelayCommand]
    private void Copy()
    {
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
        Test.NotifyCanExecuteChanged();
        Message = $"已复制 {source.Name}：改名后保存即新配方";
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            this.Commit();

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

            var previousName = ResolvePreviousDiskName();

            var isRename = IsNew ||
                !string.Equals(Editor.Name, previousName, StringComparison.OrdinalIgnoreCase);
            if (isRename && _loader.FileExists(Editor.Name) &&
                MessageBox.Show($"配方 {Editor.Name} 已存在，保存将覆盖现有内容。继续？",
                    "覆盖确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var models = new[] { PrimaryModel, SecondaryModel }
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Editor.Models = models;

            if (Editor.Lighting is not null && string.IsNullOrWhiteSpace(Editor.LightControllerId))
            {
                Message = "保存失败：已启用光源但未选择光源控制器（appsettings LightControllers 未配置时先添加 None 类型）";
                return;
            }

            _loader.Save(Editor, IsNew ? null : previousName);

            var savedMessage = isRename && !string.IsNullOrEmpty(previousName) &&
                !string.Equals(Editor.Name, previousName, StringComparison.OrdinalIgnoreCase)
                ? $"已保存 {Editor.Name}（原 {previousName} 已重命名）"
                : $"已保存 {Editor.Name}";

            IsNew = false;
            _originalName = Editor.Name;
            _baseline = Editor.Clone();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(UnsavedHint));
            Refresh(Editor.Name, reloadEditor: false, ignoreUnsaved: true);
            Message = savedMessage;
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

        var prompt = HasUnsavedChanges && string.Equals(Selected.Name, _originalName, StringComparison.OrdinalIgnoreCase)
            ? $"配方 {Selected.Name} 有未保存的修改，删除将一并丢弃。确定删除？（不可恢复）"
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

    [RelayCommand]
    private void OpenFolder() => Explorer.OpenFolder(_loader.Folder);

    [RelayCommand]
    private void PinAssets()
    {
        this.Commit();
        try
        {
            var (hashes, station) = _assets.Snapshot(Editor);
            Editor.ModelSha256 = hashes;
            Editor.StationSha256 = station;
            NotifyEditorMutated();
            var modelN = hashes.Count(h => !string.IsNullOrWhiteSpace(h));
            Message = station is null
                ? $"已钉扎 {modelN} 个模型哈希（无工位标定指纹）；请保存配方"
                : $"已钉扎 {modelN} 个模型哈希 + 工位标定指纹；请保存配方";
        }
        catch (Exception ex)
        {
            Message = $"钉扎失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearAssetPins()
    {
        Editor.ModelSha256 = [];
        Editor.StationSha256 = null;
        NotifyEditorMutated();
        Message = "已清除哈希钉扎（须保存后生效）；TRIGGER 不再校验 1017";
    }

    public void Dispose()
    {
        _dirtyTimer.Stop();
        Roi.PropertyChanged -= OnRoiOrTestChanged;
        Test.PropertyChanged -= OnRoiOrTestChanged;
        Test.EndSnapshotAwait();
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(VisibleRecipes));

    partial void OnEditorChanged(RecipeConfig value)
    {
        NotifyEditorMutated();
        Roi.ClearReferenceFrame();
    }

    partial void OnShowTestImageChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTestImageViewer));
        OnPropertyChanged(nameof(ShowRoiImageViewer));
    }

    partial void OnIsBusyChanged(bool value)
    {
        Test.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowTestImageViewer));
        OnPropertyChanged(nameof(ShowRoiImageViewer));
    }

    partial void OnSelectedChanged(RecipeListItem? value)
    {
        if (_switching || value is null)
            return;

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

    private void OnRoiOrTestChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeTestSession.ResultImage) && Test.ResultImage is not null)
            ShowTestImage = true;
        else if (e.PropertyName == nameof(RecipeRoiEditor.PreviewImage) && Roi.PreviewImage is not null)
            ShowTestImage = false;

        if (e.PropertyName is nameof(RecipeTestSession.ResultImage) or nameof(RecipeRoiEditor.PreviewImage))
        {
            OnPropertyChanged(nameof(HasAnyImage));
            OnPropertyChanged(nameof(ShowTestImageViewer));
            OnPropertyChanged(nameof(ShowRoiImageViewer));
        }
    }

    private bool ConfirmDiscard(string action) =>
        MessageBox.Show($"配方 {_originalName} 有未保存的修改，{action}将丢弃这些修改。继续？",
            "未保存修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private string ResolvePreviousDiskName()
    {
        if (IsNew)
            return "";

        if (!string.IsNullOrEmpty(_originalName) && _loader.FileExists(_originalName))
            return _originalName;

        if (Selected is not null && _loader.FileExists(Selected.Name))
            return Selected.Name;

        return _originalName;
    }

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
            Message = loaded.Enabled ? "" : $"配方 {name} 已停用（Enabled=false），触发将返回 1015";
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
            Test.NotifyCanExecuteChanged();
        }
    }

    private void NotifyEditorBindings()
    {
        OnPropertyChanged(nameof(PrimaryModel));
        OnPropertyChanged(nameof(SecondaryModel));
        Lighting.NotifyFromEditor();
        Roi.NotifyFromEditor();
        OnPropertyChanged(nameof(RotationCenterHint));
        OnPropertyChanged(nameof(IsDualMode));
        OnPropertyChanged(nameof(IsKeyPointMode));
        OnPropertyChanged(nameof(IsSegmentationMode));
        OnPropertyChanged(nameof(IsMaskTemplateMode));
        OnPropertyChanged(nameof(IsDualBlobMode));
        OnPropertyChanged(nameof(IsTemplateMethod));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(ShowBlobFixedThreshold));
        OnPropertyChanged(nameof(ShowDualCropExpand));
        OnPropertyChanged(nameof(AngleModeHint));
        OnPropertyChanged(nameof(RefineMethodHint));
        OnPropertyChanged(nameof(MappingHint));
        OnPropertyChanged(nameof(AssetPinStatus));
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
                AngleMode.MaskTemplate => r.Template.RefineMethod switch
                {
                    SegmentRefineMethod.Template => "分割+模板",
                    SegmentRefineMethod.LineFit => "分割+直线",
                    SegmentRefineMethod.CentroidHoleLine => "分割+孔槽",
                    SegmentRefineMethod.CaliperTab => "分割+卡尺",
                    _ => "分割+精修",
                },
                AngleMode.DualBlobCenterLine => "双BLOB",
                _ => r.AngleMode.ToString(),
            };
            var tags = new List<string> { mode, r.CameraId, r.Models.FirstOrDefault("") };
            tags.Add(r.SerialNumber > 0 ? $"#{r.SerialNumber}" : "无序号");
            if (!string.IsNullOrWhiteSpace(r.StationId))
                tags.Add($"工位:{r.StationId}");
            if (r.Roi is not null)
                tags.Add("ROI");
            if (r.Lighting is not null)
                tags.Add($"光:{r.LightControllerId}");
            if (!r.OutputOffset.IsZero)
                tags.Add("补偿");
            if (r.ModelSha256.Any(h => !string.IsNullOrWhiteSpace(h)) ||
                !string.IsNullOrWhiteSpace(r.StationSha256))
                tags.Add("钉扎");
            return new RecipeListItem(name, string.Join(" · ", tags), true, r.Enabled, r.Description);
        }
        catch (Exception ex)
        {
            return new RecipeListItem(name, ex.Message, false);
        }
    }
}
