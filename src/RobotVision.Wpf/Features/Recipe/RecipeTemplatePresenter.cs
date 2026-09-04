using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>掩码模板示教预览与精修参数摘要（配方页与详情窗共用）。</summary>
public sealed partial class RecipeTemplatePresenter : ObservableObject
{
    private readonly IRecipeWorkspace _host;
    private readonly IMaskTemplateTeachService _maskTeach;
    private readonly PageAsyncSession _pageSession;
    private readonly Func<bool> _hasUnsavedChanges;

    private string _templatePreviewKey = "\0";
    private string _templateDiagnosticsKey = "\0";

    internal RecipeTemplatePresenter(
        IRecipeWorkspace host,
        IMaskTemplateTeachService maskTeach,
        PageAsyncSession pageSession,
        Func<bool> hasUnsavedChanges)
    {
        _host = host;
        _maskTeach = maskTeach;
        _pageSession = pageSession;
        _hasUnsavedChanges = hasUnsavedChanges;
    }

    private RecipeConfig Editor => _host.Editor;

    [ObservableProperty]
    private ImageSource? _templatePreview;

    [ObservableProperty]
    private string _teachDiagnosticsHint = "";

    public bool IsMaskTemplateMode => Editor.AngleMode == AngleMode.MaskTemplate;

    public bool IsTemplateMethod => Editor.Template.RefineMethod == SegmentRefineMethod.Template;

    public bool UsesFeatureTeachRoi =>
        TemplateOptions.UsesFeatureTeachRoi(Editor.Template.RefineMethod);

    public bool NeedsTaughtTemplate =>
        TemplateOptions.NeedsTaughtImage(Editor.Template.RefineMethod);

    public bool ShowRefineRange =>
        Editor.Template.RefineMethod is SegmentRefineMethod.Template
            or SegmentRefineMethod.ShapeMatch
            or SegmentRefineMethod.Sift;

    public bool ShowMatchThreshold =>
        Editor.Template.RefineMethod is SegmentRefineMethod.Template
            or SegmentRefineMethod.ShapeMatch
            or SegmentRefineMethod.Sift;

    public bool HasTemplate => !string.IsNullOrEmpty(Editor.Template.TemplateImageBase64);

    private string TemplatePreviewSize =>
        TemplatePreview is { } img ? $"{(int)img.Width}×{(int)img.Height}px" : "";

    public string TemplateStatusText =>
        !HasTemplate
            ? "未示教模板：点击「示教模板」自动生成（画面需有目标）"
            : _hasUnsavedChanges()
                ? $"编辑器已有模板 {TemplatePreviewSize}（未保存不上产线）"
                : $"已示教模板 {TemplatePreviewSize}";

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
                    parts.Add($"角度 {TemplateOptions.FormatRefineAngleWindow(t)}");
                    if (t.MaxSecondPeakRatio < 1)
                        parts.Add($"二峰≤{t.MaxSecondPeakRatio:0.00}");
                    if (t.NoFlipConstraint)
                        parts.Add("不翻转");
                    if (t.UseEdgeMatch)
                        parts.Add("边缘定角");
                    if (!t.UseUprightCrop)
                        parts.Add("不转正");
                    break;
                case SegmentRefineMethod.ShapeMatch:
                    parts.Add($"阈值 {t.MatchThreshold:0.00}");
                    parts.Add($"角度 {TemplateOptions.FormatRefineAngleWindow(t)}");
                    parts.Add($"层数 {t.ShapeMatchNumLevels}");
                    if (t.NoFlipConstraint)
                        parts.Add("不翻转");
                    break;
                case SegmentRefineMethod.Sift:
                    parts.Add($"阈值 {t.MatchThreshold:0.00}");
                    parts.Add($"角度 {TemplateOptions.FormatRefineAngleWindow(t)}");
                    if (t.NoFlipConstraint)
                        parts.Add("不翻转");
                    break;
                case SegmentRefineMethod.LineFit:
                    if (t.LineFitSubpixel)
                        parts.Add("亚像素");
                    if (t.RefineLine is not null)
                        parts.Add("有基准线");
                    break;
                case SegmentRefineMethod.CaliperTab:
                    parts.Add("卡尺抓边");
                    break;
                case SegmentRefineMethod.CentroidHoleLine:
                    parts.Add("质心-孔槽");
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

    public string TeachGeometryHint =>
        Editor.Template.TeachAreaPx > 1
            ? $"示教几何：面积 {Editor.Template.TeachAreaPx:0} px²，轴比 {Editor.Template.TeachAspect:0.00}（面积 {Editor.Template.AreaRatioLo:0.00}~{Editor.Template.AreaRatioHi:0.00} 倍、轴比 {Editor.Template.AspectRatioLo:0.00}~{Editor.Template.AspectRatioHi:0.00} 倍过门）"
            : "未记示教几何：配方向导或示教模板后写入面积/轴比窗口；期望件数 0 表示不检查件数。";

