using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
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
using RobotVision.WpfHost.Shared;

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
    private readonly LightingManager _lighting;
    private readonly AngleStrategyTypeRegistry _angleRegistry;
    private readonly AssetIntegrityChecker _assets;
    private readonly SqliteResultStore? _sqlite;
    private readonly DispatcherTimer _dirtyTimer;

    private RecipeConfig? _baseline;
    private string _originalName = "";
    private RecipeListItem? _lastConfirmed;
    private bool _switching;
    private RecipePrior? _playbookPrior;
    private string _templatePreviewKey = "\0";

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
    RecipePrior? IRecipeWorkspace.PlaybookPrior => _playbookPrior;

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
        AssetIntegrityChecker assets,
        SqliteResultStore? sqlite = null)
    {
        _loader = loader;
        _cfg = cfg;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _lighting = lighting;
        _angleRegistry = angleRegistry;
        _assets = assets;
        _sqlite = sqlite;
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

    private bool CanOpenSetupWizard => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOpenSetupWizard))]
    private void OpenSetupWizard()
    {
        this.Commit();
        var wizard = new RecipeSetupWizardViewModel(this, _cameras, _models, _calibration, _lighting);
        var window = new RecipeSetupWizardWindow
        {
            Owner = Application.Current?.MainWindow,
            DataContext = wizard,
        };
        window.ShowDialog();
        if (wizard.Applied && wizard.NeedsTeachAfterApply)
            Test.TeachTemplateCommand.Execute(null);
    }

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
        OnPropertyChanged(nameof(UsesFeatureTeachRoi));
        OnPropertyChanged(nameof(NeedsTaughtTemplate));
        OnPropertyChanged(nameof(ShowRefineRange));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(ShowBlobFixedThreshold));
        OnPropertyChanged(nameof(ShowDualCropExpand));
        OnPropertyChanged(nameof(RefineMethodHint));
        OnPropertyChanged(nameof(PrimaryModel));
        OnPropertyChanged(nameof(SecondaryModel));
        OnPropertyChanged(nameof(AssetPinStatus));
        OnPropertyChanged(nameof(CanTestTrigger));
        OnPropertyChanged(nameof(TeachPeakHint));
        OnPropertyChanged(nameof(PolarityLockHint));
        OnPropertyChanged(nameof(TeachGeometryHint));
        OnPropertyChanged(nameof(UndirectedEccentricHint));
        OnPropertyChanged(nameof(OutputOffsetTeachHint));
        RefreshTemplatePreview();
        Test.RefreshAdviceCanApply();
        Test.NotifyCanExecuteChanged();
        RecordTeachOutputCommand.NotifyCanExecuteChanged();
        SuggestOutputOffsetCommand.NotifyCanExecuteChanged();
    }

    void IRecipeWorkspace.CommitEdits() => this.Commit();

    void IRecipeWorkspace.NotifyDirty()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(TemplateStatusText));
    }

    void IRecipeWorkspace.RefreshEditorBindings()
    {
        OnPropertyChanged(nameof(Editor));
        NotifyEditorMutated();
    }

    void IRecipeWorkspace.OnTestStarting() => ShowTestImage = true;

    void IRecipeWorkspace.ApplySuggestedFeatureRoi(Roi roi)
    {
        Editor.Template.Roi = roi;
        Roi.NotifyFromEditor();
        NotifyEditorMutated();
    }

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

    public string RotationCenterHint
    {
        get
        {
            if (Editor.RotationCompensation != RotationCompensationMode.EccentricTool)
                return "";
            if (string.IsNullOrWhiteSpace(Editor.StationId) ||
                !_calibration.RotationCenterProfiles.Any(p =>
                    string.Equals(p.StationId, Editor.StationId, StringComparison.OrdinalIgnoreCase)))
                return $"工位 {Editor.StationId ?? "（空）"} 未做旋转轴心标定：偏心补偿保存/触发将被拒绝，请先在标定向导完成轴心标定";
            return "";
        }
    }

    public string UndirectedEccentricHint =>
        Editor.RotationCompensation == RotationCompensationMode.EccentricTool &&
        RecipeLoader.HasUndirectedAngle(Editor)
            ? "无向角（最小外接矩形或直线拟合）不能与偏心工具同时使用，保存将被拒绝。请改用分割+精修有向方法或关闭偏心补偿。"
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
        AngleMode.MaskMinAreaRect => "最小外接矩形角度为 [0,180)，无头尾。与偏心工具同时保存会被拒绝。",
        AngleMode.DualCenterLine => "默认全局就近配对，多目标间距接近时可能配错；开「窗口配对」后 B 只在 A 外扩窗口内检测，多目标不配错",
        AngleMode.MaskTemplate => "分割给粗框，精修过门才输出有向角。失败默认 1019。示教会写模板/极性到编辑器；测试走当前下拉（未点采用推荐则不变）。保存后才上产线。",
        AngleMode.DualBlobCenterLine => "主BLOB质心定位、主→次质心定向（有方向）；次BLOB缺失该目标不输出；无需模型",
        _ => "",
    };

    public string RefineMethodHint =>
        Editor.AngleMode != AngleMode.MaskTemplate
            ? ""
            : Editor.Template.RefineMethod switch
            {
                SegmentRefineMethod.LineFit =>
                    "直线拟合吃掩码长边（会先剔凸起），角度无方向 [0,180)。拟合失败默认 1019。与偏心工具同时保存会被拒绝。",
                SegmentRefineMethod.CentroidHoleLine =>
                    "质心连到掩码内最大孔/槽，有头尾。分割须能画出孔或槽。失败默认 1019。",
                SegmentRefineMethod.CaliperTab =>
                    "卡尺放在壳体长边上（短轴中心取两线中线）；黄线指向暗凸起一侧。配方测试会叠加探针。失败默认 1019。切到此方法后抓取原点与模板中心不同，需重新对示教。",
                SegmentRefineMethod.Sift =>
                    "SIFT 把示教模板配到当前分割框内的原图，相似变换给出 XY 和有向角。需先示教整颗目标（不要只裁局部特征框）。弱纹理或外观变化大会配不上，失败默认 1019。切到此方法后抓取原点与卡尺中心不同，需重新对示教。",
                SegmentRefineMethod.ShapeMatch =>
                    "形状匹配把示教图的 Canny 轮廓配到当前分割目标的转正窗。可示教整颗，或与模板一样框选局部轮廓（齿/缺口）。切到此方法后抓取原点与卡尺中心不同，需重新对示教。",
                _ => "模板匹配：十字是 NCC 匹配峰。结果图只画金框「匹配」（随峰）；橙框「特征」仅示教预览或框选时出现。转正裁剪窗默认开启。匹配失败默认 1019。",
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
        ? "有未保存的修改：测试触发已用当前编辑器，保存后才上产线；切换/刷新将丢弃"
        : "";

    public string RecipesFolderHint => _loader.Folder;

    public ImageSource? TemplatePreview { get; private set; }

    public string TemplateStatusText =>
        !HasTemplate
            ? "未示教模板：点击「示教模板」自动生成（画面需有目标）"
            : HasUnsavedChanges
                ? $"编辑器已有模板 {TemplatePreviewSize}（未保存不上产线）"
                : $"已示教模板 {TemplatePreviewSize}";

    private string TemplatePreviewSize =>
        TemplatePreview is { } img ? $"{(int)img.Width}×{(int)img.Height}px" : "";

    public bool CanTestTrigger =>
        !string.IsNullOrWhiteSpace(Editor.CameraId) &&
        (Editor.AngleMode == AngleMode.DualBlobCenterLine ||
         (Editor.Models.Count > 0 && !string.IsNullOrWhiteSpace(Editor.Models[0])));

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
        new(SegmentRefineMethod.Sift, "SIFT特征匹配（需示教，可判头尾）"),
        new(SegmentRefineMethod.ShapeMatch, "形状匹配（需示教，分割框内几何，可判头尾）"),
        new(SegmentRefineMethod.LineFit, "直线拟合（弱纹理矩形，免示教）"),
        new(SegmentRefineMethod.CentroidHoleLine, "质心-内标连线（掩码有孔/槽，有方向）"),
        new(SegmentRefineMethod.CaliperTab, "卡尺长边+凸起极性（免示教，有方向）"),
    ];

    public bool IsTemplateMethod => Editor.Template.RefineMethod == SegmentRefineMethod.Template;

    /// <summary>模板匹配与形状匹配可框选示教训练区域；SIFT 必须整颗。</summary>
    public bool UsesFeatureTeachRoi =>
        TemplateOptions.UsesFeatureTeachRoi(Editor.Template.RefineMethod);

    public bool NeedsTaughtTemplate =>
        TemplateOptions.NeedsTaughtImage(Editor.Template.RefineMethod);

    public bool ShowRefineRange =>
        Editor.Template.RefineMethod is SegmentRefineMethod.Template or SegmentRefineMethod.ShapeMatch;

    public string TeachGeometryHint =>
        Editor.Template.TeachAreaPx > 1
            ? $"示教几何：面积 {Editor.Template.TeachAreaPx:0} px²，轴比 {Editor.Template.TeachAspect:0.00}（面积 {Editor.Template.AreaRatioLo:0.00}~{Editor.Template.AreaRatioHi:0.00} 倍、轴比 {Editor.Template.AspectRatioLo:0.00}~{Editor.Template.AspectRatioHi:0.00} 倍过门）"
            : "未记示教几何：示教模板或采用推荐后写入面积/轴比窗口；期望件数 0 表示不检查件数。";

    public string OutputOffsetTeachHint =>
        Editor.OutputOffset.HasTeachOutput
            ? $"已记示教输出 X={Editor.OutputOffset.TeachX:0.###} Y={Editor.OutputOffset.TeachY:0.###} Rz={Editor.OutputOffset.TeachRzDeg:0.##}°"
            : "尚未记下示教输出。请先成功试触发，再点「记下本次为示教输出」。";

    [ObservableProperty]
    private string _recipeHealthHint = "";

    public string TeachPeakHint =>
        Editor.Template.TeachPeakScore >= 0.3
            ? $"示教峰 NCC {Editor.Template.TeachPeakScore:0.00} → 建议匹配阈值 {TemplateOptions.MatchThresholdFromTeachPeak(Editor.Template.TeachPeakScore):0.00}"
            : "";

    public string PolarityLockHint
    {
        get
        {
            var edge = Editor.Template.HousingEdgePolarity switch
            {
                HousingEdgePolarity.BrightToDark => "亮场",
                HousingEdgePolarity.DarkToBright => "暗场",
                _ => "",
            };
            var tab = Editor.Template.TabPolarity switch
            {
                TabPolarityLock.PlusShortAxis => "凸起在+短轴",
                TabPolarityLock.MinusShortAxis => "凸起在−短轴",
                _ => "",
            };
            if (edge.Length == 0 && tab.Length == 0)
                return "";
            var parts = new List<string>();
            if (edge.Length > 0)
                parts.Add(edge);
            if (tab.Length > 0)
                parts.Add($"{tab}（每帧实测，不按示教侧别拒识）");
            return "已锁定：" + string.Join("，", parts);
        }
    }

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
        Test.ClearAdvice();
        Test.NotifyCanExecuteChanged();
        RecipeHealthHint = "";
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
        Test.ClearAdvice();
        Test.NotifyCanExecuteChanged();
        RecipeHealthHint = "";
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

            if (!ConfirmGrabOriginIfNeeded("保存"))
                return;

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
            RefreshRecipeHealth();
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

    private bool CanRecordTeachOutput =>
        Test.LastPreview is { Ok: true, Poses.Count: > 0 };

    [RelayCommand(CanExecute = nameof(CanRecordTeachOutput))]
    private void RecordTeachOutput()
    {
        if (Test.LastPreview is not { Ok: true, Poses.Count: > 0 } preview)
            return;
        if (!ConfirmGrabOriginIfNeeded("记下示教输出"))
            return;
        var p = preview.Poses[0];
        Editor.OutputOffset.TeachX = p.X;
        Editor.OutputOffset.TeachY = p.Y;
        Editor.OutputOffset.TeachRzDeg = p.AngleDeg;
        NotifyEditorMutated();
        Message = $"已记下示教输出 X={p.X:0.###} Y={p.Y:0.###} Rz={p.AngleDeg:0.##}°（未保存）";
    }

    private bool CanSuggestOutputOffset =>
        _sqlite is not null && Editor.OutputOffset.HasTeachOutput;

    [RelayCommand(CanExecute = nameof(CanSuggestOutputOffset))]
    private void SuggestOutputOffset()
    {
        if (_sqlite is null || !Editor.OutputOffset.HasTeachOutput)
            return;
        var name = RecipeLoader.IsValidRecipeName(_originalName) ? _originalName : Editor.Name;
        if (!RecipeLoader.IsValidRecipeName(name))
        {
            Message = "请先保存配方，再按配方名取结果库合格均值";
            return;
        }

        try
        {
            var ok = _sqlite.QueryOkRobotPoses(new ResultDbQuery { Recipe = name, Limit = 2000 });
            var teach = new RobotPose(
                Editor.OutputOffset.TeachX!.Value,
                Editor.OutputOffset.TeachY!.Value,
                Editor.OutputOffset.TeachRzDeg!.Value);
            var delta = OutputOffsetOptions.SuggestDelta(teach, ok);
            if (delta is null)
            {
                Message = $"合格样本不足 8 条（当前 {ok.Count}），无法建议补偿。生产 TRIGGER 写入结果库后重试。";
                return;
            }

            var nextX = Editor.OutputOffset.X + delta.X;
            var nextY = Editor.OutputOffset.Y + delta.Y;
            if (Math.Abs(nextX) > 100 || Math.Abs(nextY) > 100 || Math.Abs(delta.RzDeg) > 180)
            {
                Message =
                    $"建议补偿超限（ΔX={delta.X:0.###} ΔY={delta.Y:0.###} ΔRz={delta.RzDeg:0.##}°），请检查示教或重标定，不要用大补偿掩盖标定错误。";
                return;
            }

            Editor.OutputOffset.ApplySuggestedDelta(delta, teach, ok);
            NotifyEditorMutated();
            Message =
                $"已叠合格中位差 ΔX={delta.X:0.###} ΔY={delta.Y:0.###} ΔRz={delta.RzDeg:0.##}°。同一批数据再点一次不会叠两次；新数据进来请先重新记下示教。未保存。";
        }
        catch (Exception ex)
        {
            Message = $"读取结果库失败：{ex.Message}";
        }
    }

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
        OpenSetupWizardCommand.NotifyCanExecuteChanged();
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
        Test.ClearAdvice();
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

        if (e.PropertyName == nameof(RecipeTestSession.LastPreview))
        {
            RecordTeachOutputCommand.NotifyCanExecuteChanged();
            SuggestOutputOffsetCommand.NotifyCanExecuteChanged();
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
            RefreshRecipeHealth();
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
        OnPropertyChanged(nameof(UsesFeatureTeachRoi));
        OnPropertyChanged(nameof(NeedsTaughtTemplate));
        OnPropertyChanged(nameof(ShowRefineRange));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(ShowBlobFixedThreshold));
        OnPropertyChanged(nameof(ShowDualCropExpand));
        OnPropertyChanged(nameof(AngleModeHint));
        OnPropertyChanged(nameof(RefineMethodHint));
        OnPropertyChanged(nameof(MappingHint));
        OnPropertyChanged(nameof(AssetPinStatus));
        OnPropertyChanged(nameof(TeachPeakHint));
        OnPropertyChanged(nameof(PolarityLockHint));
        OnPropertyChanged(nameof(TeachGeometryHint));
        OnPropertyChanged(nameof(UndirectedEccentricHint));
        OnPropertyChanged(nameof(OutputOffsetTeachHint));
        OnPropertyChanged(nameof(CanTestTrigger));
        RefreshTemplatePreview();
    }

    private void RefreshRecipeHealth()
    {
        RecipeHealthHint = "";
        _playbookPrior = ScenePlaybook.FromTemplate(Editor.Template);
        if (_sqlite is null)
            return;
        var name = RecipeLoader.IsValidRecipeName(_originalName) ? _originalName : Editor.Name;
        if (!RecipeLoader.IsValidRecipeName(name))
            return;
        try
        {
            var q = new ResultDbQuery { Recipe = name };
            var total = _sqlite.Count(q);
            if (total == 0)
                return;
            var hints = RecipeHealthAdvisor.Analyze(
                total,
                _sqlite.CountByCode(q),
                _sqlite.QueryAngles(q with { OkOnly = true }),
                _sqlite.QuerySpread(q with { OkOnly = true }),
                Editor.Template.TeachPeakScore);
            RecipeHealthHint = string.Join(Environment.NewLine, hints.Select(h => h.Message));
            var current = Editor.AngleMode == AngleMode.MaskTemplate ? Editor.Template.RefineMethod : (SegmentRefineMethod?)null;
            _playbookPrior = ScenePlaybook.Merge(
                ScenePlaybook.FromTemplate(Editor.Template),
                RecipeHealthAdvisor.ToPlaybookPrior(hints, current, Editor.Template.RefinePolicyOrder));
        }
        catch (Exception)
        {
            // 结果库读失败不影响编辑
        }
    }

    private void RefreshTemplatePreview()
    {
        var b64 = Editor.Template.TemplateImageBase64 ?? "";
        if (b64 == _templatePreviewKey)
        {
            OnPropertyChanged(nameof(TemplateStatusText));
            return;
        }

        _templatePreviewKey = b64;
        TemplatePreview = null;
        if (b64.Length > 0)
        {
            try
            {
                using var mat = MaskTemplateMatcher.DecodeTemplatePng(b64);
                var src = ImageConverter.ToBitmapSource(mat);
                src.Freeze();
                TemplatePreview = src;
            }
            catch (Exception)
            {
                TemplatePreview = null;
            }
        }

        OnPropertyChanged(nameof(TemplatePreview));
        OnPropertyChanged(nameof(TemplateStatusText));
    }

    private bool ConfirmGrabOriginIfNeeded(string action)
    {
        if (_baseline is null || IsNew)
            return true;
        if (!GrabOriginChanged(_baseline, Editor))
            return true;
        return MessageBox.Show(
            $"{action}：精修方法或特征框已变，抓取原点可能与上次示教输出不同，需要重新对示教。继续？",
            "抓取原点已变",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static bool GrabOriginChanged(RecipeConfig a, RecipeConfig b) =>
        a.AngleMode != b.AngleMode ||
        a.Template.RefineMethod != b.Template.RefineMethod ||
        a.Template.UseUprightCrop != b.Template.UseUprightCrop ||
        !SameRoi(a.Template.Roi, b.Template.Roi);

    private static bool SameRoi(Roi? a, Roi? b) =>
        a is null && b is null ||
        a is not null && b is not null &&
        Math.Abs(a.X - b.X) < 1e-4 &&
        Math.Abs(a.Y - b.Y) < 1e-4 &&
        Math.Abs(a.Width - b.Width) < 1e-4 &&
        Math.Abs(a.Height - b.Height) < 1e-4;

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
                    SegmentRefineMethod.Sift => "分割+SIFT",
                    SegmentRefineMethod.ShapeMatch => "分割+形状",
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
            if (r.Template.Roi is not null &&
                TemplateOptions.UsesFeatureTeachRoi(r.Template.RefineMethod))
                tags.Add("特征框");
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
