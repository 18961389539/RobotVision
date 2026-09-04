using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using RobotVision.Teach;
using RobotVision.JlVision;

namespace RobotVision.Hosting;

/// <summary>配方试触发与模板示教编排（OpenCV/推理在此层释放，WPF 只负责展示）。</summary>
public sealed class RecipeTestService(
    VisionService vision,
    ICameraRuntime cameras,
    IModelRuntime models,
    ICalibrationRuntime calibration,
    ILightingRuntime lighting) : IRecipeTestService
{
    public async Task<RecipePreviewRunResult> RunPreviewAsync(
        RecipePreviewRequest request, CancellationToken ct = default)
    {
        var clone = request.Recipe.Clone();
        var snapshotName = RecipeLoader.IsValidRecipeName(clone.Name) ? clone.Name : "preview";
        clone.Name = snapshotName;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(5000, request.TimeoutMs)));

        TcpClientPose? pose = request.IncludeTriggerPose
            ? new TcpClientPose(request.TriggerPoseX, request.TriggerPoseY, request.TriggerPoseRz)
            : null;

        var preview = await vision.RunPreviewAsync(clone, pose, cts.Token).ConfigureAwait(false);
        var hint = ExtractRefineQualityHint(preview.Frame?.PixelPoses);
        return new RecipePreviewRunResult(preview.Result, preview.Frame, hint);
    }

    public async Task<RecipeTeachTemplateResult> TeachTemplateAsync(
        RecipeTeachTemplateRequest request, CancellationToken ct = default)
    {
        using var lightingScope = lighting.Apply(request.LightControllerId, request.Lighting);
        if (lightingScope.StabilizeDelayMs > 0)
            await Task.Delay(lightingScope.StabilizeDelayMs, ct).ConfigureAwait(false);

        try
        {
            return await Task.Run(() => GrabTeachShot(request), ct).ConfigureAwait(false);
        }
        finally
        {
            lightingScope.Dispose();
        }
    }

    private static string? ExtractRefineQualityHint(IReadOnlyList<PixelPose>? poses)
    {
        if (poses is null || poses.Count == 0)
            return null;
        for (var i = 0; i < poses.Count; i++)
        {
            var note = poses[i].Overlay?.RefineQualityNote;
            if (!string.IsNullOrEmpty(note))
                return note;
        }

        return null;
    }

    private RecipeTeachTemplateResult GrabTeachShot(RecipeTeachTemplateRequest request)
    {
        var editor = request.Recipe;
        var playbookPrior = ScenePlaybook.Merge(
            ScenePlaybook.FromTemplate(editor.Template), request.PlaybookPrior);

        using var grabbed = cameras.GrabForTeach(request.CameraId);
        using var image = RecipeEditorImagePrep.PrepareInferenceImage(calibration, editor, grabbed.Image);
        using var roiOwned = RoiHelper.CropToVisionImage(image, editor.Roi, out var ox, out var oy);
        var roiView = roiOwned ?? image;
        var imgW = image.Width;
        var imgH = image.Height;
        var templateOpts = editor.Template;
        var featureRoi = templateOpts?.Roi;
        var wantFeature = featureRoi is not null
            && TemplateOptions.UsesFeatureTeachRoi(templateOpts!.RefineMethod)
            && FeatureRoiAdvisor.IsDrawable(featureRoi, imgW, imgH);

        var session = models.Open(editor.Models[0], InferenceTask.Segmentation);
        var results = session.Run(y => y.RunSegmentation(
            roiView, editor.Confidence, editor.Segmentation.PixelConfidence, editor.Iou));

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
            var crop = MaskTemplateHostingOps.UprightCrop(roiMat, points, JlTemplateIo.CropMarginRatio);
            using (crop.Upright)
            {
                Mat templateMat = crop.Upright;
                var ownedFeature = false;
                if (useFeatureCrop)
                {
                    try
                    {
                        templateMat = MaskTemplateHostingOps.CropUprightBySourceRect(
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
                    var advice = SegmentRefineAdvisor.Analyze(new SegmentRefineAdvisor.TeachAnalyzeRequest(roiMat, points)
                    {
                        Upright = crop.Upright,
                        BitPackedMask = seg.BitPackedMask,
                        MaskWidth = box.Width,
                        MaskHeight = box.Height,
                        Template = editor.Template,
                        TemplateImage = templateMat,
                        FullImageWidth = imgW,
                        FullImageHeight = imgH,
                        OriginX = ox,
                        OriginY = oy,
                        InstanceConfidence = seg.Confidence,
                        BoxConfidence = editor.Confidence,
                        PixelConfidence = editor.Segmentation.PixelConfidence,
                        Task = ScenePlaybook.FromRecipe(editor),
                        Prior = playbookPrior,
                    });
                    using var owned = templateMat.Clone();
                    var b64 = MaskTemplateHostingOps.EncodeTemplatePng(owned);
                    return new RecipeTeachTemplateResult(
                        b64, advice, featureFallback, owned.Width, owned.Height);
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

    public async Task<RecipeTeachCropResult> TeachCropAsync(
        RecipeTeachCropRequest request, CancellationToken ct = default)
    {
        using var lightingScope = lighting.Apply(request.LightControllerId, request.Lighting);
        if (lightingScope.StabilizeDelayMs > 0)
            await Task.Delay(lightingScope.StabilizeDelayMs, ct).ConfigureAwait(false);

        try
        {
            return await Task.Run(() => GrabTeachCrop(request), ct).ConfigureAwait(false);
        }
        finally
        {
            lightingScope.Dispose();
        }
    }

    private RecipeTeachCropResult GrabTeachCrop(RecipeTeachCropRequest request)
    {
        using var grabbed = cameras.GrabForTeach(request.CameraId);
        using var image = RecipeEditorImagePrep.PrepareInferenceImage(calibration, request.Recipe, grabbed.Image);
        using var mat = VisionImageMat.AsMat(image);
        using var cropView = RoiHelper.Crop(mat, request.Crop, out _, out _);
        if (cropView.Width < 8 || cropView.Height < 8)
            throw new InvalidOperationException("模板裁剪过小（请把模板框画大一点，至少 8×8 px）");

        using var owned = cropView.Clone();
        var b64 = MaskTemplateHostingOps.EncodeTemplatePng(owned);
        return new RecipeTeachCropResult(b64, owned.Width, owned.Height);
    }
}
