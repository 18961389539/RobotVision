using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Teach;

namespace RobotVision.Hosting;

public sealed class RecipeSetupAnalysisService(
    ICameraRuntime cameras,
    IModelRuntime models,
    ICalibrationRuntime calibration) : IRecipeSetupAnalysisService
{
    public RecipeSetupAnalysisResult AnalyzeGrab(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null)
    {
        ct.ThrowIfCancellationRequested();
        using var grabbed = cameras.Grab(request.CameraId, ct);
        var image = MaybeUndistort(request.Editor, request.CameraId, grabbed.Image, out var undistorted);
        using var undistortedScope = undistorted;
        return AnalyzeImage(request, image, totalHint: 1, ct: ct, progress: progress);
    }

    public RecipeSetupAnalysisResult AnalyzePlayback(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null)
    {
        var files = cameras.GetPlaybackFiles(request.CameraId);
        if (files is null or { Count: 0 })
        {
            return new RecipeSetupAnalysisResult
            {
                Message = "回放文件列表为空。",
                Total = 0,
            };
        }

        var editor = request.Editor;
        Mat? templateMat = null;
        if (!string.IsNullOrEmpty(editor.Template.TemplateImageBase64))
            templateMat = MaskTemplateMatcher.DecodeTemplatePng(editor.Template.TemplateImageBase64);
        using var templateScope = templateMat;
        using var teachCache = SegmentRefineBakeOff.TeachCache.TryCreate(templateMat);

        ModelSession? session = null;
        if (editor.Models.Count > 0 && !string.IsNullOrWhiteSpace(editor.Models[0]))
            session = models.Open(editor.Models[0], InferenceTask.Segmentation);

        var perFrame = new List<IReadOnlyList<SegmentRefineCandidate>>(files.Count);
        SceneDescriptor? bestScene = null;
        HousingEdgePolarity edge = default;
        TabPolarityLock tab = default;
        Roi? feature = null;
        IReadOnlyList<FeatureRoiCandidate> featureRanks = [];
        BgraImageBuffer? previewBase = null;
        BgraImageBuffer? matchPreviewBase = null;
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
        var expected = request.Constraints.ExpectedCount;
        var votes = new Dictionary<SceneKind, int>();

        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            Report(progress, $"回放打分 {i + 1}/{files.Count} …");
            try
            {
                using var decoded = FileCamera.DecodeFile(files[i]);
                using var source = VisionImageMat.FromMat(decoded, ownsMat: false);
                var image = MaybeUndistort(editor, request.CameraId, source, out var undistorted);
                using var undistortedScope = undistorted;
                using var one = AnalyzeImage(request, image, files.Count, session, templateMat, teachCache,
                    keepPreview: true, ct: ct, progress: progress);
                if (one.Scene is null)
                {
                    perFrame.Add([]);
                    if (previewBase is null)
                    {
                        previewBase = one.Preview;
                        one.Preview = null;
                    }

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
                    previewBase = one.Preview;
                    one.Preview = null;
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
                    matchPreviewBase = one.Preview;
                    one.Preview = null;
                    matchLocks = one.Locks;
                }
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
            previewBase = matchPreviewBase;
            matchPreviewBase = null;
            locks = matchLocks;
        }

        var lastCount = instanceCounts.Count > 0 ? instanceCounts.Max() : 0;
        var aggregated = SegmentRefineBakeOff.Aggregate(perFrame);
        return new RecipeSetupAnalysisResult
        {
            Scene = bestScene,
            BakeOff = aggregated,
            Edge = edge,
            Tab = tab,
            FeatureRoi = feature,
            FeatureRanks = featureRanks,
            Preview = previewBase,
            Detected = detected,
            Total = files.Count,
            Message = detected == 0
                ? "回放未检出。请检查检测 ROI / 模型 / 画面内是否有目标，或勾选双 BLOB 后按任务继续。"
                : $"回放 {detected}/{files.Count} 检出。极性/特征框优先取件数匹配且置信最高的帧；赛马分已含角度稳定性（角σ）。"
                  + (unstable ? " 整夹件数不稳，请核对漏检。" : ""),
            Confidence = bestMatchConf > 0 ? bestMatchConf : (bestConf > 0 ? bestConf : 0),
            Locks = locks,
            InstanceCount = lastCount,
            CountUnstable = unstable,
            SceneVotes = votes.Count == 0 ? null : votes,
            PerFrame = perFrame,
            InstanceCounts = instanceCounts,
        };
    }

    private RecipeSetupAnalysisResult AnalyzeImage(
        RecipeSetupAnalysisRequest request,
        VisionImage image,
        int totalHint,
        ModelSession? session = null,
        Mat? templateMat = null,
        SegmentRefineBakeOff.TeachCache? teachCache = null,
        bool keepPreview = true,
        CancellationToken ct = default,
        IProgress<string>? progress = null)
    {
        ct.ThrowIfCancellationRequested();
        var editor = request.Editor;
        var prior = ScenePlaybook.Merge(ScenePlaybook.FromTemplate(editor.Template), request.Prior);
        using var roiOwned = RoiHelper.CropToVisionImage(image, editor.Roi, out var ox, out var oy);
        var roiView = roiOwned ?? image;
        using var roiMat = VisionImageMat.AsMat(roiView);

        if (request.UseBlobsWithoutModel)
        {
            var blobPreview = keepPreview ? RenderPreviewBase(image, editor.Roi) : null;
            return new RecipeSetupAnalysisResult
            {
                BakeOff = [],
                Total = totalHint,
                Message = "已按双 BLOB 任务推荐，未跑分割。",
                Preview = blobPreview,
            };
        }

        if (editor.Models.Count == 0 || string.IsNullOrWhiteSpace(editor.Models[0]))
        {
            var noModelPreview = keepPreview ? RenderPreviewBase(image, editor.Roi) : null;
            return new RecipeSetupAnalysisResult
            {
                BakeOff = [],
                Total = totalHint,
                Message = "未选分割模型。可勾选双 BLOB，或先在配方里选模型再分析。",
                Preview = noModelPreview,
            };
        }

        session ??= models.Open(editor.Models[0], InferenceTask.Segmentation);
        var results = session.Run(y => y.RunSegmentation(
            roiView, editor.Confidence, editor.Segmentation.PixelConfidence, editor.Iou));
        var valid = results.Where(s =>
            (double)s.Box.Width * s.Box.Height >= 400 && s.ContourLocal.Count >= 4).ToList();
        if (valid.Count == 0)
        {
            var missPreview = keepPreview ? RenderPreviewBase(image, editor.Roi) : null;
            return new RecipeSetupAnalysisResult
            {
                BakeOff = [],
                Total = totalHint,
                Message = "分割未检出有效目标。请缩小检测 ROI、检查模型/阈值，或确认画面里有件。",
                Preview = missPreview,
            };
        }

        var expected = request.Constraints.ExpectedCount;
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
            task: request.Constraints,
            teachCache: teachCache,
            prior: prior);
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
                feature = ranks.Count > 0 ? ranks[0].Roi : feature;
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
        return new RecipeSetupAnalysisResult
        {
            Scene = scene,
            BakeOff = bakeoff,
            Edge = edge,
            Tab = tab,
            FeatureRoi = feature,
            FeatureRanks = ranks,
            Preview = previewBase,
            Detected = 1,
            Total = totalHint,
            Message = msg,
            Confidence = seg.Confidence,
            Locks = advice,
            InstanceCount = valid.Count,
            CountUnstable = countUnstable,
            SceneVotes = votes,
            PerFrame = [bakeoff],
            InstanceCounts = [valid.Count],
        };
    }

    private static BgraImageBuffer RenderPreviewBase(
        VisionImage image,
        Roi? detection,
        Mat? roiMat = null,
        IReadOnlyList<Point2f>? champion = null,
        double ox = 0,
        double oy = 0,
        List<InstanceSegmentation>? instances = null,
        TemplateOptions? template = null)
    {
        using var mat = VisionImageMat.AsMat(image);
        var drawn = mat.Clone();
        if (instances is { Count: > 0 })
        {
            var champ = instances.MaxBy(x => x.Confidence) ?? instances[0];
            foreach (var s in instances)
            {
                var pts = new Point[s.ContourLocal.Count];
                for (var k = 0; k < s.ContourLocal.Count; k++)
                {
                    var p = s.ContourLocal[k];
                    pts[k] = new Point(
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
            var pts = champion.Select(p => new Point(
                (int)Math.Round(p.X + ox), (int)Math.Round(p.Y + oy))).ToArray();
            Cv2.Polylines(drawn, [pts], true, Scalar.Lime, 2, LineTypes.AntiAlias);
        }

        if (roiMat is not null && champion is { Count: >= 4 })
        {
            var caliper = MaskCaliperTab.TryRefine(roiMat, champion, CaliperRefineOptions.From(template));
            DrawCaliperViz(drawn, caliper.Viz, ox, oy);
        }

        if (detection is { } det)
            DrawNormalizedRoi(drawn, det, Scalar.Lime);
        return BgraImageBuffer.FromBgrMat(drawn);
    }

    private static void DrawNormalizedRoi(Mat image, Roi roi, Scalar color)
    {
        var x = (int)Math.Round(roi.X * image.Width);
        var y = (int)Math.Round(roi.Y * image.Height);
        var w = Math.Max(1, (int)Math.Round(roi.Width * image.Width));
        var h = Math.Max(1, (int)Math.Round(roi.Height * image.Height));
        Cv2.Rectangle(image, new Rect(x, y, w, h), color, 2, LineTypes.AntiAlias);
    }

    private static void DrawCaliperViz(Mat drawn, MaskCaliperTab.CaliperViz viz, double ox, double oy)
    {
        Point Map(Point2d p) =>
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

    private VisionImage MaybeUndistort(
        RecipeConfig editor, string cameraId, VisionImage source, out VisionImage? undistorted)
    {
        undistorted = null;
        if (string.IsNullOrEmpty(editor.StationId) || !calibration.HasPolynomial(editor.StationId))
        {
            try
            {
                undistorted = calibration.Undistort(cameraId, source);
                return undistorted;
            }
            catch (VisionException)
            {
                return source;
            }
        }

        return source;
    }

    private static void Report(IProgress<string>? progress, string text) =>
        progress?.Report(text);
}