    public string TeachPeakHint =>
        Editor.Template.TeachPeakScore >= 0.3
            ? $"示教峰 NCC {Editor.Template.TeachPeakScore:0.00} → 建议匹配阈值 {TemplateOptions.MatchThresholdFromTeachPeak(Editor.Template.TeachPeakScore):0.00}"
            : "";

    public string PolarityLockHint
    {
        get
        {
            if (!IsMaskTemplateMode)
                return "";
            var method = Editor.Template.RefineMethod;
            var edge = TemplateOptions.UsesHousingEdgePolarity(method)
                ? Editor.Template.HousingEdgePolarity switch
                {
                    HousingEdgePolarity.BrightToDark => "亮场",
                    HousingEdgePolarity.DarkToBright => "暗场",
                    _ => "",
                }
                : "";
            var tab = TemplateOptions.UsesTabPolarity(method)
                ? Editor.Template.TabPolarity switch
                {
                    TabPolarityLock.PlusShortAxis => "凸起在+短轴",
                    TabPolarityLock.MinusShortAxis => "凸起在−短轴",
                    _ => "",
                }
                : "";
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

    public void Refresh()
    {
        var b64 = Editor.Template.TemplateImageBase64 ?? "";
        var method = Editor.Template.RefineMethod;
        var diagKey = b64 + "|" + method;
        if (b64 == _templatePreviewKey)
        {
            OnPropertyChanged(nameof(TemplateStatusText));
            OnPropertyChanged(nameof(RefineDetailsSummary));
            if (diagKey == _templateDiagnosticsKey)
                return;
            ScheduleTeachDiagnostics(b64, method, diagKey);
            return;
        }

        _templatePreviewKey = b64;
        _templateDiagnosticsKey = diagKey;
        TemplatePreview = null;
        OnPropertyChanged(nameof(TemplateStatusText));
        OnPropertyChanged(nameof(RefineDetailsSummary));

        if (b64.Length == 0)
        {
            TeachDiagnosticsHint = "";
            return;
        }

        var generation = _pageSession.CaptureGeneration();
        var token = _pageSession.Token;
        _pageSession.Track(Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var preview = _maskTeach.TryDecodePreview(b64);
            var diag = _maskTeach.GetTeachDiagnostics(b64, method);
            return (preview, diag);
        }, token).ContinueWith(t =>
        {
            if (!_pageSession.IsCurrent(generation) || t.IsCanceled || t.IsFaulted)
                return;

            var (preview, diag) = t.Result;
            TemplatePreview = preview is null ? null : ImageConverter.ToBitmapSource(preview);
            TeachDiagnosticsHint = diag;
            OnPropertyChanged(nameof(TemplateStatusText));
            OnPropertyChanged(nameof(RefineDetailsSummary));
            OnPropertyChanged(nameof(TeachDiagnosticsHint));
        }, token, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()));
    }

    private void ScheduleTeachDiagnostics(string b64, SegmentRefineMethod method, string diagKey)
    {
        _templateDiagnosticsKey = diagKey;
        var generation = _pageSession.CaptureGeneration();
        var token = _pageSession.Token;
        _pageSession.Track(Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            return _maskTeach.GetTeachDiagnostics(b64, method);
        }, token).ContinueWith(t =>
        {
            if (!_pageSession.IsCurrent(generation) || t.IsCanceled || t.IsFaulted)
                return;

            TeachDiagnosticsHint = t.Result;
            OnPropertyChanged(nameof(TeachDiagnosticsHint));
        }, token, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()));
    }

    public void NotifyEditorBindings()
    {
        OnPropertyChanged(nameof(IsMaskTemplateMode));
        OnPropertyChanged(nameof(IsTemplateMethod));
        OnPropertyChanged(nameof(UsesFeatureTeachRoi));
        OnPropertyChanged(nameof(NeedsTaughtTemplate));
        OnPropertyChanged(nameof(ShowRefineRange));
        OnPropertyChanged(nameof(ShowMatchThreshold));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(RefineDetailsSummary));
        OnPropertyChanged(nameof(TeachPeakHint));
        OnPropertyChanged(nameof(TeachDiagnosticsHint));
        OnPropertyChanged(nameof(PolarityLockHint));
        OnPropertyChanged(nameof(FeatureGrabOriginHint));
        OnPropertyChanged(nameof(TeachGeometryHint));
        OnPropertyChanged(nameof(TemplateStatusText));
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
}
