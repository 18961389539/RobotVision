using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;
/// <summary>
/// 配方向导：任务约束 → 取图分类 → 按场景推荐角度模式/精修。写入编辑器，不自动保存、不改 TRIGGER。
/// </summary>
internal sealed partial class RecipeSetupWizardViewModel : ObservableObject, IDisposable
{
    private readonly IRecipeWorkspace _host;
    private readonly ICameraRuntime _cameras;
    private readonly IModelRuntime _models;
    private readonly ICalibrationRuntime _calibration;
    private readonly ILightingRuntime _lighting;
    private readonly IRecipeSetupAnalysisService _analysis;
    private readonly RecipeRoiEditor _roi;
    private readonly RecipeTestSession _test;

    private SceneDescriptor? _scene;
    private IReadOnlyList<SegmentRefineCandidate> _bakeoff = [];
    private IReadOnlyDictionary<SceneKind, int>? _sceneVotes;
    private IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> _perFrame = [];
    private IReadOnlyList<int> _instanceCounts = [];
    private RefineParamSuggestion? _paramTune;
    private HousingEdgePolarity _edgePolarity;
    private TabPolarityLock _tabPolarity;
    private Roi? _featureRoi;
    private IReadOnlyList<FeatureRoiCandidate> _featureRanks = [];
    private BgraImageBuffer? _previewBuffer;
    private ImageSource? _previewBitmap;
    private bool _syncingFeature;
    private PlaybookAdvice? _playbook;
    private PlaybookCandidate? _chosen;
    private SegmentRefineAdvice? _refineAdvice;
    private bool _userPicked;
    private bool _syncingSelection;
    private bool _subscriptionsDetached;
    private readonly PageAsyncSession _pageSession = new();

