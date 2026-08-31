using System.Windows.Media;
using OpenCvSharp;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>试触发与示教模板：管线快照、拍照位姿、分割示教。</summary>
public sealed partial class RecipeTestSession : ObservableObject, IDisposable
{
    private readonly IRecipeWorkspace _host;
    private readonly VisionService _vision;
    private readonly ICameraRuntime _cameras;
    private readonly IModelRuntime _models;
    private readonly ICalibrationRuntime _calibration;
    private readonly ILightingRuntime _lighting;
    private readonly IDialogService _dialogs;

    internal RecipeTestSession(
        IRecipeWorkspace host,
        VisionService vision,
        ICameraRuntime cameras,
        IModelRuntime models,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting,
        IDialogService dialogs)
    {
        _host = host;
        _vision = vision;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _lighting = lighting;
        _dialogs = dialogs;
    }

    internal IDialogService Dialogs => _dialogs;

    private RecipeConfig Editor => _host.Editor;

    private RecipePrior? PlaybookPrior =>
        ScenePlaybook.Merge(ScenePlaybook.FromTemplate(Editor.Template), _host.PlaybookPrior);

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
            using var lightingScope = _lighting.Apply(Editor.LightControllerId, Editor.Lighting);
            if (lightingScope.StabilizeDelayMs > 0)
                await Task.Delay(lightingScope.StabilizeDelayMs);

            TeachShot shot;
            try
            {
                shot = await Task.Run(() => GrabTeachShot(cameraId));
            }
            finally
            {
                lightingScope.Dispose();
            }

            try
            {
                var b64 = MaskTemplateMatcher.EncodeTemplatePng(shot.Template);
                var w = shot.Template.Width;
                var h = shot.Template.Height;
                var advice = shot.Advice;
                Editor.Template.TemplateImageBase64 = b64;
                ApplyTeachLocks(advice, Editor.Template.RefineMethod);
                ApplyTeachGeometry(advice);
                RecipeDetectionGatePrompt.TryConfirmAndApply(advice, Editor, _dialogs);
                _host.NotifyEditorMutated();
                SetAdvice(advice);
                var fallbackHint = shot.FeatureFallback
                    ? " · 特征框未盖住目标，已改整颗示教"
                    : "";
                var locksNote = TeachLocksAppliedNote(Editor.Template.RefineMethod);
                var size = Editor.Template.Roi is not null && fallbackHint.Length == 0
                    ? $"特征 {w}×{h}px"
                    : $"{w}×{h}px";
                _host.Message = $"模板已示教（{size}）{locksNote}，保存后上产线{fallbackHint}";
            }
            finally
            {
                shot.Dispose();
            }
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

    private bool CanOperate => !_host.IsBusy && _host.CanTestTrigger;

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
        if (!_host.ConfirmFlatFeatureRoiIfNeeded("测试触发"))
            return;

        _host.IsBusy = true;
        _host.OnTestStarting();
        LastRefineQualityHint = "";
        var clone = Editor.Clone();
        var snapshotName = RecipeLoader.IsValidRecipeName(clone.Name) ? clone.Name : "preview";
        clone.Name = snapshotName;
        try
        {
            var hint = _host.HasUnsavedChanges ? "编辑器（未保存）" : snapshotName;
            _host.Message = $"测试触发中：{hint} …";
            using var cts = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(Math.Max(5000, _host.RecipeTestTimeoutMs)));
            TcpClientPose? pose = IncludeTriggerPose
                ? new TcpClientPose(TriggerPoseX, TriggerPoseY, TriggerPoseRz)
                : null;
            var preview = await _vision.RunPreviewAsync(clone, pose, cts.Token);
            LastPreview = preview.Result;
            ApplyPreviewFrame(preview.Frame);
            var result = preview.Result;
            LastRefineQualityHint = ExtractRefineQualityHint(preview.Frame?.PixelPoses);
            _host.Message = RecipeTestTriggerMessages.FormatPreviewResult(result, _host.HasUnsavedChanges);
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

    private static string ExtractRefineQualityHint(IReadOnlyList<PixelPose>? poses)
    {
        if (poses is null || poses.Count == 0)
            return "";
        for (var i = 0; i < poses.Count; i++)
        {
            var note = poses[i].Overlay?.RefineQualityNote;
            if (!string.IsNullOrEmpty(note))
                return note;
        }

        return "";
    }

    private void ApplyPreviewFrame(PreviewRunOutcome? frame)
    {
        if (frame is null)
            return;
        try
        {
            FrameOverlayComposer.Compose(frame.UndistortedImage, frame.PixelPoses, frame.DisplayHints);
            var source = ImageConverter.ToBitmapSource(frame.UndistortedImage);
            UiDispatch.Begin(() => ResultImage = source);
        }
        catch (Exception)
        {
            // 绘制失败不影响试触发结论
        }
        finally
        {
            frame.Dispose();
        }
    }

    private sealed class TeachShot(
        Mat template, double area, double aspect, double instanceConf, SegmentRefineAdvice advice,
        bool FeatureFallback) : IDisposable
    {
        public Mat Template { get; } = template;
        public double Area { get; } = area;
        public double Aspect { get; } = aspect;
        public double InstanceConf { get; } = instanceConf;
        public SegmentRefineAdvice Advice { get; } = advice;
        public bool FeatureFallback { get; } = FeatureFallback;

        public void Dispose() => Template.Dispose();
    }

