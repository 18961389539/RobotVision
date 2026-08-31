using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Teach;
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
    private readonly ICameraRuntime _cameras;
    private readonly IModelRuntime _models;
    private readonly ICalibrationRuntime _calibration;
    private readonly ILightingRuntime _lighting;
    private readonly IAngleStrategyCatalog _angleRegistry;
    private readonly AssetIntegrityChecker _assets;
    private readonly IDialogService _dialogs;
    private readonly IRecipeWindowService _recipeWindows;
    private readonly SqliteResultStore? _sqlite;
    private readonly ILogger<RecipeViewModel> _log;
    private readonly DispatcherTimer _dirtyTimer;

    private RecipeConfig? _baseline;
    private string _originalName = "";
    private RecipeListItem? _lastConfirmed;
    private bool _switching;
    private RecipePrior? _playbookPrior;
    private string _templatePreviewKey = "\0";
    private string _templateDiagnosticsKey = "\0";
    private string _teachDiagnosticsHint = "";
    private bool _hasUnsavedChanges;
    private string _baselineBodyFingerprint = "";
    private string _baselineTemplateImage = "";
    private string? _testTriggerBlockReason;
    private string _assetPinStatus = AssetPinStatusText.Unpinned;

    public Action? FlushPendingEdits { get; set; }

    int IRecipeWorkspace.RecipeTestTimeoutMs => _cfg.RecipeTestTimeoutMs;

    public string? TestTriggerBlockReason => _testTriggerBlockReason;

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

    /// <summary>与 <see cref="IsBusy"/> 相反，供 XAML 禁用列表/输入。</summary>
    public bool IsIdle => !IsBusy;

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
        ICameraRuntime cameras,
        IModelRuntime models,
        ICalibrationRuntime calibration,
        VisionService vision,
        ILightingRuntime lighting,
        IAngleStrategyCatalog angleRegistry,
        AssetIntegrityChecker assets,
        IDialogService dialogs,
        IRecipeWindowService recipeWindows,
        ILogger<RecipeViewModel> log,
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
        _dialogs = dialogs;
        _recipeWindows = recipeWindows;
        _log = log;
        _sqlite = sqlite;
        Roi = new RecipeRoiEditor(this, cameras, calibration, lighting);
        Lighting = new RecipeLightingEditor(this, lighting);
        Test = new RecipeTestSession(this, vision, cameras, models, calibration, lighting, dialogs);
        Roi.PropertyChanged += OnRoiOrTestChanged;
        Test.PropertyChanged += OnRoiOrTestChanged;
        _dirtyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _dirtyTimer.Tick += (_, _) => RefreshDirtyStateFromTimer();
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
        if (_recipeWindows.ShowSetupWizard(this, _cameras, _models, _calibration, _lighting, Roi, Test))
            Test.ClearAdvice();
    }

    private bool CanOpenRefineDetails => IsMaskTemplateMode && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOpenRefineDetails))]
    private void OpenRefineDetails()
    {
        this.Commit();
        if (_recipeWindows.ShowRefineDetails(this, out var requestDraw) && requestDraw)
            RequestTemplateRoiDraw?.Invoke();
    }

    public void StartDirtyWatch() => _dirtyTimer.Start();

    public void StopDirtyWatch() => _dirtyTimer.Stop();

    public void NotifyEditorMutated() => NotifyEditorUiCore(refreshHealth: false);

    private void NotifyEditorBindings(bool refreshHealth = true) => NotifyEditorUiCore(refreshHealth, syncChildEditors: true);

    private void NotifyEditorUiCore(bool refreshHealth, bool syncChildEditors = false)
    {
        if (syncChildEditors)
            OnPropertyChanged(nameof(Editor));

        PublishDirtyState();
        RefreshTestTriggerGate();

        if (syncChildEditors)
        {
            Lighting.NotifyFromEditor();
            Roi.NotifyFromEditor();
        }

        RaiseEditorUiProperties();
        RefreshViewerScale();
        RefreshTemplatePreview();
        RefreshAssetPinStatus();
        NotifyEditorCommands();

        if (refreshHealth)
            RefreshRecipeHealth();
    }

    private void RaiseEditorUiProperties()
    {
        foreach (var name in RecipeEditorUiRefresh.PropertyNames)
            OnPropertyChanged(name);
    }

    private void NotifyEditorCommands()
    {
        Test.RefreshAdviceCanApply();
        Test.NotifyCanExecuteChanged();
        RecordTeachOutputCommand.NotifyCanExecuteChanged();
        SuggestOutputOffsetCommand.NotifyCanExecuteChanged();
        OpenSetupWizardCommand.NotifyCanExecuteChanged();
        OpenRefineDetailsCommand.NotifyCanExecuteChanged();
    }

    void IRecipeWorkspace.RefreshEditorBindings() => NotifyEditorBindings(refreshHealth: false);

    void IRecipeWorkspace.CommitEdits() => this.Commit();

    void IRecipeWorkspace.NotifyDirty()
    {
        PublishDirtyState();
        OnPropertyChanged(nameof(TemplateStatusText));
        OnPropertyChanged(nameof(FeatureGrabOriginHint));
        OnPropertyChanged(nameof(RefineDetailsSummary));
        RefreshTestTriggerGate();
    }

    private void PublishDirtyState()
    {
        var dirty = EvaluateHasUnsavedChanges();
        if (_hasUnsavedChanges == dirty)
            return;
        _hasUnsavedChanges = dirty;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(TemplateStatusText));
        OnPropertyChanged(nameof(RefineDetailsSummary));
        RefreshTestTriggerGate();
    }

    private void ResetDirtyCache()
    {
        RefreshBaselineFingerprints();
        _hasUnsavedChanges = EvaluateHasUnsavedChanges();
    }

    private void RefreshBaselineFingerprints()
    {
        if (_baseline is null)
        {
            _baselineBodyFingerprint = "";
            _baselineTemplateImage = "";
            return;
        }

        _baselineTemplateImage = _baseline.Template.TemplateImageBase64 ?? "";
        _baselineBodyFingerprint = RecipeCompare.BodyFingerprint(_baseline);
    }

    private void RefreshDirtyStateFromTimer() => PublishDirtyState();

    private bool EvaluateHasUnsavedChanges()
    {
        if (_baseline is null)
            return false;
        if (!string.Equals(Editor.Template.TemplateImageBase64, _baselineTemplateImage, StringComparison.Ordinal))
            return true;
        return RecipeCompare.BodyFingerprint(Editor) != _baselineBodyFingerprint;
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
        AngleMode.MaskTemplate => "分割给粗框，精修过门才输出有向角。失败默认 1019。方法推荐与赛马见配方向导；示教仅写极性/阈值。保存后才上产线。",
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
                    "SIFT 把示教模板配到当前分割框内的原图，相似变换给出 XY 和有向角。需先示教整颗目标（不要只裁局部特征框）。试触发/监控会叠青色内点、红色外点。弱纹理或外观变化大会配不上，失败默认 1019。切到此方法后抓取原点与卡尺中心不同，需重新对示教。",
                SegmentRefineMethod.ShapeMatch =>
                    "形状匹配把示教图的 Canny 轮廓配到当前分割目标的转正窗。可示教整颗，或与模板一样框选局部轮廓（齿/缺口）。试触发/监控会叠青色命中点、红色未命中点。切到此方法后抓取原点与卡尺中心不同，需重新对示教。",
                _ => "模板匹配：十字是 NCC 匹配峰（特征中心），不是壳体中心。结果图金框「匹配」随峰；橙框「特征」是示教裁剪窗。转正裁剪窗默认开启。匹配失败默认 1019。",
            };

    public IEnumerable<RecipeListItem> VisibleRecipes =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Recipes
            : Recipes.Where(r =>
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>当前 <see cref="Selected"/> 是否出现在 <see cref="VisibleRecipes"/> 中（过滤后列表无高亮时为 false）。</summary>
    public bool IsSelectedVisibleInFilter =>
        Selected is not null && IsRecipeVisibleInFilter(Selected);

    public string SelectedFilterHint =>
        Selected is not null && !string.IsNullOrWhiteSpace(SearchText) && !IsSelectedVisibleInFilter
            ? $"当前编辑「{Selected.Name}」不在过滤结果中，列表未高亮；清空搜索或匹配到该项后可删除"
            : "";

    private bool IsRecipeVisibleInFilter(RecipeListItem item)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;
        foreach (var visible in VisibleRecipes)
        {
            if (ReferenceEquals(visible, item))
                return true;
            if (string.Equals(visible.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool HasUnsavedChanges => _hasUnsavedChanges;

    public string UnsavedHint => _hasUnsavedChanges
        ? "有未保存的修改：测试触发已用当前编辑器，保存后才上产线；切换/刷新将丢弃"
        : "";

    public string RecipesFolderHint => _loader.Folder;

    public ImageSource? TemplatePreview { get; private set; }

    public string TemplateStatusText =>
        !HasTemplate
            ? "未示教模板：点击「示教模板」自动生成（画面需有目标）"
            : _hasUnsavedChanges
                ? $"编辑器已有模板 {TemplatePreviewSize}（未保存不上产线）"
                : $"已示教模板 {TemplatePreviewSize}";

    private string TemplatePreviewSize =>
        TemplatePreview is { } img ? $"{(int)img.Width}×{(int)img.Height}px" : "";

    public bool CanTestTrigger => _testTriggerBlockReason is null;

    public bool ShowTestTriggerBlockHint => !IsBusy && _testTriggerBlockReason is not null;

    public string TestTriggerBlockHint =>
        _testTriggerBlockReason is { } reason ? $"无法测试触发：{reason}" : "";

    public string TestTriggerButtonToolTip =>
        IsBusy
            ? "测试进行中…"
            : _testTriggerBlockReason is { } reason
                ? $"无法测试触发：{reason}"
                : "用当前编辑器（含未保存修改）跑一次完整链路，不写产量。保存后才上产线";

    private void RefreshTestTriggerGate()
    {
        var reason = RecipeEditorValidator.TryValidateForTrigger(Editor, _loader);
        if (_testTriggerBlockReason == reason)
            return;
        _testTriggerBlockReason = reason;
        OnPropertyChanged(nameof(TestTriggerBlockReason));
        OnPropertyChanged(nameof(CanTestTrigger));
        OnPropertyChanged(nameof(ShowTestTriggerBlockHint));
        OnPropertyChanged(nameof(TestTriggerBlockHint));
        OnPropertyChanged(nameof(TestTriggerButtonToolTip));
        Test.NotifyCanExecuteChanged();
    }

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

    public string RefineDetailsSummary
    {
        get
        {
            if (!IsMaskTemplateMode)
                return "";
            var t = Editor.Template;
            var parts = new List<string>();
            if (NeedsTaughtTemplate)
                parts.Add(HasTemplate ? $"已示教 {TemplatePreviewSize}" : "未示教");
            if (t.ExpectedCount > 0)
                parts.Add($"期望 {t.ExpectedCount} 件");
            switch (t.RefineMethod)
            {
                case SegmentRefineMethod.Template:
                    parts.Add($"阈值 {t.MatchThreshold:0.00}");
                    parts.Add($"±{t.RefineRangeDeg:0}°");
                    if (t.UseEdgeMatch)
                        parts.Add("边缘定角");
                    if (!t.UseUprightCrop)
                        parts.Add("不转正");
                    break;
                case SegmentRefineMethod.ShapeMatch:
                    parts.Add($"阈值 {t.MatchThreshold:0.00}");
                    parts.Add($"±{t.RefineRangeDeg:0}°");
                    break;
                case SegmentRefineMethod.Sift:
                    parts.Add($"阈值 {t.MatchThreshold:0.00}");
                    break;
            }
            AppendPolaritySummary(parts, t);
            if (t.AllowCoarseFallback)
                parts.Add("可回退粗角");
            if (t.Roi is not null && TemplateOptions.UsesFeatureTeachRoi(t.RefineMethod))
            {
                parts.Add(t.RefineMethod == SegmentRefineMethod.Template
                    ? "十字=特征中心"
                    : "特征框示教");
                if (TemplateOptions.IsFlatFeatureRoi(t.Roi))
                    parts.Add("扁框易跳齿");
            }
            if (parts.Count == 0)
                return "点击「详情…」配置精修参数";
            return string.Join(" · ", parts) + " · 详情…";
        }
    }

    private static void AppendPolaritySummary(List<string> parts, TemplateOptions t)
    {
        if (t.RefineMethod is not (SegmentRefineMethod.Template or SegmentRefineMethod.CaliperTab))
            return;
        var edge = t.HousingEdgePolarity switch
        {
            HousingEdgePolarity.BrightToDark => "亮场边",
            HousingEdgePolarity.DarkToBright => "暗场边",
            _ => "",
        };
        if (edge.Length > 0)
            parts.Add(edge);
        if (t.RefineMethod == SegmentRefineMethod.CaliperTab)
        {
            var tab = t.TabPolarity switch
            {
                TabPolarityLock.PlusShortAxis => "凸起+短轴",
                TabPolarityLock.MinusShortAxis => "凸起−短轴",
                _ => "",
            };
            if (tab.Length > 0)
                parts.Add(tab);
        }
    }

    /// <summary>详情窗关闭后由配方页接管，在结果图上框选特征 ROI。</summary>
    public Action? RequestTemplateRoiDraw { get; set; }

    public string TeachGeometryHint =>
        Editor.Template.TeachAreaPx > 1
            ? $"示教几何：面积 {Editor.Template.TeachAreaPx:0} px²，轴比 {Editor.Template.TeachAspect:0.00}（面积 {Editor.Template.AreaRatioLo:0.00}~{Editor.Template.AreaRatioHi:0.00} 倍、轴比 {Editor.Template.AspectRatioLo:0.00}~{Editor.Template.AspectRatioHi:0.00} 倍过门）"
            : "未记示教几何：配方向导或示教模板后写入面积/轴比窗口；期望件数 0 表示不检查件数。";

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

    public string TeachDiagnosticsHint => _teachDiagnosticsHint;

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

    public string FeatureGrabOriginHint
    {
        get
        {
            if (!IsMaskTemplateMode || !UsesFeatureTeachRoi || Editor.Template.Roi is null)
                return "";
            var flat = TemplateOptions.IsFlatFeatureRoi(Editor.Template.Roi)
                ? "当前框过扁，齿列件可能跳齿，不要用扁框当 XY。"
                : "";
            var core = Editor.Template.RefineMethod == SegmentRefineMethod.Template
                ? "模板匹配十字是特征中心（NCC 峰），不是壳体中心。"
                : "特征框决定示教裁哪块；形状匹配十字也是该块中心，不是壳体中心。";
            return string.IsNullOrEmpty(flat) ? core : core + " " + flat;
        }
    }

    public bool ShowDualCropExpand => IsDualMode && Editor.DualModel.CropWindowPairing;

    public AngleMode EditorAngleMode
    {
        get => Editor.AngleMode;
        set
        {
            if (Editor.AngleMode == value)
                return;
            Editor.AngleMode = value;
            Test.ClearAdvice();
            NotifyEditorMutated();
        }
    }

    public SegmentRefineMethod EditorRefineMethod
    {
        get => Editor.Template.RefineMethod;
        set
        {
            if (Editor.Template.RefineMethod == value)
                return;
            Editor.Template.RefineMethod = value;
            Test.ClearAdvice();
            NotifyEditorMutated();
        }
    }

    public bool EditorCropWindowPairing
    {
        get => Editor.DualModel.CropWindowPairing;
        set
        {
            if (Editor.DualModel.CropWindowPairing == value)
                return;
            Editor.DualModel.CropWindowPairing = value;
            NotifyEditorMutated();
        }
    }

    public bool EditorBlobUseOtsu
    {
        get => Editor.Blob.UseOtsu;
        set
        {
            if (Editor.Blob.UseOtsu == value)
                return;
            Editor.Blob.UseOtsu = value;
            NotifyEditorMutated();
        }
    }

    public string? EditorStationId
    {
        get => Editor.StationId;
        set
        {
            if (string.Equals(Editor.StationId, value, StringComparison.Ordinal))
                return;
            Editor.StationId = value;
            RefreshViewerScale();
            NotifyEditorMutated();
        }
    }

    public double ViewerPixelSize { get; private set; } = 1.0;

    public string ViewerPhysicalUnit { get; private set; } = "px";

    private void RefreshViewerScale()
    {
        var scale = _calibration.GetScale(Editor.StationId);
        ViewerPixelSize = scale?.ScaleX ?? 1.0;
        ViewerPhysicalUnit = scale is null ? "px" : "mm";
        OnPropertyChanged(nameof(ViewerPixelSize));
        OnPropertyChanged(nameof(ViewerPhysicalUnit));
    }

    public void NotifyAngleModeChanged() => NotifyEditorMutated();

    public string AssetPinStatus => _assetPinStatus;

    private void RefreshAssetPinStatus()
    {
        var next = AssetPinStatusText.Compute(_assets, Editor);
        if (string.Equals(_assetPinStatus, next, StringComparison.Ordinal))
            return;
        _assetPinStatus = next;
        OnPropertyChanged(nameof(AssetPinStatus));
    }

    private bool CanRunWhenIdle => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
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
        if (reloadEditor)
        {
            if (Selected is not null)
                LoadIntoEditor(Selected.Name);
            else if (Recipes.Count == 0)
                ResetEditorForEmptyList();
        }
        Message = $"共 {Recipes.Count} 个配方";
        OnPropertyChanged(nameof(VisibleRecipes));
        RefreshTestTriggerGate();
        RefreshAssetPinStatus();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private void New()
    {
        if (HasUnsavedChanges && !ConfirmDiscard("新建配方"))
            return;
        ClearListSelectionForDraft();
        IsNew = true;
        _originalName = "";
        Editor = new RecipeConfig
        {
            Name = "",
            CameraId = CameraIds.Count > 0 ? CameraIds[0] : "",
            Models = [""],
        };
        _baseline = Editor.Clone();
        ResetDirtyCache();
        NotifyEditorBindings();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        RefreshTestTriggerGate();
        Test.ClearAdvice();
        Test.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RecipeHealthHint = "";
        Message = "新建配方：填写名称与参数后保存";
    }

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private void Copy()
    {
        if (HasUnsavedChanges && !ConfirmCopyCurrentEditor())
            return;
        var source = Editor;
        var copy = source.Clone();
        copy.Name = source.Name.Length > 0 ? source.Name + "_copy" : "";
        copy.SerialNumber = 0;
        copy.OutputOffset = new();
        ClearListSelectionForDraft();
        IsNew = true;
        _originalName = "";
        Editor = copy;
        _baseline = copy.Clone();
        ResetDirtyCache();
        NotifyEditorBindings();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        RefreshTestTriggerGate();
        Test.ClearAdvice();
        Test.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RecipeHealthHint = "";
        Message = source.Name.Length > 0
            ? $"已复制 {source.Name}：已清序列号与输出补偿，改名后保存即新配方"
            : "已复制为新配方：已清序列号与输出补偿，填写名称后保存";
    }

    /// <summary>复制保留当前编辑器（含未保存），不丢弃、不回读磁盘。</summary>
    private bool ConfirmCopyCurrentEditor() =>
        _dialogs.ConfirmYesNo(
            "将把当前编辑器（含未保存修改）复制为新配方，原配方磁盘文件不变。新配方会清掉序列号和输出补偿。继续？",
            "复制为新配方",
            questionIcon: true);

    /// <summary>新建/复制进入草稿：列表不高亮任何已存配方，避免删除误伤源文件。</summary>
    private void ClearListSelectionForDraft()
    {
        _switching = true;
        Selected = null;
        _lastConfirmed = null;
        _switching = false;
    }

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private void Save()
    {
        try
        {
            this.Commit();

            var saveError = RecipeEditorValidator.TryValidateForSave(Editor, _loader);
            if (saveError is not null)
            {
                ShowSaveBlocked(saveError);
                return;
            }

            if (string.IsNullOrWhiteSpace(Editor.Name))
            {
                ShowSaveBlocked("请先填写配方名称");
                return;
            }
            if (!RecipeLoader.IsValidRecipeName(Editor.Name))
            {
                ShowSaveBlocked("名称只允许字母、数字、下划线、中划线（长度 ≤ 64）");
                return;
            }

            var previousName = ResolvePreviousDiskName();

            var isRename = IsNew ||
                !string.Equals(Editor.Name, previousName, StringComparison.OrdinalIgnoreCase);
            if (isRename && _loader.FileExists(Editor.Name) &&
                !_dialogs.ConfirmYesNo($"配方 {Editor.Name} 已存在，保存将覆盖现有内容。继续？",
                    "覆盖确认"))
                return;

            var modelError = RecipeModelSlots.TryCommitUiModels(Editor, PrimaryModel, SecondaryModel);
            if (modelError is not null)
            {
                ShowSaveBlocked(modelError);
                return;
            }

            if (Editor.Lighting is not null && string.IsNullOrWhiteSpace(Editor.LightControllerId))
            {
                ShowSaveBlocked("已启用光源但未选择光源控制器（appsettings LightControllers 未配置时先添加 None 类型）");
                return;
            }

            if (!ConfirmGrabOriginIfNeeded("保存"))
                return;
            if (!ConfirmFlatFeatureRoiIfNeeded("保存"))
                return;

            _loader.Save(Editor, IsNew ? null : previousName);

            var savedMessage = isRename && !string.IsNullOrEmpty(previousName) &&
                !string.Equals(Editor.Name, previousName, StringComparison.OrdinalIgnoreCase)
                ? $"已保存 {Editor.Name}（原 {previousName} 已重命名）"
                : $"已保存 {Editor.Name}";

            IsNew = false;
            _originalName = Editor.Name;
            _baseline = Editor.Clone();
            ResetDirtyCache();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(UnsavedHint));
            Refresh(Editor.Name, reloadEditor: false, ignoreUnsaved: true);
            DeleteCommand.NotifyCanExecuteChanged();
            RefreshRecipeHealth();
            Message = savedMessage;
        }
        catch (Exception ex)
        {
            ShowSaveBlocked(ex.Message);
        }
    }

    private void ShowSaveBlocked(string reason)
    {
        Message = $"保存失败：{reason}";
        _dialogs.ShowWarning(reason, "无法保存");
    }

    private bool CanDelete =>
        Selected is not null && !IsNew && !IsBusy && IsSelectedVisibleInFilter;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (Selected is null || IsNew)
            return;

        var prompt = HasUnsavedChanges && string.Equals(Selected.Name, _originalName, StringComparison.OrdinalIgnoreCase)
            ? $"配方 {Selected.Name} 有未保存的修改，删除将一并丢弃。确定删除？（不可恢复）"
            : $"确定删除配方 {Selected.Name}？（不可恢复）";

        if (!_dialogs.ConfirmYesNo(prompt, "删除配方"))
            return;

        var deletedName = Selected.Name;
        try
        {
            _loader.Delete(deletedName);
            Message = $"已删除 {deletedName}";
            _lastConfirmed = null;
            Refresh(preferName: string.Empty, reloadEditor: true, ignoreUnsaved: true);
            DeleteCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Message = $"删除失败：{ex.Message}";
        }
    }

    private void ResetEditorForEmptyList()
    {
        ClearListSelectionForDraft();
        IsNew = true;
        _originalName = "";
        Editor = new RecipeConfig
        {
            Name = "",
            CameraId = CameraIds.Count > 0 ? CameraIds[0] : "",
            Models = [""],
        };
        _baseline = Editor.Clone();
        ResetDirtyCache();
        NotifyEditorBindings();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        RefreshTestTriggerGate();
        Test.ClearAdvice();
        Test.NotifyCanExecuteChanged();
        RecipeHealthHint = "";
    }

    [RelayCommand]
    private void OpenFolder() => Explorer.OpenFolder(_loader.Folder);

    private bool CanRecordTeachOutput =>
        !IsBusy && Test.LastPreview is { Ok: true, Poses.Count: > 0 };

    [RelayCommand(CanExecute = nameof(CanRecordTeachOutput))]
    private void RecordTeachOutput()
    {
        if (Test.LastPreview is not { Ok: true, Poses.Count: > 0 } preview)
            return;
        if (!ConfirmGrabOriginIfNeeded("记下示教输出"))
            return;
        if (!ConfirmFlatFeatureRoiIfNeeded("记下示教输出"))
            return;
        var p = preview.Poses[0];
        Editor.OutputOffset.TeachX = p.X;
        Editor.OutputOffset.TeachY = p.Y;
        Editor.OutputOffset.TeachRzDeg = p.AngleDeg;
        NotifyEditorMutated();
        Message = $"已记下示教输出 X={p.X:0.###} Y={p.Y:0.###} Rz={p.AngleDeg:0.##}°（未保存）";
    }

    private bool CanSuggestOutputOffset =>
        !IsBusy && _sqlite is not null && Editor.OutputOffset.HasTeachOutput;

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

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
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

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
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
        StopDirtyWatch();
        Test.Dispose();
        Roi.PreviewImage = null;
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(VisibleRecipes));
        OnPropertyChanged(nameof(IsSelectedVisibleInFilter));
        OnPropertyChanged(nameof(SelectedFilterHint));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnEditorChanged(RecipeConfig value)
    {
        NotifyEditorMutated();
        Roi.MaybeClearReferenceFrameForCamera(value.CameraId);
    }

    partial void OnShowTestImageChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTestImageViewer));
        OnPropertyChanged(nameof(ShowRoiImageViewer));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        Test.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowTestTriggerBlockHint));
        OnPropertyChanged(nameof(TestTriggerBlockHint));
        OnPropertyChanged(nameof(TestTriggerButtonToolTip));
        OpenSetupWizardCommand.NotifyCanExecuteChanged();
        OpenRefineDetailsCommand.NotifyCanExecuteChanged();
        NewCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        PinAssetsCommand.NotifyCanExecuteChanged();
        ClearAssetPinsCommand.NotifyCanExecuteChanged();
        RecordTeachOutputCommand.NotifyCanExecuteChanged();
        SuggestOutputOffsetCommand.NotifyCanExecuteChanged();
        Roi.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowTestImageViewer));
        OnPropertyChanged(nameof(ShowRoiImageViewer));
    }

    partial void OnSelectedChanged(RecipeListItem? value)
    {
        if (_switching || value is null)
            return;

        if (IsBusy)
        {
            _switching = true;
            Selected = _lastConfirmed;
            _switching = false;
            return;
        }

        this.Commit();

        if (_lastConfirmed is not null && HasUnsavedChanges &&
            !_dialogs.ConfirmDiscard($"配方 {_originalName} 有未保存的修改，切换将丢弃这些修改。继续？"))
        {
            _switching = true;
            Selected = _lastConfirmed;
            _switching = false;
            return;
        }

        LoadIntoEditor(value.Name);
        _lastConfirmed = value;
        Test.ClearAdvice();
        OnPropertyChanged(nameof(IsSelectedVisibleInFilter));
        OnPropertyChanged(nameof(SelectedFilterHint));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsNewChanged(bool value) => DeleteCommand.NotifyCanExecuteChanged();

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
        _dialogs.ConfirmDiscard($"配方 {_originalName} 有未保存的修改，{action}将丢弃这些修改。继续？");

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
            ResetDirtyCache();
            NotifyEditorBindings();
            Message = loaded.Enabled ? "" : $"配方 {name} 已停用（Enabled=false），触发将返回 1015";
        }
        catch (Exception ex)
        {
            Editor = new RecipeConfig { Name = name };
            _baseline = Editor.Clone();
            ResetDirtyCache();
            NotifyEditorBindings();
            Message = $"读取失败：{ex.Message}";
        }
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
        var diagKey = b64 + "|" + Editor.Template.RefineMethod;
        if (b64 == _templatePreviewKey)
        {
            OnPropertyChanged(nameof(TemplateStatusText));
            OnPropertyChanged(nameof(RefineDetailsSummary));
            if (diagKey == _templateDiagnosticsKey)
                return;
            RefreshTeachDiagnostics(b64);
            _templateDiagnosticsKey = diagKey;
            return;
        }

        _templatePreviewKey = b64;
        _templateDiagnosticsKey = diagKey;
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

        RefreshTeachDiagnostics(b64);
        OnPropertyChanged(nameof(TemplatePreview));
        OnPropertyChanged(nameof(TemplateStatusText));
        OnPropertyChanged(nameof(RefineDetailsSummary));
    }

    private void RefreshTeachDiagnostics(string b64)
    {
        if (b64.Length == 0)
        {
            _teachDiagnosticsHint = "";
            OnPropertyChanged(nameof(TeachDiagnosticsHint));
            return;
        }

        try
        {
            using var mat = MaskTemplateMatcher.DecodeTemplatePng(b64);
            _teachDiagnosticsHint = Editor.Template.RefineMethod switch
            {
                SegmentRefineMethod.ShapeMatch => MaskShapeMatch.BuildTeach(mat) is { } shape
                    ? $"形状示教边缘点 {shape.PointCount} 个"
                    : "形状示教边缘点不足（需 ≥24 个 Canny 采样点）",
                SegmentRefineMethod.Sift => BuildSiftDiagnostics(mat),
                _ => "",
            };
        }
        catch (Exception)
        {
            _teachDiagnosticsHint = "";
        }

        OnPropertyChanged(nameof(TeachDiagnosticsHint));
    }

    private static string BuildSiftDiagnostics(Mat mat)
    {
        var model = MaskSiftRefine.BuildTeach(mat);
        if (model is null)
            return "SIFT 示教特征不足（需 ≥16 个关键点）";
        using (model)
            return $"SIFT 示教关键点 {model.KeypointCount} 个";
    }

    bool IRecipeWorkspace.ConfirmGrabOriginIfNeeded(string action) => ConfirmGrabOriginIfNeeded(action);

    bool IRecipeWorkspace.ConfirmFlatFeatureRoiIfNeeded(string action) => ConfirmFlatFeatureRoiIfNeeded(action);

    private bool ConfirmGrabOriginIfNeeded(string action)
    {
        if (_baseline is null)
            return true;
        if (!RecipeCompare.GrabOriginChanged(_baseline, Editor))
            return true;
        var staleTeach = Editor.OutputOffset.HasTeachOutput;
        var detail = staleTeach
            ? "将清除已记示教输出，需重新对示教。"
            : "测试/产线十字会换位置。";
        if (!_dialogs.ConfirmYesNo(
                $"{action}：精修方法或特征框已变，抓取原点可能与上次不同。{detail}继续？",
                "抓取原点已变"))
            return false;
        if (staleTeach)
        {
            Editor.OutputOffset.ClearTeachOutput();
            NotifyEditorMutated();
        }
        return true;
    }

    private bool ConfirmFlatFeatureRoiIfNeeded(string action)
    {
        if (!TemplateOptions.UsesFeatureTeachRoi(Editor.Template.RefineMethod))
            return true;
        if (!TemplateOptions.IsFlatFeatureRoi(Editor.Template.Roi))
            return true;
        return _dialogs.ConfirmYesNo(
                $"{action}：特征框过扁（宽高比 ≥ {TemplateOptions.FlatFeatureRoiAspect:0}），"
                + "匹配十字是特征中心不是壳体中心，齿列件可能跳齿。继续？",
                "特征框过扁");
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