    public RecipeSetupWizardViewModel(
        IRecipeWorkspace host,
        ICameraRuntime cameras,
        IModelRuntime models,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting,
        IRecipeSetupAnalysisService analysis,
        RecipeRoiEditor roi,
        RecipeTestSession test)
    {
        _host = host;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _lighting = lighting;
        _analysis = analysis;
        _roi = roi;
        _test = test;
        _test.PropertyChanged += OnTestPropertyChanged;
        if (host is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnHostPropertyChanged;
        var prefill = ScenePlaybook.FromRecipe(host.Editor);
        _needDirectedAngle = prefill.NeedDirectedAngle;
        _teachAllowed = prefill.TeachAllowed;
        _hasTwoLandmarks = prefill.HasTwoLandmarks;
        _useBlobsWithoutModel = prefill.UseBlobsWithoutModel;
        _expectedCount = prefill.ExpectedCount;
        RefreshPlaybook();
    }

    /// <summary>从单例 host/test 退订；窗口关闭或 <see cref="Dispose"/> 时调用，可重复调用。</summary>
    internal void DetachForClose()
    {
        if (_subscriptionsDetached)
            return;
        _subscriptionsDetached = true;
        _test.PropertyChanged -= OnTestPropertyChanged;
        if (_host is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnHostPropertyChanged;
    }

    public void Dispose()
    {
        _pageSession.Dispose();
        DetachForClose();
        ReleasePreview();
        GC.SuppressFinalize(this);
    }

    private void OnTestPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeTestSession.ResultImage))
            OnPropertyChanged(nameof(ViewerImage));
    }

    public IReadOnlyList<WizardNavItem> NavItems =>
    [
        new(SetupWizardStep.Welcome, 1, "欢迎", Step == SetupWizardStep.Welcome),
        new(SetupWizardStep.Task, 2, "任务", Step == SetupWizardStep.Task),
        new(SetupWizardStep.CaptureRoi, 3, "取景与 ROI", Step == SetupWizardStep.CaptureRoi),
        new(SetupWizardStep.Analyze, 4, "分析", Step == SetupWizardStep.Analyze),
        new(SetupWizardStep.Result, 5, "建议", Step == SetupWizardStep.Result),
        new(SetupWizardStep.TeachVerify, 6, "示教与验证", Step == SetupWizardStep.TeachVerify),
    ];

    public event Action? RequestClose;

    public bool Applied { get; private set; }

    public RecipeRoiEditor Roi => _roi;
    public RecipeTestSession Test => _test;
    internal IRecipeWorkspace Workspace => _host;

    public string? TestTriggerBlockReason => _host.TestTriggerBlockReason;
    public bool ShowTestTriggerBlockHint => _host.ShowTestTriggerBlockHint;
    public string TestTriggerBlockHint => _host.TestTriggerBlockHint;
    public string TestTriggerButtonToolTip => _host.TestTriggerButtonToolTip;

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRecipeWorkspace.TestTriggerBlockReason)
            or nameof(IRecipeWorkspace.ShowTestTriggerBlockHint)
            or nameof(IRecipeWorkspace.TestTriggerBlockHint)
            or nameof(IRecipeWorkspace.TestTriggerButtonToolTip)
            or nameof(IRecipeWorkspace.IsBusy))
        {
            OnPropertyChanged(nameof(TestTriggerBlockReason));
            OnPropertyChanged(nameof(ShowTestTriggerBlockHint));
            OnPropertyChanged(nameof(TestTriggerBlockHint));
            OnPropertyChanged(nameof(TestTriggerButtonToolTip));
        }
    }

    [ObservableProperty]
    private SetupWizardStep _step = SetupWizardStep.Welcome;

    [ObservableProperty]
    private bool _needDirectedAngle = true;

    [ObservableProperty]
    private bool _teachAllowed = true;

    [ObservableProperty]
    private bool _appearanceVaries;

    [ObservableProperty]
    private bool _hasTwoLandmarks;

    [ObservableProperty]
    private bool _useBlobsWithoutModel;

    [ObservableProperty]
    private int _expectedCount;

    [ObservableProperty]
    private bool _scoreAllPlayback = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _message = "按步骤填写任务，再分析当前画面或整夹回放。";

    [ObservableProperty]
    private ImageSource? _preview;

    [ObservableProperty]
    private string _sceneSummary = "尚未分析。";

    [ObservableProperty]
    private string _playbookSummary = "";

    [ObservableProperty]
    private string _confidenceNote = "";

    [ObservableProperty]
    private string _primaryTitle = "";

    [ObservableProperty]
    private string _primaryWhy = "";

    [ObservableProperty]
    private IReadOnlyList<WizardAltRow> _alternativeRows = [];

    [ObservableProperty]
    private IReadOnlyList<BakeOffRow> _bakeOffRows = [];

    [ObservableProperty]
    private int _selectedBakeOffIndex = -1;

    [ObservableProperty]
    private IReadOnlyList<ParamTuneRow> _paramTuneRows = [];

    [ObservableProperty]
    private int _selectedParamTuneIndex = -1;

    [ObservableProperty]
    private string _paramTuneHint = "";

    [ObservableProperty]
    private IReadOnlyList<FeatureRoiRow> _featureRoiRows = [];

    [ObservableProperty]
    private int _selectedFeatureIndex;

    [ObservableProperty]
    private Roi? _featureOverlayRoi;

    [ObservableProperty]
    private string _featureRoiHint = "";

    [ObservableProperty]
    private int _detected;

    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private string _chosenHint = "尚未选定。请分析画面，或勾选双特征 / 双 BLOB。";

    public bool ChosenIsPrimary =>
        _chosen is { } c && _playbook is { } p && ScenePlaybook.SameRecipe(c, p.Primary);

    public static string ApplyLabel => "完成并写入编辑器";

    public bool IsWelcome => Step == SetupWizardStep.Welcome;
    public bool IsTask => Step == SetupWizardStep.Task;
    public bool IsCaptureRoi => Step == SetupWizardStep.CaptureRoi;
    public bool IsAnalyze => Step == SetupWizardStep.Analyze;
    public bool IsResult => Step == SetupWizardStep.Result;
    public bool IsTeachVerify => Step == SetupWizardStep.TeachVerify;
    public bool ShowPreviewPane => IsCaptureRoi || IsAnalyze || IsResult || IsTeachVerify;
    /// <summary>取景 / 示教步骤使用可交互 ImageViewer；分析建议仍用静态预览图。</summary>
    public bool ShowInteractiveViewer => IsCaptureRoi || IsTeachVerify;
    public bool ShowStaticPreview => (IsAnalyze || IsResult) && !ShowInteractiveViewer;

    public ImageSource? ViewerImage =>
        IsTeachVerify && _test.ResultImage is not null
            ? _test.ResultImage
            : _roi.PreviewImage ?? Preview;

    public bool ShowTeachActions =>
        _host.Editor.AngleMode == AngleMode.MaskTemplate &&
        TemplateOptions.NeedsTaughtImage(_host.Editor.Template.RefineMethod);

    public bool ShowFeatureDraw =>
        _chosen?.Refine is { } r && TemplateOptions.UsesFeatureTeachRoi(r);

    public string CaptureRoiHint =>
        string.IsNullOrWhiteSpace(_host.Editor.CameraId)
            ? "请先在配方页「基本信息」选择相机，或关闭向导在编辑器中选好再打开。"
            : UseBlobsWithoutModel
                ? "双 BLOB 模式可不画 ROI（默认全图）。仍建议取一帧确认画面。"
                : "取一帧后拖拽绿色「检测」框；需要局部推理时勾选启用 ROI。";

    public string TeachVerifyHint =>
        ShowTeachActions
            ? "先「示教模板」，需要时可「框选特征」。最后「试触发」看叠加结果，满意后点「完成并写入编辑器」。"
            : "点「试触发」验证当前建议参数。满意后点「完成并写入编辑器」。保存配方仍须在主页面。";

    public bool HasFeatureOverlay => FeatureOverlayRoi is not null;
    public int PreviewPixelWidth => _previewBuffer?.Width ?? 0;
    public int PreviewPixelHeight => _previewBuffer?.Height ?? 0;

    public event Action? RequestBeginDetectionRoiDraw;
    public event Action? RequestBeginFeatureRoiDraw;

    public string StepCaption => Step switch
    {
        SetupWizardStep.Welcome => "1 / 6  欢迎",
        SetupWizardStep.Task => "2 / 6  任务",
        SetupWizardStep.CaptureRoi => "3 / 6  取景与 ROI",
        SetupWizardStep.Analyze => "4 / 6  分析",
        SetupWizardStep.Result => "5 / 6  建议",
        _ => "6 / 6  示教与验证",
    };
    public string NextLabel => Step switch
    {
        SetupWizardStep.Analyze when HasEnoughForResult => "查看建议",
        SetupWizardStep.Analyze => "按任务继续",
        SetupWizardStep.Result => "下一步",
        _ => "下一步",
    };

    public string AnalyzeHint
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_host.Editor.CameraId))
                return "配方未选相机：请关闭向导，在配方页选择相机后再打开。";
            if (UseBlobsWithoutModel)
                return "已勾选双 BLOB：可不跑分割。可取一张预览，或直接「按任务继续」。";
            if (_host.Editor.Models.Count == 0 || string.IsNullOrWhiteSpace(_host.Editor.Models[0]))
                return "未选分割模型：回配方选择模型，或到任务勾选双 BLOB / 双特征。";
            if (HasPlaybackFiles)
            {
                var n = _cameras.GetPlaybackFiles(_host.Editor.CameraId)?.Count ?? 0;
                return ScoreAllPlayback
                    ? $"文件夹相机，将分析目录内 {n} 张（磁盘解码，不推进产线回放下标）。"
                    : "将只分析下一张回放图。建议勾选整夹打分。";
            }

            return "将取一帧做分割、场景分类和精修赛马。失败时看下方说明，改 ROI / 模型后再点分析。";
        }
    }

    public bool CanGoBack => Step > SetupWizardStep.Welcome && !IsBusy;
    public bool CanGoNext => Step < SetupWizardStep.TeachVerify && !IsBusy &&
                              (Step != SetupWizardStep.CaptureRoi ||
                               !string.IsNullOrWhiteSpace(_host.Editor.CameraId)) &&
                              (Step != SetupWizardStep.Analyze ||
                               _scene is not null || UseBlobsWithoutModel || HasTwoLandmarks);
    public bool ShowNext => Step < SetupWizardStep.TeachVerify;
    public bool ShowApply => Step == SetupWizardStep.TeachVerify;
    public bool HasFeatureRoiRows => FeatureRoiRows.Count > 0;
    public bool ShowFeatureRoiPicker =>
        _chosen?.Refine is { } r && TemplateOptions.UsesFeatureTeachRoi(r);
    public bool HasParamTuneRows => ParamTuneRows.Count > 0;
    public bool HasEnoughForResult => _scene is not null;
    public bool HasPlaybackFiles =>
        !string.IsNullOrWhiteSpace(_host.Editor.CameraId)
        && _cameras.GetPlaybackFiles(_host.Editor.CameraId) is { Count: > 0 };
    public string CameraHint =>
        string.IsNullOrWhiteSpace(_host.Editor.CameraId)
            ? "当前配方未选相机。"
            : $"相机 {_host.Editor.CameraId}" + (HasPlaybackFiles ? "（文件夹回放）" : "");

    public TaskConstraints Constraints => new(
        NeedDirectedAngle, TeachAllowed, AppearanceVaries, HasTwoLandmarks, UseBlobsWithoutModel,
        Math.Clamp(ExpectedCount, 0, 20));

    private RecipePrior? CurrentPrior =>
        ScenePlaybook.Merge(ScenePlaybook.FromTemplate(_host.Editor.Template), _host.PlaybookPrior);

    partial void OnStepChanged(SetupWizardStep value)
    {
        OnPropertyChanged(nameof(IsWelcome));
        OnPropertyChanged(nameof(IsTask));
        OnPropertyChanged(nameof(IsCaptureRoi));
        OnPropertyChanged(nameof(IsAnalyze));
        OnPropertyChanged(nameof(IsResult));
        OnPropertyChanged(nameof(IsTeachVerify));
        OnPropertyChanged(nameof(ShowPreviewPane));
        OnPropertyChanged(nameof(ShowInteractiveViewer));
        OnPropertyChanged(nameof(ShowStaticPreview));
        OnPropertyChanged(nameof(ViewerImage));
        OnPropertyChanged(nameof(ShowTeachActions));
        OnPropertyChanged(nameof(ShowFeatureDraw));
        OnPropertyChanged(nameof(CaptureRoiHint));
        OnPropertyChanged(nameof(TeachVerifyHint));
        OnPropertyChanged(nameof(StepCaption));
        OnPropertyChanged(nameof(NavItems));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(AnalyzeHint));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowNext));
        OnPropertyChanged(nameof(ShowApply));
        OnPropertyChanged(nameof(ApplyLabel));
        if (value == SetupWizardStep.Result)
            RefreshPlaybook();
        if (value == SetupWizardStep.TeachVerify)
            ApplyRecommendationToEditor();
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowNext));
        OnPropertyChanged(nameof(ShowApply));
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        GrabFrameCommand.NotifyCanExecuteChanged();
        DrawDetectionRoiCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ApplyLabel));
    }

    partial void OnNeedDirectedAngleChanged(bool value)
    {
        _userPicked = false;
        OnPropertyChanged(nameof(AnalyzeHint));
        RefreshPlaybook();
    }
    partial void OnTeachAllowedChanged(bool value)
    {
        _userPicked = false;
        RefreshPlaybook();
    }
    partial void OnAppearanceVariesChanged(bool value)
    {
        _userPicked = false;
        RefreshPlaybook();
    }
    partial void OnHasTwoLandmarksChanged(bool value)
    {
        _userPicked = false;
        OnPropertyChanged(nameof(HasEnoughForResult));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(AnalyzeHint));
        RefreshPlaybook();
    }
    partial void OnUseBlobsWithoutModelChanged(bool value)
    {
        _userPicked = false;
        OnPropertyChanged(nameof(HasEnoughForResult));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(AnalyzeHint));
        RefreshPlaybook();
    }
    partial void OnScoreAllPlaybackChanged(bool value) => OnPropertyChanged(nameof(AnalyzeHint));
    partial void OnExpectedCountChanged(int value)
    {
        _userPicked = false;
        RefreshPlaybook();
    }

    partial void OnSelectedFeatureIndexChanged(int value)
    {
        if (value <= 0)
            _featureRoi = null;
        else if ((uint)(value - 1) < (uint)_featureRanks.Count)
            _featureRoi = _featureRanks[value - 1].Roi;
        else
            _featureRoi = null;

        if (!_syncingFeature)
            SyncFeatureOverlay();
    }

    partial void OnFeatureOverlayRoiChanged(Roi? value) =>
        OnPropertyChanged(nameof(HasFeatureOverlay));
}
