using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.WpfHost.Features.Recipe;

internal enum SetupWizardStep
{
    Welcome = 0,
    Task = 1,
    Analyze = 2,
    Result = 3,
}

internal sealed record BakeOffRow(
    SegmentRefineMethod MethodId, string Method, string Score, string Note, bool Ok, bool Eligible);

internal sealed record FeatureRoiRow(string Size, string Gap, bool Best);

internal sealed record ParamTuneRow(string Label, string Score, string Note, bool Best);

internal sealed record WizardAltRow(PlaybookCandidate Candidate, string Title, string Why, bool Selected);

internal sealed record WizardNavItem(SetupWizardStep Step, int Number, string Label, bool IsCurrent);

/// <summary>
/// 配方向导：任务约束 → 取图分类 → 按场景推荐角度模式/精修。写入编辑器，不自动保存、不改 TRIGGER。
/// </summary>
internal sealed partial class RecipeSetupWizardViewModel : ObservableObject
{
    private readonly IRecipeWorkspace _host;
    private readonly CameraManager _cameras;
    private readonly ModelManager _models;
    private readonly CalibrationManager _calibration;
    private readonly LightingManager _lighting;

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
    private Mat? _previewBase;
    private ImageSource? _previewBitmap;
    private bool _syncingFeature;
    private PlaybookAdvice? _playbook;
    private PlaybookCandidate? _chosen;
    private SegmentRefineAdvice? _refineAdvice;
    private bool _userPicked;
    private bool _syncingSelection;

    public RecipeSetupWizardViewModel(
        IRecipeWorkspace host,
        CameraManager cameras,
        ModelManager models,
        CalibrationManager calibration,
        LightingManager lighting)
    {
        _host = host;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _lighting = lighting;
        var prefill = ScenePlaybook.FromRecipe(host.Editor);
        _needDirectedAngle = prefill.NeedDirectedAngle;
        _teachAllowed = prefill.TeachAllowed;
        _hasTwoLandmarks = prefill.HasTwoLandmarks;
        _useBlobsWithoutModel = prefill.UseBlobsWithoutModel;
        _expectedCount = prefill.ExpectedCount;
        RefreshPlaybook();
    }

    public IReadOnlyList<WizardNavItem> NavItems =>
    [
        new(SetupWizardStep.Welcome, 1, "欢迎", Step == SetupWizardStep.Welcome),
        new(SetupWizardStep.Task, 2, "任务", Step == SetupWizardStep.Task),
        new(SetupWizardStep.Analyze, 3, "分析", Step == SetupWizardStep.Analyze),
        new(SetupWizardStep.Result, 4, "建议", Step == SetupWizardStep.Result),
    ];

    public event Action? RequestClose;

    public bool Applied { get; private set; }

    public bool NeedsTeachAfterApply { get; private set; }

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

    public string ApplyLabel =>
        ChosenNeedsTeach && string.IsNullOrEmpty(_host.Editor.Template.TemplateImageBase64)
            ? "采用并示教"
            : "采用并关闭";

    private bool ChosenNeedsTeach =>
        _chosen?.Refine is { } r && TemplateOptions.NeedsTaughtImage(r);

