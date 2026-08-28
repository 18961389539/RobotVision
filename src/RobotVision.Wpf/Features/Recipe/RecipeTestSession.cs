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

    /// <summary>测试触发的一次性快照捕获标志。不能用 IsBusy 判断（见原 RecipeViewModel 注释）。</summary>
    private string? _awaitSnapshotFor;

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

    public void EndSnapshotAwait()
    {
        Interlocked.Exchange(ref _awaitSnapshotFor, null);
        _vision.FrameProcessed -= OnTestFrameProcessed;
    }

    private bool CanTeachTemplate =>
        !_host.IsBusy &&
        Editor.AngleMode == AngleMode.MaskTemplate &&
        Editor.Template.RefineMethod == SegmentRefineMethod.Template;

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

            var shots = new List<TeachShot>();
            Exception? lastFail = null;
            for (var i = 0; i < TeachTemplateFusion.DefaultFrameCount; i++)
            {
                if (i > 0)
                    await Task.Delay(TeachTemplateFusion.GrabGapMs);
                _host.Message =
                    $"示教模板取图中 · {cameraId}（{i + 1}/{TeachTemplateFusion.DefaultFrameCount}）…";
                try
                {
                    shots.Add(await Task.Run(() => GrabTeachShot(cameraId)));
                }
                catch (Exception ex)
                {
                    lastFail = ex;
                }
            }

            lightingScope.Dispose();
            if (shots.Count == 0)
                throw lastFail ?? new InvalidOperationException("分割未检出有效目标，无法示教");

            try
            {
                var (b64, w, h, advice, fused) = FuseTeachShots(shots);
                Editor.Template.TemplateImageBase64 = b64;
                ApplyTeachLocks(advice);
                ApplyTeachGeometry(advice);
                ApplyDetectionGates(advice);
                if (Editor.Template.ExpectedCount == 0)
                    Editor.Template.ExpectedCount = 1;
                _host.NotifyEditorMutated();
                SetAdvice(advice);
                var fuseHint = fused >= 2 ? $"{fused} 帧中位融合 " : "";
                _host.Message = Editor.Template.Roi is not null
                    ? $"模板已示教（{fuseHint}特征 {w}×{h}px）· 极性/阈值已写入编辑器，保存后上产线"
                    : $"模板已示教（{fuseHint}{w}×{h}px）· 极性/阈值已写入编辑器，保存后上产线";
            }
            finally
            {
                foreach (var s in shots)
                    s.Dispose();
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
        _awaitSnapshotFor = snapshotName;
        _vision.FrameProcessed += OnTestFrameProcessed;
        try
        {
            var hint = _host.HasUnsavedChanges ? "编辑器（未保存）" : snapshotName;
            _host.Message = $"测试触发中：{hint} …";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(500, _tcp.TimeoutMs)));
            TcpClientPose? pose = IncludeTriggerPose
                ? new TcpClientPose(TriggerPoseX, TriggerPoseY, TriggerPoseRz)
                : null;
            var result = await _vision.RunPreviewAsync(clone, pose, cts.Token);
            LastPreview = result;
            await AwaitTestSnapshotAsync(TimeSpan.FromSeconds(3));
            if (result.Ok)
                _host.Message = $"测试通过：{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms"
                    + (_host.HasUnsavedChanges ? "（编辑器，未保存不上产线）" : "");
            else if (result.ErrorCode == VisionErrorCode.RefineFailed)
                _host.Message = $"测试失败：ERR 1019 精修未过门 · {result.Message}";
            else
                _host.Message = $"测试失败：ERR {result.ErrorCode} · {result.Message}";
        }
        catch (Exception ex)
        {
            EndSnapshotAwait();
            _host.Message = $"测试异常：{ex.Message}";
        }
        finally
        {
            _host.IsBusy = false;
        }
    }

    private sealed class TeachShot(
        Mat template, double area, double aspect, double instanceConf, SegmentRefineAdvice advice) : IDisposable
    {
        public Mat Template { get; } = template;
        public double Area { get; } = area;
        public double Aspect { get; } = aspect;
        public double InstanceConf { get; } = instanceConf;
        public SegmentRefineAdvice Advice { get; } = advice;

        public void Dispose() => Template.Dispose();
    }

    private (string B64, int W, int H, SegmentRefineAdvice Advice, int Fused) FuseTeachShots(
        IReadOnlyList<TeachShot> shots)
    {
        var best = shots.OrderByDescending(s => s.InstanceConf).First();
        var same = shots.Count >= 2 &&
                   shots.Skip(1).All(s => TeachTemplateFusion.SameTarget(shots[0].Template, s.Template));
        using var blended = same
            ? TeachTemplateFusion.Blend(shots.Select(s => s.Template).ToList())
            : best.Template.Clone();
        var advice = best.Advice;
        if (same && shots.Count >= 2)
        {
            advice = advice with
            {
                Aspect = TeachTemplateFusion.Median(shots.Select(s => s.Aspect).ToList()),
                TeachAreaPx = TeachTemplateFusion.Median(shots.Select(s => s.Area).ToList()),
            };
        }

        return (MaskTemplateMatcher.EncodeTemplatePng(blended), blended.Width, blended.Height, advice,
            same ? shots.Count : 1);
    }

    private TeachShot GrabTeachShot(string cameraId)
    {
        using var grabbed = _cameras.Grab(cameraId);
        VisionImage image = grabbed.Image;
        VisionImage? undistorted = null;
        if (string.IsNullOrEmpty(Editor.StationId) || !_calibration.HasPolynomial(Editor.StationId))
        {
            try
            {
                undistorted = _calibration.Undistort(cameraId, grabbed.Image);
                image = undistorted;
            }
            catch (VisionException)
            {
                image = grabbed.Image;
            }
        }

        using var undistortedScope = undistorted;
        using var roiOwned = RoiHelper.CropToVisionImage(image, Editor.Roi, out var ox, out var oy);
        var roiView = roiOwned ?? image;
        var imgW = image.Width;
        var imgH = image.Height;
        var featureRoi = Editor.Template?.Roi;
        double? featureCx = featureRoi is { } fr
            ? (fr.X + fr.Width / 2.0) * imgW - ox
            : null;
        double? featureCy = featureRoi is { } fr2
            ? (fr2.Y + fr2.Height / 2.0) * imgH - oy
            : null;

        var session = _models.Open(Editor.Models[0], InferenceTask.Segmentation);
        var results = session.Run(y => y.RunSegmentation(
            roiView, Editor.Confidence, Editor.Segmentation.PixelConfidence, Editor.Iou));

        var valid = results.Where(s =>
            (double)s.Box.Width * s.Box.Height >= 400 && s.ContourLocal.Count >= 4).ToList();
        if (valid.Count == 0)
            throw new InvalidOperationException("分割未检出有效目标，无法示教（请确认模型/阈值/画面内有目标）");

        IReadOnlyList<InstanceSegmentation> candidates = valid;
        if (featureCx is { } fcx && featureCy is { } fcy)
        {
            var inside = valid.Where(s =>
                fcx >= s.Box.Left && fcx < s.Box.Right &&
                fcy >= s.Box.Top && fcy < s.Box.Bottom).ToList();
            if (inside.Count == 0)
                throw new InvalidOperationException(
                    "特征 ROI 中心未落在分割目标内（请把特征框画在目标上，或检查检测区域/模型）");
            candidates = inside;
        }

        foreach (var seg in candidates.OrderByDescending(s => s.Confidence))
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
                Mat templateMat;
                if (featureRoi is null)
                    templateMat = crop.Upright;
                else
                    templateMat = MaskTemplateMatcher.CropUprightBySourceRect(
                        crop,
                        featureRoi.X * imgW - ox,
                        featureRoi.Y * imgH - oy,
                        featureRoi.Width * imgW,
                        featureRoi.Height * imgH);
                try
                {
                    var advice = SegmentRefineAdvisor.Analyze(
                        roiMat, points, crop.Upright, seg.BitPackedMask, box.Width, box.Height,
                        Editor.Template, templateMat, imgW, imgH, ox, oy,
                        instanceConfidence: seg.Confidence,
                        boxConfidence: Editor.Confidence,
                        pixelConfidence: Editor.Segmentation.PixelConfidence);
                    var owned = templateMat.Clone();
                    var area = Cv2.ContourArea(points);
                    var housing = MaskHousing.Fit(points);
                    var aspect = housing.LongLen / Math.Max(1.0, housing.ShortLen);
                    return new TeachShot(owned, area, aspect, seg.Confidence, advice);
                }
                finally
                {
                    if (!ReferenceEquals(templateMat, crop.Upright))
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
            $"推荐：{SegmentRefineAdvisor.MethodLabel(advice.Recommended)}（{orient}，轴比 {advice.Aspect:0.0}，0/180 分差 {advice.Separability:0.00}）。{advice.Summary}";
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
         advice.Recommended == SegmentRefineMethod.Template &&
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
            _lastAdvice.Recommended == SegmentRefineMethod.Template)
            _host.ApplySuggestedFeatureRoi(roi);
        _host.RefreshEditorBindings();
        CanApplyRefineAdvice = false;
        _host.Message = $"已采用推荐精修：{SegmentRefineAdvisor.MethodLabel(_lastAdvice.Recommended)}（试触发已用编辑器，保存后上产线）";
    }

    private void OnTestFrameProcessed(VisionFrameSnapshot snapshot)
    {
        var image = snapshot.UndistortedImage;
        try
        {
            var expected = Volatile.Read(ref _awaitSnapshotFor);
            if (expected is null || snapshot.RecipeName != expected)
                return;
            if (Interlocked.Exchange(ref _awaitSnapshotFor, null) != expected)
                return;
            _vision.FrameProcessed -= OnTestFrameProcessed;

            SegmentRefineAdvice? advice = null;
            if (Editor.AngleMode == AngleMode.MaskTemplate &&
                snapshot.Poses.Count > 0 &&
                snapshot.Poses[0].Overlay?.Contour is { Count: >= 4 } contour)
            {
                var pts = new Point2f[contour.Count];
                for (var i = 0; i < contour.Count; i++)
                    pts[i] = new Point2f((float)contour[i].X, (float)contour[i].Y);
                using var mat = VisionImageCv.AsMat(image);
                Mat? templateMat = null;
                try
                {
                    if (!string.IsNullOrEmpty(Editor.Template.TemplateImageBase64))
                        templateMat = MaskTemplateMatcher.DecodeTemplatePng(Editor.Template.TemplateImageBase64);
                    advice = SegmentRefineAdvisor.Analyze(
                        mat, pts,
                        bitPackedMask: snapshot.Poses[0].Overlay?.BitPackedMask,
                        maskWidth: snapshot.Poses[0].Overlay?.MaskWidth ?? 0,
                        maskHeight: snapshot.Poses[0].Overlay?.MaskHeight ?? 0,
                        template: Editor.Template, templateImage: templateMat,
                        fullImageWidth: image.Width, fullImageHeight: image.Height,
                        instanceConfidence: snapshot.Poses[0].SegmentScore ?? snapshot.Poses[0].Score,
                        boxConfidence: Editor.Confidence,
                        pixelConfidence: Editor.Segmentation.PixelConfidence);
                }
                catch (Exception)
                {
                    // 推荐失败不影响叠加
                }
                finally
                {
                    templateMat?.Dispose();
                }
            }

            OverlayDrawer.DrawPoses(image, snapshot.Poses, drawDebug: true);
            using (var drawn = VisionImageCv.AsMat(image))
            {
                if (Editor.Template.Roi is { } feature)
                    OverlayDrawer.DrawNormalizedRoi(drawn, feature, "特征");
                if (advice?.SuggestedFeatureRoi is { } suggested &&
                    !SameRoi(suggested, Editor.Template.Roi))
                    OverlayDrawer.DrawNormalizedRoi(drawn, suggested, "建议", Scalar.Gold);
            }
            var source = ImageConverter.ToBitmapSource(image);
            UiDispatch.Begin(() =>
            {
                ResultImage = source;
                if (advice is not null)
                    SetAdvice(advice);
            });
        }
        catch (Exception)
        {
            // 绘制失败不影响管线
        }
        finally
        {
            image.Dispose();
        }
    }

    private async Task AwaitTestSnapshotAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Volatile.Read(ref _awaitSnapshotFor) is not null && DateTime.UtcNow < deadline)
            await Task.Delay(30);
        if (Volatile.Read(ref _awaitSnapshotFor) is not null)
            EndSnapshotAwait();
    }

    private static bool SameRoi(Roi a, Roi? b) =>
        b is not null &&
        Math.Abs(a.X - b.X) < 1e-4 &&
        Math.Abs(a.Y - b.Y) < 1e-4 &&
        Math.Abs(a.Width - b.Width) < 1e-4 &&
        Math.Abs(a.Height - b.Height) < 1e-4;
}
