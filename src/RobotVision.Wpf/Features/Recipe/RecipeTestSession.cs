using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>试触发与示教模板：绑定状态与命令，编排委托 <see cref="IRecipeTestService"/>。</summary>
public sealed partial class RecipeTestSession : ObservableObject, IDisposable
{
    private readonly IRecipeWorkspace _host;
    private readonly IRecipeTestService _recipeTest;
    private readonly ISegmentRefineGuidance _refineGuidance;
    private readonly IFrameOverlayPresenter _overlay;
    private readonly IDialogService _dialogs;

    internal RecipeTestSession(
        IRecipeWorkspace host,
        IRecipeTestService recipeTest,
        ISegmentRefineGuidance refineGuidance,
        IFrameOverlayPresenter overlay,
        IDialogService dialogs)
    {
        _host = host;
        _recipeTest = recipeTest;
        _refineGuidance = refineGuidance;
        _overlay = overlay;
        _dialogs = dialogs;
    }

    internal IDialogService Dialogs => _dialogs;

    private RecipeConfig Editor => _host.Editor;

    [ObservableProperty]
    private ImageSource? _resultImage;

    [ObservableProperty]
    private VisionResult? _lastPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTriggerPoseFields))]
    private bool _includeTriggerPose;

    public bool ShowTriggerPoseFields => IncludeTriggerPose;

    [ObservableProperty]
    private double _triggerPoseX;

    [ObservableProperty]
    private double _triggerPoseY;

    [ObservableProperty]
    private double _triggerPoseRz;

    public void NotifyCanExecuteChanged()
    {
        TestTriggerCommand.NotifyCanExecuteChanged();
        TeachTemplateCommand.NotifyCanExecuteChanged();
    }

    public void ClearAdvice()
    {
        _lastAdvice = null;
        RefineAdviceText = "";
        ShowRefineAdviceReminder = false;
        OnPropertyChanged(nameof(RefineMethodScoreHint));
    }

    [ObservableProperty]
    private string _refineAdviceText = "";

    [ObservableProperty]
    private string _lastRefineQualityHint = "";

    [ObservableProperty]
    private bool _showRefineAdviceReminder;

    private SegmentRefineAdvice? _lastAdvice;

    private bool CanTeachTemplate =>
        !_host.IsBusy &&
        !_host.IsPipelineOccupied &&
        Editor.AngleMode == AngleMode.MaskTemplate &&
        TemplateOptions.NeedsTaughtImage(Editor.Template.RefineMethod);

    [RelayCommand(CanExecute = nameof(CanTeachTemplate))]
    private async Task TeachTemplateAsync()
    {
        if (!CanTeachTemplate)
            return;
        _host.CommitEdits();
        var cameraId = Editor.CameraId;
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            _host.Message = "示教模板：请先选择相机";
            return;
        }
        if (Editor.Models.Count == 0 || string.IsNullOrWhiteSpace(Editor.Models[0]))
        {
            _host.Message = "示教模板：请先选择分割模型";
            return;
        }

        _host.IsBusy = true;
        try
        {
            _host.Message = $"示教模板取图中 · {cameraId} …";
            var shot = await _recipeTest.TeachTemplateAsync(new RecipeTeachTemplateRequest(
                Editor.Clone(),
                _host.PlaybookPrior,
                cameraId,
                Editor.LightControllerId ?? "",
                Editor.Lighting)).ConfigureAwait(true);

            Editor.Template.TemplateImageBase64 = shot.TemplateImageBase64;
            ApplyTeachLocks(shot.Advice, Editor.Template.RefineMethod);
            ApplyTeachGeometry(shot.Advice);
            RecipeDetectionGatePrompt.TryConfirmAndApply(shot.Advice, Editor, _dialogs);
            _host.NotifyEditorMutated();
            SetAdvice(shot.Advice);
            var fallbackHint = shot.FeatureFallback
                ? " · 特征框未盖住目标，已改整颗示教"
                : "";
            var locksNote = TeachLocksAppliedNote(Editor.Template.RefineMethod);
            var size = Editor.Template.Roi is not null && !shot.FeatureFallback
                ? $"特征 {shot.TemplateWidth}×{shot.TemplateHeight}px"
                : $"{shot.TemplateWidth}×{shot.TemplateHeight}px";
            _host.Message = $"模板已示教（{size}）{locksNote}，保存后上产线{fallbackHint}";
        }
        catch (Exception ex)
        {
            _host.Message = $"示教模板失败: {ex.Message}";
        }
        finally
        {
            _host.IsBusy = false;
        }
    }

    private bool CanOperate => !_host.IsBusy && _host.CanTestTrigger && !_host.IsPipelineOccupied;

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task TestTriggerAsync()
    {
        _host.CommitEdits();
        if (!_host.CanTestTrigger)
        {
            if (_host.TestTriggerBlockReason is { } reason)
                _host.Message = $"无法测试触发：{reason}";
            return;
        }
        if (!_host.ConfirmGrabOriginIfNeeded("测试触发"))
            return;

        _host.IsBusy = true;
        _host.OnTestStarting();
        LastRefineQualityHint = "";
        var clone = Editor.Clone();
        try
        {
            var hint = _host.HasUnsavedChanges ? "编辑器（未保存）" : clone.Name;
            _host.Message = $"测试触发中：{hint} …";
            var preview = await _recipeTest.RunPreviewAsync(new RecipePreviewRequest(
                clone,
                _host.RecipeTestTimeoutMs,
                IncludeTriggerPose,
                TriggerPoseX,
                TriggerPoseY,
                TriggerPoseRz)).ConfigureAwait(true);
            LastPreview = preview.Result;
            ApplyPreviewFrame(preview.Frame);
            LastRefineQualityHint = preview.RefineQualityHint ?? "";
            _host.Message = RecipeTestTriggerMessages.FormatPreviewResult(preview.Result, _host.HasUnsavedChanges);
        }
        catch (Exception ex)
        {
            _host.Message = RecipeTestTriggerMessages.FormatException(ex, _host.RecipeTestTimeoutMs);
        }
        finally
        {
            _host.IsBusy = false;
        }
    }

    private void ApplyPreviewFrame(PreviewRunOutcome? frame)
    {
        if (frame is null)
            return;
        try
        {
            _overlay.Compose(frame.UndistortedImage, frame.PixelPoses, frame.DisplayHints);
            var source = ImagePresentation.FromVisionImage(frame.UndistortedImage);
            UiDispatch.Begin(() => ResultImage = source);
        }
        catch (Exception ex)
        {
            // 绘制失败不影响试触发结论，但留痕以便排查
            Trace.TraceWarning("[RecipeTestSession] 预览帧绘制失败: {0}", ex);
        }
        finally
        {
            frame.Dispose();
        }
    }

    private void ApplyTeachLocks(SegmentRefineAdvice advice, SegmentRefineMethod refineMethod)
    {
        var t = Editor.Template;
        switch (refineMethod)
        {
            case SegmentRefineMethod.Template:
                if (advice.TeachPeakScore >= 0.3)
                {
                    t.TeachPeakScore = advice.TeachPeakScore;
                    if (advice.SuggestedMatchThreshold > 0)
                        t.MatchThreshold = advice.SuggestedMatchThreshold;
                }
                if (advice.EdgePolarity != HousingEdgePolarity.Auto)
                    t.HousingEdgePolarity = advice.EdgePolarity;
                break;
            case SegmentRefineMethod.CaliperTab:
                if (advice.EdgePolarity != HousingEdgePolarity.Auto)
                    t.HousingEdgePolarity = advice.EdgePolarity;
                if (advice.TabPolarity != TabPolarityLock.Auto)
                    t.TabPolarity = advice.TabPolarity;
                break;
            case SegmentRefineMethod.ShapeMatch:
            case SegmentRefineMethod.Sift:
            case SegmentRefineMethod.LineFit:
            case SegmentRefineMethod.CentroidHoleLine:
                break;
        }
    }

    private static string TeachLocksAppliedNote(SegmentRefineMethod refineMethod) => refineMethod switch
    {
        SegmentRefineMethod.Template => " · 示教峰/匹配阈值/极性已写入",
        SegmentRefineMethod.CaliperTab => " · 极性已写入",
        _ => "",
    };

    private void ApplyTeachGeometry(SegmentRefineAdvice advice)
    {
        if (advice.TeachAreaPx > 1)
            Editor.Template.TeachAreaPx = advice.TeachAreaPx;
        if (advice.Aspect > 1e-3)
            Editor.Template.TeachAspect = advice.Aspect;
    }

    private void SetAdvice(SegmentRefineAdvice advice)
    {
        _lastAdvice = advice;
        RefineAdviceText = _refineGuidance.FormatBriefAdvice(advice, Editor.Template.RefineMethod);
        ShowRefineAdviceReminder =
            Editor.AngleMode == AngleMode.MaskTemplate && ShouldShowRefineAdviceReminder(advice, Editor);
        OnPropertyChanged(nameof(RefineMethodScoreHint));
    }

    /// <summary>示教/分析后当前精修方法在赛马中的得分提示（详情窗用，非完整推荐）。</summary>
    public string RefineMethodScoreHint =>
        _lastAdvice is null
            ? ""
            : _refineGuidance.FormatMethodScoreHint(_lastAdvice, Editor.Template.RefineMethod);

    /// <summary>
    /// 本页示教只提醒「当前方法下参数可调」。方法赛马与期望件数 0（不检查）不去催改。
    /// </summary>
    internal static bool ShouldShowRefineAdviceReminder(SegmentRefineAdvice advice, RecipeConfig editor)
    {
        var method = editor.Template.RefineMethod;
        switch (method)
        {
            case SegmentRefineMethod.Template:
                if (editor.Template.UseEdgeMatch != advice.RecommendEdgeMatch)
                    return true;
                if (advice.SuggestedMatchThreshold > 0 &&
                    Math.Abs(editor.Template.MatchThreshold - advice.SuggestedMatchThreshold) > 0.01)
                    return true;
                if (advice.EdgePolarity != HousingEdgePolarity.Auto &&
                    editor.Template.HousingEdgePolarity != advice.EdgePolarity)
                    return true;
                if (advice.SuggestedFeatureRoi is not null && editor.Template.Roi is null)
                    return true;
                break;
            case SegmentRefineMethod.CaliperTab:
                if (advice.EdgePolarity != HousingEdgePolarity.Auto &&
                    editor.Template.HousingEdgePolarity != advice.EdgePolarity)
                    return true;
                if (advice.TabPolarity != TabPolarityLock.Auto &&
                    editor.Template.TabPolarity != advice.TabPolarity)
                    return true;
                break;
        }

        if (advice.SuggestedConfidence > 0 &&
            Math.Abs(editor.Confidence - advice.SuggestedConfidence) > 0.02)
            return true;
        if (advice.SuggestedPixelConfidence > 0 &&
            Math.Abs(editor.Segmentation.PixelConfidence - advice.SuggestedPixelConfidence) > 0.02)
            return true;
        return false;
    }

    public void RefreshAdviceCanApply()
    {
        if (_lastAdvice is null)
            return;
        RefineAdviceText = _refineGuidance.FormatBriefAdvice(_lastAdvice, Editor.Template.RefineMethod);
        ShowRefineAdviceReminder =
            Editor.AngleMode == AngleMode.MaskTemplate && ShouldShowRefineAdviceReminder(_lastAdvice, Editor);
        OnPropertyChanged(nameof(RefineMethodScoreHint));
    }

    public void Dispose()
    {
        ResultImage = null;
        LastPreview = null;
    }
}
