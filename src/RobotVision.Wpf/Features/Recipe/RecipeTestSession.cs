using System.Windows.Media;
using OpenCvSharp;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core;
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

/// <summary>试触发与示教模板：管线快照、拍照位姿、分割示教。</summary>
public sealed partial class RecipeTestSession : ObservableObject
{
    private readonly IRecipeWorkspace _host;
    private readonly VisionService _vision;
    private readonly CameraManager _cameras;
    private readonly ModelManager _models;
    private readonly CalibrationManager _calibration;
    private readonly LightingManager _lighting;
    private readonly TcpServerManager _tcp;

    internal RecipeTestSession(
        IRecipeWorkspace host,
        VisionService vision,
        CameraManager cameras,
        ModelManager models,
        CalibrationManager calibration,
        LightingManager lighting,
        TcpServerManager tcp)
    {
        _host = host;
        _vision = vision;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _lighting = lighting;
        _tcp = tcp;
    }

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
        ApplyRefineAdviceCommand.NotifyCanExecuteChanged();
    }

    public void ClearAdvice()
    {
        _lastAdvice = null;
        RefineAdviceText = "";
        HasRefineAdvice = false;
        CanApplyRefineAdvice = false;
    }

    [ObservableProperty]
    private string _refineAdviceText = "";

    [ObservableProperty]
    private bool _hasRefineAdvice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyRefineAdviceCommand))]
    private bool _canApplyRefineAdvice;

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
                ApplyTeachLocks(advice);
                ApplyTeachGeometry(advice);
                ApplyDetectionGates(advice);
                _host.NotifyEditorMutated();
                SetAdvice(advice);
                var fallbackHint = shot.FeatureFallback
                    ? " · 特征框未盖住目标，已改整颗示教"
                    : "";
                var baseMsg = Editor.Template.Roi is not null && fallbackHint.Length == 0
                    ? $"模板已示教（特征 {w}×{h}px）· 极性/阈值已写入编辑器，保存后上产线"
                    : $"模板已示教（{w}×{h}px）· 极性/阈值已写入编辑器，保存后上产线";
                _host.Message = baseMsg + fallbackHint;
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
            return;

        _host.IsBusy = true;
        _host.OnTestStarting();
        var clone = Editor.Clone();
        var snapshotName = RecipeLoader.IsValidRecipeName(clone.Name) ? clone.Name : "preview";
        clone.Name = snapshotName;
        try
        {
            var hint = _host.HasUnsavedChanges ? "编辑器（未保存）" : snapshotName;
            _host.Message = $"测试触发中：{hint} …";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(500, _tcp.TimeoutMs)));
            TcpClientPose? pose = IncludeTriggerPose
                ? new TcpClientPose(TriggerPoseX, TriggerPoseY, TriggerPoseRz)
                : null;
            var preview = await _vision.RunPreviewAsync(clone, pose, cts.Token);
            LastPreview = preview.Result;
            ApplyPreviewFrame(preview.Frame);
            var result = preview.Result;
            string previewMsg;
            if (result.Ok)
                previewMsg = $"测试通过：{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms"
                    + (_host.HasUnsavedChanges ? "（编辑器，未保存不上产线）" : "");
            else if (result.ErrorCode == VisionErrorCode.RefineFailed)
                previewMsg = $"测试失败：ERR 1019 精修未过门 · {result.Message}";
            else
                previewMsg = $"测试失败：ERR {result.ErrorCode} · {result.Message}";
            _host.Message = previewMsg;
        }
        catch (Exception ex)
        {
            _host.Message = $"测试异常：{ex.Message}";
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
        var image = MaybeUndistort(cameraId, grabbed.Image, out var undistorted);
        using var undistortedScope = undistorted;
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

            using var roiMat = VisionImageCv.AsMat(roiView);
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

    private void ApplyTeachLocks(SegmentRefineAdvice advice)
    {
        if (advice.TeachPeakScore >= 0.3)
        {
            Editor.Template.TeachPeakScore = advice.TeachPeakScore;
            if (advice.SuggestedMatchThreshold > 0)
                Editor.Template.MatchThreshold = advice.SuggestedMatchThreshold;
        }
        if (advice.EdgePolarity != HousingEdgePolarity.Auto)
            Editor.Template.HousingEdgePolarity = advice.EdgePolarity;
        if (advice.TabPolarity != TabPolarityLock.Auto)
            Editor.Template.TabPolarity = advice.TabPolarity;
    }

    private void ApplyDetectionGates(SegmentRefineAdvice advice)
    {
        if (advice.SuggestedConfidence > 0)
            Editor.Confidence = advice.SuggestedConfidence;
        if (advice.SuggestedPixelConfidence > 0)
            Editor.Segmentation.PixelConfidence = advice.SuggestedPixelConfidence;
    }

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
        var orient = advice.CanResolveOrientation ? "可判头尾" : "头尾难分";
        RefineAdviceText =
            $"未点「采用推荐方法」则测试仍用当前下拉。推荐：{SegmentRefineAdvisor.MethodLabel(advice.Recommended)}（{orient}，轴比 {advice.Aspect:0.0}，0/180 分差 {advice.Separability:0.00}）。{advice.Summary}";
        HasRefineAdvice = true;
        CanApplyRefineAdvice =
            Editor.AngleMode == AngleMode.MaskTemplate && AdviceDiffers(advice);
        ApplyRefineAdviceCommand.NotifyCanExecuteChanged();
    }

    private bool AdviceDiffers(SegmentRefineAdvice advice) =>
        Editor.Template.RefineMethod != advice.Recommended ||
        Editor.Template.UseEdgeMatch != advice.RecommendEdgeMatch ||
        (advice.SuggestedMatchThreshold > 0 &&
         Math.Abs(Editor.Template.MatchThreshold - advice.SuggestedMatchThreshold) > 0.01) ||
        (advice.EdgePolarity != HousingEdgePolarity.Auto &&
         Editor.Template.HousingEdgePolarity != advice.EdgePolarity) ||
        (advice.TabPolarity != TabPolarityLock.Auto &&
         Editor.Template.TabPolarity != advice.TabPolarity) ||
        (advice.SuggestedFeatureRoi is not null &&
         TemplateOptions.UsesFeatureTeachRoi(advice.Recommended) &&
         Editor.Template.Roi is null) ||
        (advice.TeachAreaPx > 1 && Editor.Template.ExpectedCount == 0) ||
        (advice.SuggestedConfidence > 0 &&
         Math.Abs(Editor.Confidence - advice.SuggestedConfidence) > 0.02) ||
        (advice.SuggestedPixelConfidence > 0 &&
         Math.Abs(Editor.Segmentation.PixelConfidence - advice.SuggestedPixelConfidence) > 0.02);

    public void RefreshAdviceCanApply()
    {
        if (_lastAdvice is null)
            return;
        CanApplyRefineAdvice =
            Editor.AngleMode == AngleMode.MaskTemplate && AdviceDiffers(_lastAdvice);
    }

    private bool CanApplyRefineAdviceNow =>
        !_host.IsBusy && _lastAdvice is not null && CanApplyRefineAdvice;

    [RelayCommand(CanExecute = nameof(CanApplyRefineAdviceNow))]
    private void ApplyRefineAdvice()
    {
        if (_lastAdvice is null)
            return;
        Editor.Template.RefineMethod = _lastAdvice.Recommended;
        Editor.Template.UseEdgeMatch = _lastAdvice.RecommendEdgeMatch;
        ApplyTeachLocks(_lastAdvice);
        ApplyTeachGeometry(_lastAdvice);
        ApplyDetectionGates(_lastAdvice);
        if (Editor.Template.ExpectedCount == 0)
            Editor.Template.ExpectedCount = 1;
        if (_lastAdvice.SuggestedFeatureRoi is { } roi &&
            TemplateOptions.UsesFeatureTeachRoi(_lastAdvice.Recommended))
            _host.ApplySuggestedFeatureRoi(roi);
        _host.RefreshEditorBindings();
        CanApplyRefineAdvice = false;
        _host.Message = $"已采用推荐精修：{SegmentRefineAdvisor.MethodLabel(_lastAdvice.Recommended)}（试触发已用编辑器，保存后上产线）";
    }

    private VisionImage MaybeUndistort(string cameraId, VisionImage source, out VisionImage? undistorted)
    {
        undistorted = null;
        if (string.IsNullOrEmpty(Editor.StationId) || !_calibration.HasPolynomial(Editor.StationId))
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
}
