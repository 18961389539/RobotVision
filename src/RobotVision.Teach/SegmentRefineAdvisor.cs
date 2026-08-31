using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;

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
    public static SegmentRefineAdvice Analyze(
        Mat bgr,
        IReadOnlyList<Point2f> contour,
        Mat? upright = null,
        byte[]? bitPackedMask = null,
        int maskWidth = 0,
        int maskHeight = 0,
        TemplateOptions? template = null,
        Mat? templateImage = null,
        int fullImageWidth = 0,
        int fullImageHeight = 0,
        double originX = 0,
        double originY = 0,
        double instanceConfidence = 0,
        double boxConfidence = DetectionThresholdAdvisor.DefaultBoxConfidence,
        double pixelConfidence = DetectionThresholdAdvisor.DefaultPixelConfidence,
        TaskConstraints? task = null,
        SegmentRefineBakeOff.TeachCache? teachCache = null,
        RecipePrior? prior = null)
    {
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
        UprightCropResult? crop = null;
        if (view is null || view.Empty())
        {
            try
            {
                crop = MaskTemplateMatcher.UprightCrop(bgr, contour, 0.05);
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
            var canOrient = scene.Separability >= 0.08 || holeOk || protrusion > 0.08 * Math.Max(8, 1);

            var candidates = SegmentRefineBakeOff.Run(
                bgr, contour, bitPackedMask, maskWidth, maskHeight, templateImage, template, teachCache);
            var playbook = ScenePlaybook.Recommend(task, scene, candidates, prior);
            var method = ScenePlaybook.RefineMethodOf(playbook.Primary)
                         ?? PickHeuristic(holeOk, protrusion, Math.Max(8, 1), separability, aspect, entropy).Method;
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
                    var tmp = MaskTemplateMatcher.UprightCrop(bgr, contour, 0.05);
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

            var why = $"{playbook.Summary}{FormatRace(candidates)}";
            if (raced is not null && playbook.Primary.Refine == raced.Method)
                why += $" 采用实测胜出：{MethodLabel(method)}。{raced.Note}";

            if (method == SegmentRefineMethod.Template && !canOrient)
                why += featureRoi is not null
                    ? " 已建议特征框（对 180° 最不对称的块）。"
                    : " 当前 0°/180° 分差偏小，示教时请框选凸起或齿脚。";

            if (suggestedTh > 0)
            {
                why += $" 示教峰 NCC {teachPeak:0.00}，建议匹配阈值 {suggestedTh:0.00}";
                if (peakSharp < 0.08)
                    why += $"（主峰较钝 锐度 {peakSharp:0.00}，已抬高匹配门）";
                why += "。";
            }

            if (Math.Abs(sugBox - boxConfidence) > 0.02)
                why += $" 检测置信度建议 {sugBox:0.00}（实例 {instanceConfidence:0.00}，当前门过紧）。";
            if (Math.Abs(sugPix - pixelConfidence) > 0.02)
                why += $" 掩码像素置信度建议 {sugPix:0.00}。";
            if (edgePolarity != HousingEdgePolarity.Auto)
                why += edgePolarity == HousingEdgePolarity.DarkToBright
                    ? " 已锁定暗场边缘。"
                    : " 已锁定亮场边缘。";
            if (tabPolarity != TabPolarityLock.Auto)
                why += tabPolarity == TabPolarityLock.PlusShortAxis
                    ? " 示教凸起在 +短轴（运行每帧实测）。"
                    : " 示教凸起在 −短轴（运行每帧实测）。";

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

        var why = $"回放 {detected}/{total} 检出。{FormatRace(aggregated)}";
        if (raced is not null && playbook.Primary.Refine == raced.Method)
            why += $" 采用回放胜出：{MethodLabel(method)}。{raced.Note}";
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

    public static string MethodLabel(SegmentRefineMethod method) => method switch
    {
        SegmentRefineMethod.LineFit => "直线拟合",
        SegmentRefineMethod.CentroidHoleLine => "质心-内标连线",
        SegmentRefineMethod.CaliperTab => "卡尺长边+凸起极性",
        SegmentRefineMethod.Sift => "SIFT特征匹配",
        SegmentRefineMethod.ShapeMatch => "形状匹配",
        _ => "模板匹配",
    };

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
                return $"{c.Score:0.00}";
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
        if (protrusion > 0.08 * Math.Max(8, shortLen))
            return new(SegmentRefineMethod.CaliperTab, false,
                "短轴一侧有外伸，推荐卡尺长边+凸起极性（免示教，有方向）。");
        if (separability >= 0.08)
        {
            var edge = entropy is >= 4.0 and <= 6.5;
            return new(SegmentRefineMethod.Template, edge, edge
                ? $"0°/180° 可分（分差 {separability:0.00}），推荐模板匹配并打开边缘图定角。"
                : $"0°/180° 可分（分差 {separability:0.00}），推荐模板匹配（需示教）。");
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

    private static string FormatRace(IReadOnlyList<SegmentRefineCandidate> candidates)
    {
        if (candidates.Count == 0)
            return "";
        var parts = candidates.Select(c =>
            $"{MethodLabel(c.Method)} {(c.Ok ? $"✓{c.Score:0.00}" : "—")}");
        return " 赛马：" + string.Join(" · ", parts);
    }

    private static (double Score, double Sharpness) MeasureTeachPeak(
        Mat? upright, Mat? templateImage, TemplateOptions? options)
    {
        if (upright is null || upright.Empty() || templateImage is null || templateImage.Empty())
            return (0, 1);
        var range = Math.Clamp(options?.RefineRangeDeg ?? 5, 1, 45);
        try
        {
            var match = options?.UseEdgeMatch == true
                ? MaskTemplateMatcher.MatchBestHybrid(upright, templateImage, range, 0.01)
                : MaskTemplateMatcher.MatchBest(upright, templateImage, range, 0.01);
            var sharp = MaskTemplateMatcher.LastDebug.PeakSharpness;
            if (sharp <= 0)
                sharp = 1;
            return (match?.Score ?? 0, sharp);
        }
        catch (Exception)
        {
            return (0, 1);
        }
    }
}