    private TeachShot GrabTeachShot(string cameraId)
    {
        using var grabbed = _cameras.GrabForTeach(cameraId);
        using var image = RecipeEditorFrame.PrepareInferenceImage(_calibration, Editor, grabbed.Image);
        using var roiOwned = RoiHelper.CropToVisionImage(image, Editor.Roi, out var ox, out var oy);
        var roiView = roiOwned ?? image;
        var imgW = image.Width;
        var imgH = image.Height;
        var templateOpts = Editor.Template;
        var featureRoi = templateOpts?.Roi;
        var wantFeature = featureRoi is not null
            && TemplateOptions.UsesFeatureTeachRoi(templateOpts!.RefineMethod)
            && FeatureRoiAdvisor.IsDrawable(featureRoi, imgW, imgH);

        var session = _models.Open(Editor.Models[0], InferenceTask.Segmentation);
        var results = session.Run(y => y.RunSegmentation(
            roiView, Editor.Confidence, Editor.Segmentation.PixelConfidence, Editor.Iou));

        var valid = results.Where(s =>
            (double)s.Box.Width * s.Box.Height >= 400 && s.ContourLocal.Count >= 4).ToList();
        if (valid.Count == 0)
            throw new InvalidOperationException("分割未检出有效目标，无法示教（请确认模型/阈值/画面内有目标）");

        var useFeatureCrop = wantFeature;
        var featureFallback = false;
        IReadOnlyList<InstanceSegmentation> candidates = valid;
        if (useFeatureCrop)
        {
            var hits = FeatureRoiAdvisor.PickOverlapping(valid, featureRoi!, imgW, imgH, ox, oy);
            if (hits.Count == 0)
            {
                useFeatureCrop = false;
                featureFallback = true;
            }
            else
                candidates = hits;
        }

        IEnumerable<InstanceSegmentation> order = useFeatureCrop
            ? candidates
            : candidates.OrderByDescending(s => s.Confidence);
        foreach (var seg in order)
        {
            var box = seg.Box;
            var points = new Point2f[seg.ContourLocal.Count];
            for (var i = 0; i < seg.ContourLocal.Count; i++)
            {
                var p = seg.ContourLocal[i];
                points[i] = new Point2f((float)(p.X + box.X), (float)(p.Y + box.Y));
            }

            using var roiMat = VisionImageMat.AsMat(roiView);
            var crop = MaskTemplateMatcher.UprightCrop(roiMat, points, 0);
            using (crop.Upright)
            {
                Mat templateMat = crop.Upright;
                var ownedFeature = false;
                if (useFeatureCrop)
                {
                    try
                    {
                        templateMat = MaskTemplateMatcher.CropUprightBySourceRect(
                            crop,
                            featureRoi!.X * imgW - ox,
                            featureRoi.Y * imgH - oy,
                            featureRoi.Width * imgW,
                            featureRoi.Height * imgH);
                        ownedFeature = true;
                    }
                    catch (InvalidOperationException)
                    {
                        templateMat = crop.Upright;
                        featureFallback = true;
                    }
                }

                try
                {
                    var advice = SegmentRefineAdvisor.Analyze(
                        roiMat, points, crop.Upright, seg.BitPackedMask, box.Width, box.Height,
                        Editor.Template, templateMat, imgW, imgH, ox, oy,
                        instanceConfidence: seg.Confidence,
                        boxConfidence: Editor.Confidence,
                        pixelConfidence: Editor.Segmentation.PixelConfidence,
                        task: ScenePlaybook.FromRecipe(Editor),
                        prior: PlaybookPrior);
                    var owned = templateMat.Clone();
                    var area = Cv2.ContourArea(points);
                    var aspect = MaskHousing.Aspect(MaskHousing.FitObb(points));
                    return new TeachShot(owned, area, aspect, seg.Confidence, advice, featureFallback);
                }
                finally
                {
                    if (ownedFeature)
                        templateMat.Dispose();
                }
            }
        }
        throw new InvalidOperationException("分割未检出有效目标，无法示教（请确认模型/阈值/画面内有目标）");
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
        RefineAdviceText = SegmentRefineAdvisor.FormatBriefAdvice(advice, Editor.Template.RefineMethod);
        ShowRefineAdviceReminder =
            Editor.AngleMode == AngleMode.MaskTemplate && ShouldShowRefineAdviceReminder(advice, Editor);
        OnPropertyChanged(nameof(RefineMethodScoreHint));
    }

    /// <summary>示教/分析后当前精修方法在赛马中的得分提示（详情窗用，非完整推荐）。</summary>
    public string RefineMethodScoreHint =>
        _lastAdvice is null
            ? ""
            : SegmentRefineAdvisor.FormatMethodScoreHint(_lastAdvice, Editor.Template.RefineMethod);

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
        RefineAdviceText = SegmentRefineAdvisor.FormatBriefAdvice(_lastAdvice, Editor.Template.RefineMethod);
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
