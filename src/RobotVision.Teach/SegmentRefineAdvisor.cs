using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.JlVision;

namespace RobotVision.Teach;

/// <summary>示教/试触发时的精修方法推荐（不进 TRIGGER 热路径）。</summary>
public sealed record SegmentRefineAdvice(
    SegmentRefineMethod Recommended,
    bool RecommendEdgeMatch,
    bool CanResolveOrientation,
    double Aspect,
    double TextureEntropy,
    double Separability,
    double HoleAreaPx,
    double ProtrusionPx,
    string Summary)
{
    public Roi? SuggestedFeatureRoi { get; init; }
    public HousingEdgePolarity EdgePolarity { get; init; }
    public TabPolarityLock TabPolarity { get; init; }
    public double TeachPeakScore { get; init; }
    public double SuggestedMatchThreshold { get; init; }
    public double TeachAreaPx { get; init; }
    public double SuggestedConfidence { get; init; }
    public double SuggestedPixelConfidence { get; init; }
    public IReadOnlyList<SegmentRefineCandidate> Candidates { get; init; } = [];
    public SceneDescriptor? Scene { get; init; }
}

/// <summary>
/// 根据掩码几何、纹理熵、自转 180° NCC 可分性，再叠加同一帧四路赛马，推荐精修方法。
/// 只在配方页示教时生成短提醒；完整推荐与写入编辑器在配方向导。
/// </summary>
public static class SegmentRefineAdvisor
{
    /// <summary>
    /// <see cref="Analyze"/> 的输入上下文：把原先 17 个平铺参数收敛成一个对象。
    /// <see cref="Bgr"/>/<see cref="Contour"/> 必填，其余按旧默认值可省。字段命名与旧参数一一对应。
    /// </summary>
    public sealed record TeachAnalyzeRequest(Mat Bgr, IReadOnlyList<Point2f> Contour)
    {
        public Mat? Upright { get; init; }
        public byte[]? BitPackedMask { get; init; }
        public int MaskWidth { get; init; }
        public int MaskHeight { get; init; }
        public TemplateOptions? Template { get; init; }
        public Mat? TemplateImage { get; init; }
        public int FullImageWidth { get; init; }
        public int FullImageHeight { get; init; }
        public double OriginX { get; init; }
        public double OriginY { get; init; }
        public double InstanceConfidence { get; init; }
        public double BoxConfidence { get; init; } = DetectionThresholdAdvisor.DefaultBoxConfidence;
        public double PixelConfidence { get; init; } = DetectionThresholdAdvisor.DefaultPixelConfidence;
        public TaskConstraints? Task { get; init; }
        public SegmentRefineBakeOff.TeachCache? TeachCache { get; init; }
        public RecipePrior? Prior { get; init; }
    }

    /// <summary>简写重载：仅给图与轮廓（其余走默认），便于单帧快速分析。</summary>
    public static SegmentRefineAdvice Analyze(Mat bgr, IReadOnlyList<Point2f> contour) =>
        Analyze(new TeachAnalyzeRequest(bgr, contour));

