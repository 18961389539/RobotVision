using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;
internal sealed partial class RecipeSetupWizardViewModel
{
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
            OnPropertyChanged(nameof(ViewerImage));
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
                using var source = VisionImageMat.FromMat(decoded, ownsMat: false);
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
        using var roiMat = VisionImageMat.AsMat(roiView);

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
