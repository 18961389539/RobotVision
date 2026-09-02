using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
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
/// 配方管理：列表 + 编辑表单。ROI / 光源 / 试触发 / 模板 / 列表由协作对象承担，本类保留 DataContext。
/// </summary>
public partial class RecipeViewModel : ObservableObject, ICommitPendingEdits, IRecipeWorkspace, IRecipeListHost, IPageUnloadAware, IDisposable
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
    private readonly ISegmentRefineGuidance _refineGuidance;
    private readonly IReadOnlyList<EnumItem<AngleMode>> _angleModeOptions;
    private readonly DispatcherTimer _dirtyTimer;
    private readonly PageAsyncSession _pageSession = new();
    private readonly RecipeDirtyTracker _dirty = new();

    private RecipeConfig? _baseline;
    private string _originalName = "";
    private RecipePrior? _playbookPrior;
    private string? _testTriggerBlockReason;
    private string _assetPinStatus = AssetPinStatusText.Unpinned;

    /// <summary>最近一次从磁盘加载配方失败（Editor 为空壳）。为 true 时禁用保存/新建/复制/刷新（见 List.CanRunWhenIdle）。</summary>
    [ObservableProperty]
    private bool _editorLoadFailed;

    public Action? FlushPendingEdits { get; set; }

    int IRecipeWorkspace.RecipeTestTimeoutMs => _cfg.RecipeTestTimeoutMs;

    public string? TestTriggerBlockReason => _testTriggerBlockReason;

    public RecipeRoiEditor Roi { get; }
    public RecipeLightingEditor Lighting { get; }
    public RecipeTestSession Test { get; }
    public RecipeTemplatePresenter TemplateUi { get; }
    public RecipeListCatalog List { get; }

    public ObservableCollection<RecipeListItem> Recipes => List.Recipes;

    public RecipeListItem? Selected
    {
        get => List.Selected;
        set => List.Selected = value;
    }

    [ObservableProperty]
    private RecipeConfig _editor = new();

    [ObservableProperty]
    private bool _isNew;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private bool _isBusy;

    public bool IsIdle => !IsBusy;

    [ObservableProperty]
    private bool _isListPanelVisible = true;

    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    [ObservableProperty]
    private bool _showTestImage = true;

    public string SearchText
    {
        get => List.SearchText;
        set => List.SearchText = value;
    }

    string IRecipeWorkspace.OriginalName => _originalName;
    RecipePrior? IRecipeWorkspace.PlaybookPrior => _playbookPrior;

    RecipeConfig? IRecipeListHost.Baseline
    {
        get => _baseline;
        set => _baseline = value;
    }

    bool IRecipeListHost.EditorLoadFailed
    {
        get => EditorLoadFailed;
        set => EditorLoadFailed = value;
    }

    partial void OnEditorLoadFailedChanged(bool value) => List.NotifyIdleCommands();

    string IRecipeListHost.OriginalName
    {
        get => _originalName;
        set => _originalName = value;
    }

    void IRecipeListHost.CommitEdits() => this.Commit();

    void IRecipeListHost.ResetDirtyCache() => ResetDirtyCache();

    void IRecipeListHost.ClearTestAdvice() => Test.ClearAdvice();

    void IRecipeListHost.NotifyTestCanExecute() => Test.NotifyCanExecuteChanged();

    void IRecipeListHost.NotifyDeleteCanExecute() => List.DeleteCommand.NotifyCanExecuteChanged();

    void IRecipeListHost.RaiseListFilterBindings()
    {
        OnPropertyChanged(nameof(ModelFiles));
        OnPropertyChanged(nameof(StationIds));
        Lighting.RefreshControllerIds();
    }

    void IRecipeListHost.RaiseDirtyBindings()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
    }

    void IRecipeListHost.NotifyEditorBindings() => NotifyEditorBindings();

    void IRecipeListHost.RefreshTestTriggerGate() => RefreshTestTriggerGate();

    void IRecipeListHost.RefreshRecipeHealth() => RefreshRecipeHealth();

    public RecipeViewModel(
        RecipeLoader loader,
        AppConfig cfg,
        ICameraRuntime cameras,
        IModelRuntime models,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting,
        IAngleStrategyCatalog angleRegistry,
        AssetIntegrityChecker assets,
        IDialogService dialogs,
        IRecipeWindowService recipeWindows,
        IRecipeTestService recipeTest,
        IMaskTemplateTeachService maskTeach,
        ISegmentRefineGuidance refineGuidance,
        IFrameOverlayPresenter overlay,
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
        _refineGuidance = refineGuidance;
        _log = log;
        _sqlite = sqlite;
        _angleModeOptions = angleRegistry.Options
            .Select(o => new EnumItem<AngleMode>(o.Mode, o.Label))
            .ToList();
        Roi = new RecipeRoiEditor(this, cameras, calibration, lighting, _pageSession);
        Lighting = new RecipeLightingEditor(this, lighting);
        Test = new RecipeTestSession(this, recipeTest, refineGuidance, overlay, dialogs);
        TemplateUi = new RecipeTemplatePresenter(this, maskTeach, () => HasUnsavedChanges);
        List = new RecipeListCatalog(this, loader, dialogs, sqlite);
        Roi.PropertyChanged += OnRoiOrTestChanged;
        Test.PropertyChanged += OnRoiOrTestChanged;
        TemplateUi.PropertyChanged += OnTemplateUiChanged;
        List.PropertyChanged += OnListChanged;
        _dirtyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _dirtyTimer.Tick += (_, _) => RefreshDirtyStateFromTimer();
        List.Refresh();
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
        if (_recipeWindows.ShowSetupWizard(new RecipeWorkspaceContext(this, Roi, Test)))
            Test.ClearAdvice();
    }

    private bool CanOpenRefineDetails => TemplateUi.IsMaskTemplateMode && !IsBusy;

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
        TemplateUi.Refresh();
        RefreshAssetPinStatus();
        NotifyEditorCommands();

        if (refreshHealth)
            RefreshRecipeHealth();
    }

    private void RaiseEditorUiProperties()
    {
        foreach (var name in RecipeEditorUiRefresh.PropertyNames)
            OnPropertyChanged(name);
        TemplateUi.NotifyEditorBindings();
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
        if (!_dirty.TryPublish(Editor, _baseline, OnDirtyChanged))
            return;
    }

    private void OnDirtyChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(TemplateStatusText));
        OnPropertyChanged(nameof(RefineDetailsSummary));
        RefreshTestTriggerGate();
    }

    private void ResetDirtyCache()
    {
        _dirty.ResetFromBaseline(_baseline, Editor);
        _hasUnsavedChangesSync();
    }

    private void RefreshDirtyStateFromTimer() => PublishDirtyState();

    private void _hasUnsavedChangesSync()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
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

    public string RotationCenterHint => RecipeEditorHints.RotationCenter(Editor, _calibration);

    public string UndirectedEccentricHint => RecipeEditorHints.UndirectedEccentric(Editor);

    public string MappingHint => RecipeEditorHints.Mapping(Editor, _calibration);

    public string AngleModeHint => RecipeEditorHints.AngleModeHint(Editor.AngleMode);

    public string RefineMethodHint => RecipeEditorHints.RefineMethod(Editor);

    public IEnumerable<RecipeListItem> VisibleRecipes => List.VisibleRecipes;

    public bool IsSelectedVisibleInFilter => List.IsSelectedVisibleInFilter;

    public string SelectedFilterHint => List.SelectedFilterHint;

    public bool HasUnsavedChanges => _dirty.HasUnsavedChanges;

    public string UnsavedHint => HasUnsavedChanges
        ? "有未保存的修改：测试触发已用当前编辑器，保存后才上产线；切换/刷新将丢弃"
        : "";

    public string RecipesFolderHint => List.RecipesFolderHint;

    public ImageSource? TemplatePreview => TemplateUi.TemplatePreview;

    public string TemplateStatusText => TemplateUi.TemplateStatusText;

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

    internal ISegmentRefineGuidance RefineGuidance => _refineGuidance;

    public IReadOnlyList<EnumItem<AngleMode>> AngleModeOptions => _angleModeOptions;

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
    public bool IsMaskTemplateMode => TemplateUi.IsMaskTemplateMode;
    public bool IsDualBlobMode => Editor.AngleMode == AngleMode.DualBlobCenterLine;
    public bool ShowBlobFixedThreshold => IsDualBlobMode && !Editor.Blob.UseOtsu;
    public bool HasTemplate => TemplateUi.HasTemplate;

    public IReadOnlyList<EnumItem<SegmentRefineMethod>> RefineMethodOptions { get; } =
    [
        new(SegmentRefineMethod.Template, "模板匹配（需示教，可判头尾）"),
        new(SegmentRefineMethod.Sift, "SIFT特征匹配（需示教，可判头尾）"),
        new(SegmentRefineMethod.ShapeMatch, "形状匹配（需示教，分割框内几何，可判头尾）"),
        new(SegmentRefineMethod.LineFit, "直线拟合（弱纹理矩形，免示教）"),
        new(SegmentRefineMethod.CentroidHoleLine, "质心-内标连线（掩码有孔/槽，有方向）"),
        new(SegmentRefineMethod.CaliperTab, "卡尺长边+凸起极性（免示教，有方向）"),
    ];

    public bool IsTemplateMethod => TemplateUi.IsTemplateMethod;
    public bool UsesFeatureTeachRoi => TemplateUi.UsesFeatureTeachRoi;
    public bool UsesRefineLine =>
        Editor.Template is { } tmpl && TemplateOptions.UsesTaughtRefineLine(tmpl.RefineMethod);
    public bool NeedsTaughtTemplate => TemplateUi.NeedsTaughtTemplate;
    public bool ShowRefineRange => TemplateUi.ShowRefineRange;
    public string RefineDetailsSummary => TemplateUi.RefineDetailsSummary;

    public Action? RequestTemplateRoiDraw { get; set; }

    public string TeachGeometryHint => TemplateUi.TeachGeometryHint;

    public string OutputOffsetTeachHint =>
        Editor.OutputOffset.HasTeachOutput
            ? $"已记示教输出 X={Editor.OutputOffset.TeachX:0.###} Y={Editor.OutputOffset.TeachY:0.###} Rz={Editor.OutputOffset.TeachRzDeg:0.##}°"
            : "尚未记下示教输出。请先成功试触发，再点「记下本次为示教输出」。";

    [ObservableProperty]
    private string _recipeHealthHint = "";

    public string TeachPeakHint => TemplateUi.TeachPeakHint;
    public string TeachDiagnosticsHint => TemplateUi.TeachDiagnosticsHint;
    public string PolarityLockHint => TemplateUi.PolarityLockHint;
    public string FeatureGrabOriginHint => TemplateUi.FeatureGrabOriginHint;

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

    public IRelayCommand RefreshCommand => List.RefreshCommand;
    public IRelayCommand NewCommand => List.NewCommand;
    public IRelayCommand CopyCommand => List.CopyCommand;
    public IRelayCommand SaveCommand => List.SaveCommand;
    public IRelayCommand DeleteCommand => List.DeleteCommand;
    public IRelayCommand OpenFolderCommand => List.OpenFolderCommand;

    public void Refresh() => List.Refresh();
    public void Refresh(string? preferName, bool reloadEditor, bool ignoreUnsaved = false) =>
        List.Refresh(preferName, reloadEditor, ignoreUnsaved);

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

    private bool CanRecordTeachOutput =>
        !IsBusy && Test.LastPreview is { Ok: true, Poses.Count: > 0 };

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

    private bool CanRunWhenIdle => !IsBusy;

    public void OnPageUnloading() => _pageSession.Deactivate();

    public void Dispose()
    {
        if (_pageSession is IDisposable d)
            d.Dispose();
        _dirtyTimer.Stop();
        Roi.PropertyChanged -= OnRoiOrTestChanged;
        Test.PropertyChanged -= OnRoiOrTestChanged;
        TemplateUi.PropertyChanged -= OnTemplateUiChanged;
        StopDirtyWatch();
        Test.Dispose();
        Roi.PreviewImage = null;
    }

    private void OnListChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeListCatalog.Selected))
        {
            OnPropertyChanged(nameof(Selected));
            List.HandleSelectedChanged(List.Selected);
        }
        else if (e.PropertyName == nameof(RecipeListCatalog.SearchText))
        {
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(VisibleRecipes));
            OnPropertyChanged(nameof(IsSelectedVisibleInFilter));
            OnPropertyChanged(nameof(SelectedFilterHint));
        }
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
        List.RefreshCommand.NotifyCanExecuteChanged();
        List.NewCommand.NotifyCanExecuteChanged();
        List.CopyCommand.NotifyCanExecuteChanged();
        List.SaveCommand.NotifyCanExecuteChanged();
        List.DeleteCommand.NotifyCanExecuteChanged();
        PinAssetsCommand.NotifyCanExecuteChanged();
        ClearAssetPinsCommand.NotifyCanExecuteChanged();
        RecordTeachOutputCommand.NotifyCanExecuteChanged();
        SuggestOutputOffsetCommand.NotifyCanExecuteChanged();
        Roi.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowTestImageViewer));
        OnPropertyChanged(nameof(ShowRoiImageViewer));
    }

    partial void OnIsNewChanged(bool value) => List.DeleteCommand.NotifyCanExecuteChanged();

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

    private void OnTemplateUiChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RecipeTemplatePresenter.TemplatePreview):
                OnPropertyChanged(nameof(TemplatePreview));
                break;
            case nameof(RecipeTemplatePresenter.TeachDiagnosticsHint):
                OnPropertyChanged(nameof(TeachDiagnosticsHint));
                break;
            case nameof(RecipeTemplatePresenter.TemplateStatusText):
            case nameof(RecipeTemplatePresenter.RefineDetailsSummary):
            case nameof(RecipeTemplatePresenter.TeachPeakHint):
            case nameof(RecipeTemplatePresenter.PolarityLockHint):
            case nameof(RecipeTemplatePresenter.FeatureGrabOriginHint):
            case nameof(RecipeTemplatePresenter.TeachGeometryHint):
                OnPropertyChanged(e.PropertyName!);
                break;
        }
    }

    bool IRecipeListHost.ConfirmGrabOriginIfNeeded(string action) => ConfirmGrabOriginIfNeeded(action);

    bool IRecipeListHost.ConfirmFlatFeatureRoiIfNeeded(string action) => ConfirmFlatFeatureRoiIfNeeded(action);

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
        catch (Exception ex)
        {
            // 结果库读失败不影响编辑，但需留痕以便排查
            WpfUiLog.RecipeHealthHintFailed(_log, ex, name);
        }
    }
}