    public static SegmentRefineAdvice Analyze(TeachAnalyzeRequest req)
    {
        var (bgr, contour) = (req.Bgr, req.Contour);
        var upright = req.Upright;
        var (bitPackedMask, maskWidth, maskHeight) = (req.BitPackedMask, req.MaskWidth, req.MaskHeight);
        var (template, templateImage) = (req.Template, req.TemplateImage);
        var (fullImageWidth, fullImageHeight) = (req.FullImageWidth, req.FullImageHeight);
        var (originX, originY) = (req.OriginX, req.OriginY);
        var instanceConfidence = req.InstanceConfidence;
        var (boxConfidence, pixelConfidence) = (req.BoxConfidence, req.PixelConfidence);
        var teachCache = req.TeachCache;
        var prior = req.Prior;
        TaskConstraints? task = req.Task;

        var scene = ScenePlaybook.Describe(bgr, contour, bitPackedMask, maskWidth, maskHeight);
        var aspect = scene.Aspect;
        var protrusion = scene.ProtrusionPx;
        var holeOk = scene.HoleOk;
        var holeArea = holeOk ? 1.0 : 0.0;
        var teachAreaPx = scene.TeachAreaPx;
        task ??= new TaskConstraints();
        prior = ScenePlaybook.Merge(ScenePlaybook.FromTemplate(template), prior);

        Mat? ownedUpright = null;
        var view = upright;
        JlUprightCrop? crop = null;
        if (view is null || view.Empty())
        {
            try
            {
                crop = JlTemplateIo.UprightCrop(bgr, contour, 0.05);
                ownedUpright = crop.Upright;
                view = ownedUpright;
            }
            catch (InvalidOperationException)
            {
                view = null;
            }
        }

        try
        {
            var entropy = scene.TextureEntropy;
            var separability = scene.Separability;
            var canOrient = scene.Separability >= TeachThresholds.SeparabilityOrientable || holeOk
                || protrusion > TeachThresholds.ProtrusionShortLenRatio * Math.Max(8, scene.ShortLenPx);

            var candidates = SegmentRefineBakeOff.Run(
                bgr, contour, bitPackedMask, maskWidth, maskHeight, templateImage, template, teachCache);
            var playbook = ScenePlaybook.Recommend(task, scene, candidates, prior);
            var method = ScenePlaybook.RefineMethodOf(playbook.Primary)
                         ?? PickHeuristic(holeOk, protrusion, scene.ShortLenPx, separability, aspect, entropy).Method;
            var edge = playbook.Primary.EdgeMatch && method == SegmentRefineMethod.Template;
            var raced = ScenePlaybook.PickWinnerForTask(candidates, task, scene, prior);

            var (edgePolarity, tabPolarity) = ScenePlaybook.InferPolarity(bgr, contour);
            var (teachPeak, peakSharp) = MeasureTeachPeak(view, templateImage, template);
            var suggestedTh = teachPeak >= 0.3
                ? TemplateOptions.MatchThresholdFromTeachPeak(teachPeak, peakSharp)
                : 0;
            var sugBox = DetectionThresholdAdvisor.SuggestBoxConfidence(instanceConfidence, boxConfidence);
            var sugPix = DetectionThresholdAdvisor.SuggestPixelConfidence(pixelConfidence);

            Roi? featureRoi = null;
            if (crop is not null && fullImageWidth > 0 && fullImageHeight > 0)
            {
                featureRoi = FeatureRoiAdvisor.Suggest(
                    crop, fullImageWidth, fullImageHeight, contour, originX, originY);
            }
            else if (view is not null && fullImageWidth > 0 && ownedUpright is null)
            {
                try
                {
                    var tmp = JlTemplateIo.UprightCrop(bgr, contour, 0.05);
                    using (tmp.Upright)
                    {
                        featureRoi = FeatureRoiAdvisor.Suggest(
                            tmp, fullImageWidth, fullImageHeight, contour, originX, originY);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            var why = playbook.Summary
                + TeachNarrator.RaceTable(candidates
                    .Select(c => TeachNarrator.RaceRow(MethodLabel(c.Method), c.Ok, c.Score))
                    .ToList());
            if (raced is not null && playbook.Primary.Refine == raced.Method)
                why += TeachNarrator.MeasuredWin(MethodLabel(method), raced.Note);

            if (method == SegmentRefineMethod.Template && !canOrient)
                why += TeachNarrator.TemplateNoOrientation(featureRoi is not null);

            if (suggestedTh > 0)
                why += TeachNarrator.TeachPeak(teachPeak, suggestedTh, peakSharp);

            if (Math.Abs(sugBox - boxConfidence) > 0.02)
                why += TeachNarrator.BoxConfidence(sugBox, instanceConfidence);
            if (Math.Abs(sugPix - pixelConfidence) > 0.02)
                why += TeachNarrator.PixelConfidence(sugPix);
            if (edgePolarity != HousingEdgePolarity.Auto)
                why += TeachNarrator.EdgeLocked(edgePolarity);
            if (tabPolarity != TabPolarityLock.Auto)
                why += TeachNarrator.TabLocked(tabPolarity);

            return new SegmentRefineAdvice(
                method, edge, canOrient, aspect, entropy, separability, holeArea, protrusion, why)
            {
                SuggestedFeatureRoi = featureRoi,
                EdgePolarity = edgePolarity,
                TabPolarity = tabPolarity,
                TeachPeakScore = teachPeak,
                SuggestedMatchThreshold = suggestedTh,
                TeachAreaPx = teachAreaPx,
                SuggestedConfidence = Math.Abs(sugBox - boxConfidence) > 0.02 ? sugBox : 0,
                SuggestedPixelConfidence = Math.Abs(sugPix - pixelConfidence) > 0.02 ? sugPix : 0,
                Candidates = candidates,
                Scene = scene,
            };
        }
        finally
        {
            ownedUpright?.Dispose();
        }
    }

    /// <summary>
    /// 用多帧汇总赛马覆盖单帧推荐。极性、阈值、特征框仍来自 <paramref name="seed"/>（示教帧或最高置信帧）。
    /// </summary>
    public static SegmentRefineAdvice OverlayBatch(
        SegmentRefineAdvice seed,
        IReadOnlyList<SegmentRefineCandidate> aggregated,
        int detected,
        int total,
        TaskConstraints? task = null,
        RecipePrior? prior = null)
    {
        task ??= new TaskConstraints();
        var playbook = ScenePlaybook.Recommend(task, seed.Scene, aggregated, prior);
        var raced = ScenePlaybook.PickWinnerForTask(aggregated, task, seed.Scene, prior);
        var method = ScenePlaybook.RefineMethodOf(playbook.Primary) ?? seed.Recommended;
        var edge = method == SegmentRefineMethod.Template && playbook.Primary.EdgeMatch;

        var why = $"回放 {detected}/{total} 检出。"
            + TeachNarrator.RaceTable(aggregated
                .Select(c => TeachNarrator.RaceRow(MethodLabel(c.Method), c.Ok, c.Score))
                .ToList());
        if (raced is not null && playbook.Primary.Refine == raced.Method)
            why += TeachNarrator.ReplayWin(MethodLabel(method), raced.Note);
        else
            why += $" {playbook.Summary}";

        return seed with
        {
            Recommended = method,
            RecommendEdgeMatch = edge,
            Summary = why,
            Candidates = aggregated,
        };
    }

    /// <summary>精修方法中文短名（转发到 <see cref="TeachNarrator.MethodLabel"/> 单一来源）。</summary>
    public static string MethodLabel(SegmentRefineMethod method) => TeachNarrator.MethodLabel(method);

    /// <summary>配方页短提醒：不含 playbook 长文，完整赛马在配方向导。</summary>
    public static string FormatBriefAdvice(SegmentRefineAdvice advice, SegmentRefineMethod currentMethod)
    {
        var recommended = MethodLabel(advice.Recommended);
        var currentLabel = MethodLabel(currentMethod);
        var curScore = FormatCandidateScore(advice, currentMethod);
        var recScore = FormatCandidateScore(advice, advice.Recommended);
        if (advice.Recommended != currentMethod)
        {
            return $"与示教分析不一致：推荐 {recommended}（{recScore}），当前 {currentLabel}（{curScore}）。"
                   + "完整赛马与场景说明请用配方向导。";
        }

        return $"示教分析建议调整参数（当前 {currentLabel} {curScore}）。请用配方向导查看并写入编辑器。";
    }

    /// <summary>精修详情页一行：仅当前方法赛马分 + 指向向导（方法不一致时）。</summary>
    public static string FormatMethodScoreHint(SegmentRefineAdvice advice, SegmentRefineMethod currentMethod)
    {
        var score = FormatCandidateScore(advice, currentMethod);
        if (score == "—")
            return "";
        if (advice.Recommended == currentMethod)
            return "";
        return $"本件 {MethodLabel(currentMethod)} {score}；推荐 {MethodLabel(advice.Recommended)}（{FormatCandidateScore(advice, advice.Recommended)}）。详见配方向导。";
    }

    private static string FormatCandidateScore(SegmentRefineAdvice advice, SegmentRefineMethod method)
    {
        foreach (var c in advice.Candidates)
        {
            if (c.Method == method && !c.Skipped)
                return FormattableString.Invariant($"{c.Score:0.00}");
        }

        return "—";
    }

    private readonly record struct HeuristicPick(SegmentRefineMethod Method, bool Edge, string Why);

    private static HeuristicPick PickHeuristic(
        bool holeOk, double protrusion, double shortLen, double separability, double aspect, double entropy)
    {
        if (holeOk)
            return new(SegmentRefineMethod.CentroidHoleLine, false,
                "掩码内有稳定孔/槽，推荐质心-内标连线（有头尾，免示教）。");
        if (protrusion > TeachThresholds.ProtrusionShortLenRatio * Math.Max(8, shortLen))
            return new(SegmentRefineMethod.CaliperTab, false,
                "短轴一侧有外伸，推荐卡尺长边+凸起极性（免示教，有方向）。");
        if (separability >= TeachThresholds.SeparabilityOrientable)
        {
            var edge = entropy is >= TeachThresholds.EdgeMatchEntropyLo and <= TeachThresholds.EdgeMatchEntropyHi;
            return new(SegmentRefineMethod.Template, edge, edge
                ? FormattableString.Invariant($"0°/180° 可分（分差 {separability:0.00}），推荐模板匹配并打开边缘图定角。")
                : FormattableString.Invariant($"0°/180° 可分（分差 {separability:0.00}），推荐模板匹配（需示教）。"));
        }
        if (aspect >= 1.7 && entropy < 4.2)
            return new(SegmentRefineMethod.LineFit, false,
                "弱纹理矩形且头尾难分，推荐直线拟合（无方向）。不要用灰度模板匹配。");
        if (aspect >= 1.7)
            return new(SegmentRefineMethod.CaliperTab, false,
                "壳体细长、头尾可分性弱，优先卡尺抓原图长边。");
        return new(SegmentRefineMethod.LineFit, false,
            "轴比接近 1 且无可分头尾，只能直线拟合给无向角。");
    }

    private static (double Score, double Sharpness) MeasureTeachPeak(
        Mat? upright, Mat? templateImage, TemplateOptions? options)
    {
        if (upright is null || upright.Empty() || templateImage is null || templateImage.Empty())
            return (0, 1);
        var range = Math.Clamp(options?.RefineRangeDeg ?? 5, 1, 45);
        try
        {
            using var grayTpl = JlImageConvert.ToGray(templateImage);
            using var model = JlNccRefine.CreateModel(grayTpl);
            using var scene = JlImageConvert.FromGrayMat(upright);
            var box = new[]
            {
                new Point2f(0, 0),
                new Point2f(upright.Width, 0),
                new Point2f(upright.Width, upright.Height),
                new Point2f(0, upright.Height),
            };
            var hit = JlNccRefine.TryRefine(scene, box, model, range, 0.01, JlFindOptions.ProductDefault);
            return (hit.Found ? hit.Score : 0, 1);
        }
        catch (Exception)
        {
            return (0, 1);
        }
    }
}
