using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;
using System.ComponentModel;
using System.Windows.Media;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>精修方法参数详情弹窗：编辑当前 <see cref="TemplateOptions"/>，取消时恢复快照。</summary>
internal sealed partial class RefineMethodDetailsViewModel : ObservableObject, ICommitPendingEdits
{
    private readonly RecipeViewModel _host;
    private TemplateOptions _snapshot;
    private bool _useTemplateRoiSnapshot;

    public RefineMethodDetailsViewModel(RecipeViewModel host)
    {
        _host = host;
        _snapshot = host.Editor.Template.Clone();
        _useTemplateRoiSnapshot = host.Roi.UseTemplateRoi;
        _host.PropertyChanged += OnHostPropertyChanged;
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecipeViewModel.PolarityLockHint)
            or nameof(RecipeViewModel.FeatureGrabOriginHint))
        {
            OnPropertyChanged(nameof(PolarityLockHint));
            OnPropertyChanged(nameof(HasPolarityLockHint));
            OnPropertyChanged(nameof(FeatureGrabOriginHint));
            OnPropertyChanged(nameof(HasFeatureGrabOriginHint));
        }
    }

    private void DetachHost() => _host.PropertyChanged -= OnHostPropertyChanged;

    internal void DetachHostForClose() => DetachHost();

    public RecipeViewModel Host => _host;

    public Action? FlushPendingEdits { get; set; }

    public TemplateOptions Template => _host.Editor.Template;

    public RecipeRoiEditor Roi => _host.Roi;

    public ImageSource? TemplatePreview => _host.TemplatePreview;

    public bool HasTemplate => _host.HasTemplate;

    public string TemplateStatusText => _host.TemplateStatusText;

    public string MethodTitle =>
        $"{_host.RefineGuidance.MethodLabel(Template.RefineMethod)} · 参数";

    public string MethodHint => _host.RefineMethodHint;

    public string TeachGeometryHint => _host.TeachGeometryHint;

    public string TeachPeakHint => _host.TeachPeakHint;

    public string RefineMethodScoreHint => _host.Test.RefineMethodScoreHint;

    public bool HasRefineMethodScoreHint => !string.IsNullOrEmpty(RefineMethodScoreHint);

    public string LastRefineQualityHint => _host.Test.LastRefineQualityHint;

    public bool HasRefineQualityHint => !string.IsNullOrEmpty(LastRefineQualityHint);

    public string TeachDiagnosticsHint => _host.TeachDiagnosticsHint;

    public bool HasTeachDiagnostics => !string.IsNullOrEmpty(TeachDiagnosticsHint);

    public string PolarityLockHint => _host.PolarityLockHint;

    public bool HasPolarityLockHint => !string.IsNullOrEmpty(PolarityLockHint);

    public string FeatureGrabOriginHint => _host.FeatureGrabOriginHint;

    public bool HasFeatureGrabOriginHint => !string.IsNullOrEmpty(FeatureGrabOriginHint);

    public bool NeedsTaughtTemplate => _host.NeedsTaughtTemplate;

    public bool ShowTemplatePanel => Template.RefineMethod == SegmentRefineMethod.Template;

    public bool ShowShapePanel => Template.RefineMethod == SegmentRefineMethod.ShapeMatch;

    public bool ShowSiftPanel => Template.RefineMethod == SegmentRefineMethod.Sift;

    public bool ShowCaliperPanel => Template.RefineMethod == SegmentRefineMethod.CaliperTab;

    public bool ShowLineFitPanel => Template.RefineMethod == SegmentRefineMethod.LineFit;

    public bool ShowCentroidPanel => Template.RefineMethod == SegmentRefineMethod.CentroidHoleLine;

    public bool ShowRefineRange =>
        Template.RefineMethod is SegmentRefineMethod.Template
            or SegmentRefineMethod.ShapeMatch
            or SegmentRefineMethod.Sift;

    public bool ShowNoFlip => ShowRefineRange;

    public bool ShowFeatureRoi => TemplateOptions.UsesFeatureTeachRoi(Template.RefineMethod);

    public bool ShowPolarity =>
        Template.RefineMethod is SegmentRefineMethod.CaliperTab
            or SegmentRefineMethod.Template
            or SegmentRefineMethod.LineFit;

    public string AdaptiveRangeHint =>
        ShowRefineRange
            ? $"配置 {TemplateOptions.FormatRefineAngleWindow(Template)}。细长件（轴比≥2.5）运行时每侧最多 ±3°；中等轴比最多 ±5°；近方形用完整配置。"
            : "";

    public string SuggestedMatchThresholdHint =>
        Template.TeachPeakScore >= 0.3
            ? $"示教峰 {Template.TeachPeakScore:0.00} → 建议匹配阈值 {TemplateOptions.MatchThresholdFromTeachPeak(Template.TeachPeakScore):0.00}"
            : "尚未记录示教峰（示教模板后写入）。";

    /// <summary>角度范围语义提示：输入值为半宽 ±range°；NoFlip 时仅 0 支（无 ±180° 额外搜索）。</summary>
    public string RefineRangeHint => TemplateOptions.RefineRangeHintText(Template);

    public IReadOnlyList<EnumItem<HousingEdgePolarity>> EdgePolarityOptions { get; } =
    [
        new(HousingEdgePolarity.Auto, "自动（先亮场再暗场）"),
        new(HousingEdgePolarity.BrightToDark, "亮场（背景亮、壳体暗）"),
        new(HousingEdgePolarity.DarkToBright, "暗场（背景暗、壳体亮）"),
    ];

    public IReadOnlyList<EnumItem<TabPolarityLock>> TabPolarityOptions { get; } =
    [
        new(TabPolarityLock.Auto, "自动（每帧实测凸起侧）"),
        new(TabPolarityLock.PlusShortAxis, "示教：凸起在 +短轴"),
        new(TabPolarityLock.MinusShortAxis, "示教：凸起在 −短轴"),
    ];

    public IReadOnlyList<EnumItem<ShapeMatchMetric>> ShapeMetricOptions { get; } =
    [
        new(ShapeMatchMetric.UsePolarity, "使用极性（亮暗方向一致）"),
        new(ShapeMatchMetric.IgnoreLocalPolarity, "忽略局部极性"),
        new(ShapeMatchMetric.IgnoreGlobalPolarity, "忽略全局极性"),
    ];

    public bool RequestTemplateRoiDrawAfterClose { get; private set; }

    public bool AcceptedByUser { get; private set; }

    public event Action? RequestClose;

    public void RestoreSnapshot()
    {
        _snapshot.CopyTo(_host.Editor.Template);
        _host.Roi.UseTemplateRoi = _useTemplateRoiSnapshot;
        ((IRecipeWorkspace)_host).RefreshEditorBindings();
    }

    public void NotifyMethodUiChanged()
    {
        OnPropertyChanged(nameof(MethodTitle));
        OnPropertyChanged(nameof(MethodHint));
        OnPropertyChanged(nameof(ShowTemplatePanel));
        OnPropertyChanged(nameof(ShowShapePanel));
        OnPropertyChanged(nameof(ShowSiftPanel));
        OnPropertyChanged(nameof(ShowCaliperPanel));
        OnPropertyChanged(nameof(ShowLineFitPanel));
        OnPropertyChanged(nameof(ShowCentroidPanel));
        OnPropertyChanged(nameof(ShowRefineRange));
        OnPropertyChanged(nameof(ShowNoFlip));
        OnPropertyChanged(nameof(ShowFeatureRoi));
        OnPropertyChanged(nameof(ShowPolarity));
        OnPropertyChanged(nameof(RefineRangeHint));
        OnPropertyChanged(nameof(AdaptiveRangeHint));
        OnPropertyChanged(nameof(SuggestedMatchThresholdHint));
        OnPropertyChanged(nameof(TeachGeometryHint));
        OnPropertyChanged(nameof(TeachPeakHint));
        OnPropertyChanged(nameof(RefineMethodScoreHint));
        OnPropertyChanged(nameof(HasRefineMethodScoreHint));
        OnPropertyChanged(nameof(LastRefineQualityHint));
        OnPropertyChanged(nameof(HasRefineQualityHint));
        OnPropertyChanged(nameof(TeachDiagnosticsHint));
        OnPropertyChanged(nameof(HasTeachDiagnostics));
        OnPropertyChanged(nameof(PolarityLockHint));
        OnPropertyChanged(nameof(HasPolarityLockHint));
        OnPropertyChanged(nameof(FeatureGrabOriginHint));
        OnPropertyChanged(nameof(HasFeatureGrabOriginHint));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(TemplatePreview));
        OnPropertyChanged(nameof(TemplateStatusText));
    }

    public void NotifyPolarityHintChanged()
    {
        OnPropertyChanged(nameof(PolarityLockHint));
        OnPropertyChanged(nameof(HasPolarityLockHint));
    }

    [RelayCommand]
    private async Task TeachTemplateAsync()
    {
        var before = _host.Editor.Template.TemplateImageBase64 ?? "";
        if (_host.Test.TeachTemplateCommand.CanExecute(null))
            await _host.Test.TeachTemplateCommand.ExecuteAsync(null);
        if (!string.Equals(_host.Editor.Template.TemplateImageBase64 ?? "", before, StringComparison.Ordinal))
        {
            _snapshot = _host.Editor.Template.Clone();
            _useTemplateRoiSnapshot = _host.Roi.UseTemplateRoi;
        }
        NotifyMethodUiChanged();
    }

    [RelayCommand]
    private void DrawFeatureRoi()
    {
        if (!ShowFeatureRoi)
            return;
        this.Commit();
        Template.EnsureRefineAngleBounds();
        AcceptedByUser = true;
        RequestTemplateRoiDrawAfterClose = true;
        DetachHost();
        _host.NotifyEditorMutated();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void ClearRefineLine()
    {
        _host.Roi.ClearRefineLine();
        _host.NotifyEditorMutated();
    }

    [RelayCommand]
    private void Accept()
    {
        this.Commit();
        Template.EnsureRefineAngleBounds();
        AcceptedByUser = true;
        DetachHost();
        _host.NotifyEditorMutated();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        AcceptedByUser = false;
        DetachHost();
        RequestClose?.Invoke();
    }

}
