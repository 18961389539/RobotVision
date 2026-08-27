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

            var (b64, w, h) = await Task.Run(() => TeachOnGrab(cameraId));
            lightingScope.Dispose();

            Editor.Template.TemplateImageBase64 = b64;
            _host.NotifyEditorMutated();
            _host.Message = Editor.Template.Roi is not null
                ? $"模板已示教（特征 {w}×{h}px）· 保存配方后生效"
                : $"模板已示教（{w}×{h}px）· 保存配方后生效";
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
        if (string.IsNullOrEmpty(_host.OriginalName))
            return;

        if (_host.HasUnsavedChanges &&
            System.Windows.MessageBox.Show("有未保存的修改：测试触发仍用磁盘上的旧配方。继续？",
                "未保存修改", System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
            return;

        _host.IsBusy = true;
        _host.OnTestStarting();
        _awaitSnapshotFor = _host.OriginalName;
        _vision.FrameProcessed += OnTestFrameProcessed;
        try
        {
            _host.Message = $"测试触发中：{_host.OriginalName} …";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(500, _tcp.TimeoutMs)));
            TcpClientPose? pose = IncludeTriggerPose
                ? new TcpClientPose(TriggerPoseX, TriggerPoseY, TriggerPoseRz)
                : null;
            var result = pose is null
                ? await _vision.RunAsync(_host.OriginalName, cts.Token)
                : await _vision.RunAsync(_host.OriginalName, pose, cts.Token);
            await AwaitTestSnapshotAsync(TimeSpan.FromSeconds(3));
            if (result.Ok)
                _host.Message = $"测试通过：{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms";
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

    private (string B64, int W, int H) TeachOnGrab(string cameraId)
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
                if (featureRoi is null)
                {
                    return (MaskTemplateMatcher.EncodeTemplatePng(crop.Upright),
                        crop.Upright.Width, crop.Upright.Height);
                }

                using var feature = MaskTemplateMatcher.CropUprightBySourceRect(
                    crop,
                    featureRoi.X * imgW - ox,
                    featureRoi.Y * imgH - oy,
                    featureRoi.Width * imgW,
                    featureRoi.Height * imgH);
                return (MaskTemplateMatcher.EncodeTemplatePng(feature),
                    feature.Width, feature.Height);
            }
        }
        throw new InvalidOperationException("分割未检出有效目标，无法示教（请确认模型/阈值/画面内有目标）");
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

            OverlayDrawer.DrawPoses(image, snapshot.Poses, drawDebug: true);
            var source = ImageConverter.ToBitmapSource(image);
            UiDispatch.Begin(() => ResultImage = source);
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
}