    public bool IsWelcome => Step == SetupWizardStep.Welcome;
    public bool IsTask => Step == SetupWizardStep.Task;
    public bool IsAnalyze => Step == SetupWizardStep.Analyze;
    public bool IsResult => Step == SetupWizardStep.Result;
    public bool ShowPreviewPane => IsAnalyze || IsResult;
    public bool HasFeatureOverlay => FeatureOverlayRoi is not null;
    public int PreviewPixelWidth => _previewBase?.Width ?? 0;
    public int PreviewPixelHeight => _previewBase?.Height ?? 0;
    public string StepCaption => Step switch
    {
        SetupWizardStep.Welcome => "1 / 4  欢迎",
        SetupWizardStep.Task => "2 / 4  任务",
        SetupWizardStep.Analyze => "3 / 4  分析",
        _ => "4 / 4  建议",
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
            if (IsFileCamera)
            {
                var n = TryPlaybackFiles()?.Count ?? 0;
                return ScoreAllPlayback
                    ? $"文件夹相机，将分析目录内 {n} 张（磁盘解码，不推进产线回放下标）。"
                    : "将只分析下一张回放图。建议勾选整夹打分。";
            }

            return "将取一帧做分割、场景分类和精修赛马。失败时看下方说明，改 ROI / 模型后再点分析。";
        }
    }

    public bool CanGoBack => Step > SetupWizardStep.Welcome && !IsBusy;
    public bool CanGoNext => Step < SetupWizardStep.Result && !IsBusy &&
                              (Step != SetupWizardStep.Analyze || HasEnoughForResult);
    public bool ShowNext => Step < SetupWizardStep.Result;
    public bool HasFeatureRoiRows => FeatureRoiRows.Count > 0;
    public bool ShowFeatureRoiPicker =>
        _chosen?.Refine is { } r && TemplateOptions.UsesFeatureTeachRoi(r);
    public bool HasParamTuneRows => ParamTuneRows.Count > 0;
    public bool HasEnoughForResult => _scene is not null || UseBlobsWithoutModel || HasTwoLandmarks;
    public bool IsFileCamera => TryPlaybackFiles() is { Count: > 0 };
    public string CameraHint =>
        string.IsNullOrWhiteSpace(_host.Editor.CameraId)
            ? "当前配方未选相机。"
            : $"相机 {_host.Editor.CameraId}" + (IsFileCamera ? "（文件夹回放）" : "");

    public TaskConstraints Constraints => new(
        NeedDirectedAngle, TeachAllowed, AppearanceVaries, HasTwoLandmarks, UseBlobsWithoutModel,
        Math.Clamp(ExpectedCount, 0, 20));

    private RecipePrior? CurrentPrior =>
        ScenePlaybook.Merge(ScenePlaybook.FromTemplate(_host.Editor.Template), _host.PlaybookPrior);

    partial void OnStepChanged(SetupWizardStep value)
    {
        OnPropertyChanged(nameof(IsWelcome));
        OnPropertyChanged(nameof(IsTask));
        OnPropertyChanged(nameof(IsAnalyze));
        OnPropertyChanged(nameof(IsResult));
        OnPropertyChanged(nameof(ShowPreviewPane));
        OnPropertyChanged(nameof(StepCaption));
        OnPropertyChanged(nameof(NavItems));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(AnalyzeHint));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowNext));
        OnPropertyChanged(nameof(ApplyLabel));
        if (value == SetupWizardStep.Result)
            RefreshPlaybook();
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowNext));
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
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

    internal void ReleasePreview()
    {
        _previewBase?.Dispose();
        _previewBase = null;
        _previewBitmap = null;
        Preview = null;
        FeatureOverlayRoi = null;
    }

    private void SyncFeatureOverlay() =>
        FeatureOverlayRoi = SelectedFeatureRoi();

    private Roi? SelectedFeatureRoi() =>
        SelectedFeatureIndex <= 0
            ? null
            : (uint)(SelectedFeatureIndex - 1) < (uint)_featureRanks.Count
                ? _featureRanks[SelectedFeatureIndex - 1].Roi
                : _featureRoi;

    private void RebuildFeatureRoiRows()
    {
        var rows = new List<FeatureRoiRow>(1 + _featureRanks.Count)
        {
            new("整颗目标", "不裁局部", SelectedFeatureIndex <= 0),
        };
        for (var i = 0; i < _featureRanks.Count; i++)
        {
            var c = _featureRanks[i];
            rows.Add(new($"{c.SizePx}×{c.SizePx}", $"{c.Gap:0.00}", SelectedFeatureIndex == i + 1));
        }

        FeatureRoiRows = rows;
        OnPropertyChanged(nameof(HasFeatureRoiRows));
        OnPropertyChanged(nameof(ShowFeatureRoiPicker));
        UpdateFeatureRoiHint();
    }

    private void UpdateFeatureRoiHint()
    {
        if (!ShowFeatureRoiPicker)
        {
            FeatureRoiHint = _chosen?.Refine == SegmentRefineMethod.Sift
                ? "SIFT 必须示教整颗目标，不能只裁局部特征框。"
                : "当前方法不需要模板训练区域。";
            return;
        }

        FeatureRoiHint = _featureRanks.Count == 0
            ? "点选「整颗目标」用分割转正全图示教。没有足够不对称的局部块时，也可采用后在配方页点「框选特征」手动画橙色框（丝印/齿脚）。"
            : "NCC / 形状匹配的示教裁剪：点表中一行。「整颗目标」= 分割转正全图；其余为相对壳体短边的局部窗口（预览金框）。也可采用后在配方页「框选特征」手动画。SIFT 请勿用局部框。";
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (Step > SetupWizardStep.Welcome)
            Step--;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (Step < SetupWizardStep.Result)
            Step++;
    }

    [RelayCommand]
    private void Cancel()
    {
        ReleasePreview();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void GoTo(SetupWizardStep step)
    {
        if (IsBusy)
            return;
        if (step == SetupWizardStep.Result && !HasEnoughForResult)
        {
            Message = "请先分析画面，或勾选双特征 / 双 BLOB。";
            return;
        }
        Step = step;
    }

    private bool CanAnalyze => !IsBusy && !string.IsNullOrWhiteSpace(_host.Editor.CameraId);

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        var cameraId = _host.Editor.CameraId;
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            Message = "请先在配方里选择相机。";
            return;
        }

        IsBusy = true;
        _host.IsBusy = true;
        try
        {
            _host.CommitEdits();
            using var lightingScope = _lighting.Apply(_host.Editor.LightControllerId, _host.Editor.Lighting);
            if (lightingScope.StabilizeDelayMs > 0)
                await Task.Delay(lightingScope.StabilizeDelayMs);

            var playback = ScoreAllPlayback ? TryPlaybackFiles() : null;
            if (playback is { Count: > 0 })
            {
                var result = await Task.Run(() => AnalyzePlayback(playback, cameraId));
                ApplyAnalysis(result);
            }
            else
            {
                var result = await Task.Run(() => AnalyzeGrab(cameraId));
                ApplyAnalysis(result);
            }

            _userPicked = false;
            RefreshPlaybook();
            Step = SetupWizardStep.Result;
        }
        catch (Exception ex)
        {
            Message = $"分析失败：{ex.Message}";
        }
        finally
        {
            _host.IsBusy = false;
            IsBusy = false;
        }
    }

    private bool CanApply => !IsBusy && HasEnoughForResult && _chosen is not null;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        var chosen = _chosen ?? ScenePlaybook.Recommend(Constraints, _scene, _bakeoff, CurrentPrior, _sceneVotes).Primary;
        var editor = _host.Editor;
        editor.AngleMode = chosen.AngleMode;
        if (chosen.AngleMode == AngleMode.MaskTemplate && chosen.Refine is { } refine)
        {
            editor.Template.RefineMethod = refine;
            editor.Template.UseEdgeMatch = chosen.EdgeMatch;
            editor.Template.ExpectedCount = Constraints.ExpectedCount;
            if (_edgePolarity != HousingEdgePolarity.Auto)
                editor.Template.HousingEdgePolarity = _edgePolarity;
            if (_tabPolarity != TabPolarityLock.Auto)
                editor.Template.TabPolarity = _tabPolarity;
            WriteLocks(refine);
            ApplyParamTune();
            if (TemplateOptions.UsesFeatureTeachRoi(refine))
            {
                if (SelectedFeatureRoi() is { } roi)
                    _host.ApplySuggestedFeatureRoi(roi);
                else
                    _host.Editor.Template.Roi = null;
            }
            else if (refine == SegmentRefineMethod.Sift)
                _host.Editor.Template.Roi = null;
        }

        NeedsTeachAfterApply = chosen.Refine is { } teach &&
                               TemplateOptions.NeedsTaughtImage(teach) &&
                               string.IsNullOrEmpty(editor.Template.TemplateImageBase64);

        _host.RefreshEditorBindings();
        Applied = true;
        var extra = chosen.AngleMode == AngleMode.DualCenterLine && editor.Models.Count < 2
            ? " 请再配置两个检测模型。"
            : NeedsTeachAfterApply
                ? " 将打开示教模板。"
                : "";
        _host.Message = $"配方向导已写入编辑器：{chosen.Title}（未保存，试触发已用编辑器）{extra}";
        RequestClose?.Invoke();
    }

    private void WriteLocks(SegmentRefineMethod refine)
    {
        if (_refineAdvice is not { } locks)
            return;
        if (refine == SegmentRefineMethod.Template && locks.TeachPeakScore >= 0.3)
        {
            _host.Editor.Template.TeachPeakScore = locks.TeachPeakScore;
            if (locks.SuggestedMatchThreshold > 0)
                _host.Editor.Template.MatchThreshold = locks.SuggestedMatchThreshold;
        }
        if (locks.TeachAreaPx > 1)
            _host.Editor.Template.TeachAreaPx = locks.TeachAreaPx;
        if (locks.Aspect > 1e-3)
            _host.Editor.Template.TeachAspect = locks.Aspect;
        if (locks.SuggestedConfidence > 0)
            _host.Editor.Confidence = locks.SuggestedConfidence;
        if (locks.SuggestedPixelConfidence > 0)
            _host.Editor.Segmentation.PixelConfidence = locks.SuggestedPixelConfidence;
    }

    private void ApplyParamTune()
    {
        if (_paramTune is not { } tune)
            return;
        var t = _host.Editor.Template;
        if (tune.MatchThreshold is { } th)
            t.MatchThreshold = th;
        if (tune.RefineRangeDeg is { } range)
            t.RefineRangeDeg = range;
        if (tune.UseEdgeMatch is { } edge)
            t.UseEdgeMatch = edge;
        if (tune.ExpectedCount is { } n)
            t.ExpectedCount = n;
        if (tune.EdgePolarity is { } ep)
            t.HousingEdgePolarity = ep;
        if (tune.TabPolarity is { } tp)
            t.TabPolarity = tp;
    }

    [RelayCommand]
    private void ChoosePrimary()
    {
        if (_playbook is null)
            return;
        _userPicked = true;
        SelectCandidate(_playbook.Primary);
    }

    [RelayCommand]
    private void ChooseAlternative(PlaybookCandidate? candidate)
    {
        if (candidate is null)
            return;
        _userPicked = true;
        SelectCandidate(candidate);
    }

    partial void OnSelectedBakeOffIndexChanged(int value)
    {
        if (_syncingSelection || (uint)value >= (uint)_bakeoff.Count)
            return;
        var row = _bakeoff[value];
        if (!ScenePlaybook.IsEligible(row.Method, Constraints, _scene))
        {
            Message = $"「{ScenePlaybook.RefineLabel(row.Method)}」无任务资格，未改采用项。";
            return;
        }

        _userPicked = true;
        var edge = row.Method == SegmentRefineMethod.Template &&
                   (_playbook?.Primary.EdgeMatch ?? false);
        SelectCandidate(new PlaybookCandidate(
            AngleMode.MaskTemplate, row.Method, edge,
            $"{ScenePlaybook.AngleModeLabel(AngleMode.MaskTemplate)} · {ScenePlaybook.RefineLabel(row.Method)}",
            $"已选赛马项：{row.Note}", true));
    }

    private void SelectCandidate(PlaybookCandidate candidate)
    {
        _chosen = candidate;
        ChosenHint = _playbook is { IsUncertain: true }
            ? $"把握不足，请先核备选。将采用：{candidate.Title}"
            : $"将采用：{candidate.Title}";
        AlternativeRows = (_playbook?.Alternatives ?? []).Select(a =>
            new WizardAltRow(a, a.Title, a.Why, ScenePlaybook.SameRecipe(a, candidate))).ToList();
        OnPropertyChanged(nameof(ChosenIsPrimary));
        OnPropertyChanged(nameof(ApplyLabel));
        ApplyCommand.NotifyCanExecuteChanged();
        SyncBakeOffSelection(candidate);
        RefreshParamTune();
        OnPropertyChanged(nameof(ShowFeatureRoiPicker));
        UpdateFeatureRoiHint();
    }

    private bool _syncingParamTune;

    private void RefreshParamTune()
    {
        _paramTune = null;
        ParamTuneRows = [];
        ParamTuneHint = "";
        if (_chosen?.Refine is not { } method || _perFrame.Count == 0)
        {
            OnPropertyChanged(nameof(HasParamTuneRows));
            return;
        }

        var peak = _refineAdvice is { TeachPeakScore: >= 0.3 } locks
            ? locks.TeachPeakScore
            : _host.Editor.Template.TeachPeakScore;
        _paramTune = RefineParamTuner.Tune(
            method,
            _perFrame,
            _instanceCounts,
            _host.Editor.Template,
            peak,
            _edgePolarity,
            _tabPolarity,
            Constraints.ExpectedCount,
            _scene?.Aspect ?? 0,
            _chosen.EdgeMatch);
        if (_paramTune is { } sug)
        {
            ParamTuneHint = sug.Summary;
            ParamTuneRows = sug.Trials.Select(t => new ParamTuneRow(t.Label, $"{t.Score:0.00}", t.Note, t.Best)).ToList();
        }
        else
            ParamTuneHint = "当前方法无可调门限，或还没有整夹分数（模板类请先示教再回放）。";

        _syncingParamTune = true;
        SelectedParamTuneIndex = ParamTuneRows.ToList().FindIndex(r => r.Best);
        _syncingParamTune = false;
        OnPropertyChanged(nameof(HasParamTuneRows));
    }

    partial void OnSelectedParamTuneIndexChanged(int value)
    {
        if (_syncingParamTune || _paramTune is not { } sug || (uint)value >= (uint)sug.Trials.Count)
            return;
        var trial = sug.Trials[value];
        if (trial.MatchThreshold <= 0)
            return;
        _paramTune = sug with
        {
            MatchThreshold = trial.MatchThreshold,
            Score = trial.Score,
            Trials = sug.Trials.Select((t, i) => t with { Best = i == value }).ToList(),
            Summary = $"已选匹配门 {trial.MatchThreshold:0.00}（{trial.Note}）。采用后请再试触发。",
        };
        ParamTuneHint = _paramTune.Summary;
        ParamTuneRows = _paramTune.Trials.Select(t => new ParamTuneRow(t.Label, $"{t.Score:0.00}", t.Note, t.Best)).ToList();
    }

    private void SyncBakeOffSelection(PlaybookCandidate candidate)
    {
        _syncingSelection = true;
        SelectedBakeOffIndex = candidate.Refine is { } m
            ? BakeOffRows.ToList().FindIndex(r => r.MethodId == m)
            : -1;
        _syncingSelection = false;
    }

    private void RefreshPlaybook()
    {
        var advice = ScenePlaybook.Recommend(Constraints, _scene, _bakeoff, CurrentPrior, _sceneVotes);
        _playbook = advice;
        PlaybookSummary = advice.Summary;
        ConfidenceNote = advice.ConfidenceNote;
        PrimaryTitle = advice.Primary.Title;
        PrimaryWhy = advice.Primary.Why;
        BakeOffRows = _bakeoff.Select(c => new BakeOffRow(
            c.Method,
            ScenePlaybook.RefineLabel(c.Method),
            $"{c.Score:0.00}",
            c.Note,
            c.Ok,
            ScenePlaybook.IsEligible(c.Method, Constraints, _scene))).ToList();

        var keep = _userPicked && _chosen is { } prev &&
                   (ScenePlaybook.SameRecipe(prev, advice.Primary) ||
                    advice.Alternatives.Any(a => ScenePlaybook.SameRecipe(a, prev)) ||
                    (prev.Refine is { } m && _bakeoff.Any(c => c.Method == m && ScenePlaybook.IsEligible(m, Constraints, _scene))));
        SelectCandidate(keep && _chosen is { } chosen ? chosen : advice.Primary);
        if (!keep)
            _userPicked = false;

        ApplyCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(HasEnoughForResult));
        OnPropertyChanged(nameof(IsFileCamera));
        OnPropertyChanged(nameof(CameraHint));
        OnPropertyChanged(nameof(AnalyzeHint));
        OnPropertyChanged(nameof(ApplyLabel));
    }

    private sealed record AnalysisResult(
        SceneDescriptor? Scene,
        IReadOnlyList<SegmentRefineCandidate> BakeOff,
        HousingEdgePolarity Edge,
        TabPolarityLock Tab,
        Roi? FeatureRoi,
        IReadOnlyList<FeatureRoiCandidate> FeatureRanks,
        ImageSource? Preview,
        int Detected,
        int Total,
        string Message,
        double Confidence = 0,
        SegmentRefineAdvice? Locks = null,
        int InstanceCount = 0,
        bool CountUnstable = false,
        IReadOnlyDictionary<SceneKind, int>? SceneVotes = null,
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>>? PerFrame = null,
        IReadOnlyList<int>? InstanceCounts = null,
        Mat? PreviewBase = null);

    private void ApplyAnalysis(AnalysisResult result)
    {
        _scene = result.Scene;
        _bakeoff = result.BakeOff;
        _sceneVotes = result.SceneVotes;
        _perFrame = result.PerFrame ?? [];
        _instanceCounts = result.InstanceCounts ?? [];
        _edgePolarity = result.Edge;
        _tabPolarity = result.Tab;
        _featureRanks = result.FeatureRanks;
        _refineAdvice = result.Locks;
        _previewBase?.Dispose();
        _previewBase = result.PreviewBase;
        _previewBitmap = _previewBase is null ? null : ImageConverter.ToBitmapSource(_previewBase);
        Preview = _previewBitmap;
        Detected = result.Detected;
        Total = result.Total;
        _syncingFeature = true;
        var pickLocal = result.FeatureRanks.Count > 0 && result.Scene is { Separability: < 0.10 };
        SelectedFeatureIndex = pickLocal ? 1 : 0;
        _featureRoi = pickLocal ? result.FeatureRanks[0].Roi : null;
        RebuildFeatureRoiRows();
        _syncingFeature = false;
        SyncFeatureOverlay();
        OnPropertyChanged(nameof(PreviewPixelWidth));
        OnPropertyChanged(nameof(PreviewPixelHeight));
        SceneSummary = result.Scene is { } s
            ? $"{ScenePlaybook.SceneLabel(s.Kind)} · {LightingLabel(s.Lighting)} · 轴比 {s.Aspect:0.0} · 圆度 {s.Circularity:0.00} · 熵 {s.TextureEntropy:0.0}（相对 {s.RelativeEntropy:+0.0;-0.0}） · 0/180 分差 {s.Separability:0.00} · {(s.HoleOk ? $"有孔/槽 {s.HoleQuality:0.00}" : "无孔")}"
              + (s.KindConfidence < 1 ? $" · 分类把握 {s.KindConfidence:0.00}" : "")
              + (result.Total > 1 ? $" · 帧 {result.Detected}/{result.Total}" : "")
              + (result.InstanceCount > 0 ? $" · 本帧 {result.InstanceCount} 件" : "")
              + (Constraints.ExpectedCount > 0 ? $"（期望 {Constraints.ExpectedCount}）" : "")
              + "。" + s.Why
              + (result.CountUnstable
                  ? " 件数与期望不符或不稳，场景按置信最高且件数匹配的帧（没有则退回冠军件），请核对漏检。"
                  : "")
            : "未检出分割目标，仅按任务约束推荐。";
        Message = result.Message;
        OnPropertyChanged(nameof(HasEnoughForResult));
        OnPropertyChanged(nameof(NextLabel));
    }

    private static string LightingLabel(LightingClass lighting) => lighting switch
    {
        LightingClass.DarkField => "暗场",
        LightingClass.BrightField => "亮场",
        _ => "打光未分",
    };

    private AnalysisResult AnalyzeGrab(string cameraId)
    {
        using var grabbed = _cameras.Grab(cameraId);
        var image = MaybeUndistort(cameraId, grabbed.Image, out var undistorted);
        using var undistortedScope = undistorted;
        return AnalyzeImage(image, totalHint: 1);
    }

    private AnalysisResult AnalyzePlayback(IReadOnlyList<string> files, string cameraId)
    {
        Mat? templateMat = null;
        if (!string.IsNullOrEmpty(_host.Editor.Template.TemplateImageBase64))
            templateMat = MaskTemplateMatcher.DecodeTemplatePng(_host.Editor.Template.TemplateImageBase64);
        using var templateScope = templateMat;
        using var teachCache = SegmentRefineBakeOff.TeachCache.TryCreate(templateMat);

        ModelSession? session = null;
        if (_host.Editor.Models.Count > 0 && !string.IsNullOrWhiteSpace(_host.Editor.Models[0]))
            session = _models.Open(_host.Editor.Models[0], InferenceTask.Segmentation);

        var perFrame = new List<IReadOnlyList<SegmentRefineCandidate>>(files.Count);
        SceneDescriptor? bestScene = null;
        HousingEdgePolarity edge = default;
        TabPolarityLock tab = default;
        Roi? feature = null;
        IReadOnlyList<FeatureRoiCandidate> featureRanks = [];
        Mat? previewBase = null;
        Mat? matchPreviewBase = null;
        SegmentRefineAdvice? locks = null;
        var detected = 0;
        var bestConf = double.NegativeInfinity;
        var bestMatchConf = double.NegativeInfinity;
        SceneDescriptor? matchScene = null;
        HousingEdgePolarity matchEdge = default;
        TabPolarityLock matchTab = default;
        Roi? matchFeature = null;
        IReadOnlyList<FeatureRoiCandidate> matchRanks = [];
        SegmentRefineAdvice? matchLocks = null;
        var instanceCounts = new List<int>();
        var expected = Constraints.ExpectedCount;
        var votes = new Dictionary<SceneKind, int>();

        for (var i = 0; i < files.Count; i++)
        {
            Report($"回放打分 {i + 1}/{files.Count} …");
            try
            {
                using var decoded = FileCamera.DecodeFile(files[i]);
                using var source = VisionImageCv.FromMat(decoded, ownsMat: false);
                var image = MaybeUndistort(cameraId, source, out var undistorted);
                using var undistortedScope = undistorted;
                var one = AnalyzeImage(image, files.Count, session, templateMat, teachCache, keepPreview: true);
                var keptPreview = false;
                if (one.Scene is null)
                {
                    perFrame.Add([]);
                    if (previewBase is null)
                    {
                        previewBase = one.PreviewBase;
                        keptPreview = true;
                    }
                    if (!keptPreview)
                        one.PreviewBase?.Dispose();
                    continue;
                }

                perFrame.Add(one.BakeOff);
                detected++;
                instanceCounts.Add(one.InstanceCount);
                if (one.Scene is { } framed)
                {
                    votes.TryGetValue(framed.Kind, out var n);
                    votes[framed.Kind] = n + 1;
                }
                if (one.Confidence >= bestConf)
                {
                    bestConf = one.Confidence;
                    bestScene = one.Scene;
                    edge = one.Edge;
                    tab = one.Tab;
                    feature = one.FeatureRoi;
                    featureRanks = one.FeatureRanks;
                    if (!ReferenceEquals(previewBase, one.PreviewBase))
                        previewBase?.Dispose();
                    previewBase = one.PreviewBase;
                    keptPreview = true;
                    locks = one.Locks;
                }

                var countOk = expected == 0 || one.InstanceCount == expected;
                if (countOk && one.Confidence >= bestMatchConf)
                {
                    bestMatchConf = one.Confidence;
                    matchScene = one.Scene;
                    matchEdge = one.Edge;
                    matchTab = one.Tab;
                    matchFeature = one.FeatureRoi;
                    matchRanks = one.FeatureRanks;
                    if (!ReferenceEquals(matchPreviewBase, one.PreviewBase))
                        matchPreviewBase?.Dispose();
                    matchPreviewBase = one.PreviewBase;
                    keptPreview = true;
                    matchLocks = one.Locks;
                }

                if (!keptPreview)
                    one.PreviewBase?.Dispose();
            }
            catch (Exception)
            {
                perFrame.Add([]);
            }
        }

        var unstable = instanceCounts.Distinct().Count() > 1
                       || (expected > 0 && instanceCounts.Exists(c => c != expected));
        if (matchScene is not null)
        {
            bestScene = matchScene;
            edge = matchEdge;
            tab = matchTab;
            feature = matchFeature;
            featureRanks = matchRanks;
            if (!ReferenceEquals(previewBase, matchPreviewBase))
                previewBase?.Dispose();
            previewBase = matchPreviewBase;
            matchPreviewBase = null;
            locks = matchLocks;
        }
        else
            matchPreviewBase?.Dispose();

        var lastCount = instanceCounts.Count > 0 ? instanceCounts.Max() : 0;
        var aggregated = SegmentRefineBakeOff.Aggregate(perFrame);
        return new AnalysisResult(bestScene, aggregated, edge, tab, feature, featureRanks, null, detected, files.Count,
            detected == 0
                ? "回放未检出。请检查检测 ROI / 模型 / 画面内是否有目标，或勾选双 BLOB 后按任务继续。"
                : $"回放 {detected}/{files.Count} 检出。极性/特征框优先取件数匹配且置信最高的帧；赛马分已含角度稳定性（角σ）。"
                  + (unstable ? " 整夹件数不稳，请核对漏检。" : ""),
            bestMatchConf > 0 ? bestMatchConf : (bestConf > 0 ? bestConf : 0),
            locks, lastCount, unstable, votes.Count == 0 ? null : votes, perFrame, instanceCounts,
            previewBase);
    }

    private AnalysisResult AnalyzeImage(
        VisionImage image,
        int totalHint,
        ModelSession? session = null,
        Mat? templateMat = null,
        SegmentRefineBakeOff.TeachCache? teachCache = null,
        bool keepPreview = true)
    {
        var editor = _host.Editor;
        using var roiOwned = RoiHelper.CropToVisionImage(image, editor.Roi, out var ox, out var oy);
        var roiView = roiOwned ?? image;
        using var roiMat = VisionImageCv.AsMat(roiView);

        if (UseBlobsWithoutModel)
        {
            var blobPreview = keepPreview ? RenderPreviewBase(image, editor.Roi) : null;
            return new AnalysisResult(null, [], default, default, null, [], null, 0, totalHint,
                "已按双 BLOB 任务推荐，未跑分割。", PreviewBase: blobPreview);
        }

        if (editor.Models.Count == 0 || string.IsNullOrWhiteSpace(editor.Models[0]))
        {
            var noModelPreview = keepPreview ? RenderPreviewBase(image, editor.Roi) : null;
            return new AnalysisResult(null, [], default, default, null, [], null, 0, totalHint,
                "未选分割模型。可勾选双 BLOB，或先在配方里选模型再分析。", PreviewBase: noModelPreview);
        }

        session ??= _models.Open(editor.Models[0], InferenceTask.Segmentation);
        var results = session.Run(y => y.RunSegmentation(
            roiView, editor.Confidence, editor.Segmentation.PixelConfidence, editor.Iou));
        var valid = results.Where(s =>
            (double)s.Box.Width * s.Box.Height >= 400 && s.ContourLocal.Count >= 4).ToList();
        if (valid.Count == 0)
        {
            var missPreview = keepPreview ? RenderPreviewBase(image, editor.Roi) : null;
            return new AnalysisResult(null, [], default, default, null, [], null, 0, totalHint,
                "分割未检出有效目标。请缩小检测 ROI、检查模型/阈值，或确认画面里有件。", PreviewBase: missPreview);
        }

        var expected = Constraints.ExpectedCount;
        var countUnstable = expected > 0 && valid.Count != expected;
        var seg = valid.OrderByDescending(s => s.Confidence).First();
        var box = seg.Box;
        var points = new Point2f[seg.ContourLocal.Count];
        for (var i = 0; i < seg.ContourLocal.Count; i++)
        {
            var p = seg.ContourLocal[i];
            points[i] = new Point2f((float)(p.X + box.X), (float)(p.Y + box.Y));
        }

        Mat? ownedTemplate = null;
        if (templateMat is null && !string.IsNullOrEmpty(editor.Template.TemplateImageBase64))
            ownedTemplate = MaskTemplateMatcher.DecodeTemplatePng(editor.Template.TemplateImageBase64);
        using var ownedTemplateScope = ownedTemplate;
        var template = templateMat ?? ownedTemplate;

        var advice = SegmentRefineAdvisor.Analyze(
            roiMat, points,
            bitPackedMask: seg.BitPackedMask,
            maskWidth: box.Width,
            maskHeight: box.Height,
            template: editor.Template,
            templateImage: template,
            fullImageWidth: image.Width,
            fullImageHeight: image.Height,
            originX: ox,
            originY: oy,
            instanceConfidence: seg.Confidence,
            boxConfidence: editor.Confidence,
            pixelConfidence: editor.Segmentation.PixelConfidence,
            task: Constraints,
            teachCache: teachCache,
            prior: CurrentPrior);
        var scene = advice.Scene;
        var bakeoff = advice.Candidates;
        var edge = advice.EdgePolarity;
        var tab = advice.TabPolarity;
        IReadOnlyList<FeatureRoiCandidate> ranks = [];
        Roi? feature = advice.SuggestedFeatureRoi;
        try
        {
            var crop = MaskTemplateMatcher.UprightCrop(roiMat, points, 0.05);
            using (crop.Upright)
            {
                ranks = FeatureRoiAdvisor.Rank(
                    crop, image.Width, image.Height, points, ox, oy);
                feature = ranks.FirstOrDefault()?.Roi ?? feature;
            }
        }
        catch (InvalidOperationException)
        {
        }

        var previewBase = keepPreview
            ? RenderPreviewBase(image, editor.Roi, roiMat, points, ox, oy, valid, editor.Template)
            : null;
        var msg = countUnstable
            ? $"已分类并赛马。本帧检出 {valid.Count} 件，期望 {expected}，场景按置信最高的一颗。"
            : "已根据当前画面分类场景并完成精修赛马。";
        IReadOnlyDictionary<SceneKind, int>? votes = scene is { } described
            ? new Dictionary<SceneKind, int> { [described.Kind] = 1 }
            : null;
        return new AnalysisResult(scene, bakeoff, edge, tab, feature, ranks, null, 1, totalHint,
            msg, seg.Confidence, advice, valid.Count, countUnstable, votes, [bakeoff], [valid.Count],
            previewBase);
    }

    private static Mat RenderPreviewBase(
        VisionImage image,
        Roi? detection,
        Mat? roiMat = null,
        IReadOnlyList<Point2f>? champion = null,
        double ox = 0,
        double oy = 0,
        IReadOnlyList<InstanceSegmentation>? instances = null,
        TemplateOptions? template = null)
    {
        using var mat = VisionImageCv.AsMat(image);
        var drawn = mat.Clone();
        if (instances is { Count: > 0 })
        {
            var champ = instances.MaxBy(x => x.Confidence) ?? instances[0];
            foreach (var s in instances)
            {
                var pts = new OpenCvSharp.Point[s.ContourLocal.Count];
                for (var k = 0; k < s.ContourLocal.Count; k++)
                {
                    var p = s.ContourLocal[k];
                    pts[k] = new OpenCvSharp.Point(
                        (int)Math.Round(p.X + s.Box.X + ox),
                        (int)Math.Round(p.Y + s.Box.Y + oy));
                }

                var isChamp = ReferenceEquals(s, champ);
                Cv2.Polylines(drawn, [pts], true, isChamp ? Scalar.Lime : Scalar.Cyan,
                    isChamp ? 2 : 1, LineTypes.AntiAlias);
            }
        }
        else if (champion is { Count: >= 3 })
        {
            var pts = champion.Select(p => new OpenCvSharp.Point(
                (int)Math.Round(p.X + ox), (int)Math.Round(p.Y + oy))).ToArray();
            Cv2.Polylines(drawn, [pts], true, Scalar.Lime, 2, LineTypes.AntiAlias);
        }

        if (roiMat is not null && champion is { Count: >= 4 })
        {
            var caliper = MaskCaliperTab.TryRefine(roiMat, champion, CaliperRefineOptions.From(template));
            DrawCaliperViz(drawn, caliper.Viz, ox, oy);
        }

        if (detection is { } det)
            OverlayDrawer.DrawNormalizedRoi(drawn, det, "检测", Scalar.Lime);
        return drawn;
    }

    private static void DrawCaliperViz(Mat drawn, MaskCaliperTab.CaliperViz viz, double ox, double oy)
    {
        OpenCvSharp.Point Map(OpenCvSharp.Point2d p) =>
            new((int)Math.Round(p.X + ox), (int)Math.Round(p.Y + oy));

        foreach (var bar in viz.SearchBars)
            Cv2.Line(drawn, Map(bar.A), Map(bar.B), Scalar.Cyan, 1, LineTypes.AntiAlias);
        foreach (var bar in viz.InvalidBars)
            Cv2.Line(drawn, Map(bar.A), Map(bar.B), Scalar.Gray, 1, LineTypes.AntiAlias);
        if (viz.FittedMinus is { } minus)
            Cv2.Line(drawn, Map(minus.A), Map(minus.B), Scalar.Magenta, 2, LineTypes.AntiAlias);
        if (viz.FittedPlus is { } plus)
            Cv2.Line(drawn, Map(plus.A), Map(plus.B), Scalar.Magenta, 2, LineTypes.AntiAlias);
        foreach (var p in viz.Inliers)
            Cv2.Circle(drawn, Map(p), 3, Scalar.Cyan, -1, LineTypes.AntiAlias);
        foreach (var p in viz.Rejected)
            Cv2.Circle(drawn, Map(p), 3, Scalar.IndianRed, -1, LineTypes.AntiAlias);
    }

    private IReadOnlyList<string>? TryPlaybackFiles()
    {
        var id = _host.Editor.CameraId;
        if (string.IsNullOrWhiteSpace(id) || !_cameras.TryGet(id, out var cam))
            return null;
        return cam is FileCamera file && file.PlaybackFiles.Count > 0 ? file.PlaybackFiles : null;
    }

    private VisionImage MaybeUndistort(string cameraId, VisionImage source, out VisionImage? undistorted)
    {
        undistorted = null;
        if (string.IsNullOrEmpty(_host.Editor.StationId) || !_calibration.HasPolynomial(_host.Editor.StationId))
        {
            try
            {
                undistorted = _calibration.Undistort(cameraId, source);
                return undistorted;
            }
            catch (VisionException)
            {
                return source;
            }
        }

        return source;
    }

    private void Report(string text) =>
        UiDispatch.Begin(() => Message = text);
}
