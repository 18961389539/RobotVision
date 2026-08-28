using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

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
}

/// <summary>
/// 根据掩码几何、纹理熵、自转 180° NCC 可分性，再叠加同一帧四路赛马，推荐精修方法。
/// 只在配方页示教/试触发调用，不改变运行时互斥下拉的默认值，除非用户点「采用推荐」。
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
        double pixelConfidence = DetectionThresholdAdvisor.DefaultPixelConfidence)
    {
        var housing = MaskHousing.Fit(contour);
        var aspect = housing.LongLen / Math.Max(1.0, housing.ShortLen);
        var full = Cv2.MinAreaRect(contour);
        var fullShort = Math.Min(full.Size.Width, full.Size.Height);
        var protrusion = Math.Max(0, fullShort - housing.ShortLen);

        var holeArea = 0.0;
        var holeOk = false;
        if (bitPackedMask is { Length: > 0 } && maskWidth > 0 && maskHeight > 0)
        {
            var hole = MaskTemplateMatcher.RefineByCentroidHoleLine(bitPackedMask, maskWidth, maskHeight);
            if (hole is not null)
            {
                holeOk = true;
                holeArea = 1;
            }
        }

        var teachAreaPx = 0.0;
        if (contour.Count >= 3)
        {
            var arr = contour as Point2f[] ?? contour.ToArray();
            teachAreaPx = Cv2.ContourArea(arr);
        }

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
            var entropy = view is null ? 0 : GrayEntropy(view);
            var separability = view is null ? 0 : SelfFlipGap(view);
            var canOrient = separability >= 0.08 || holeOk || protrusion > 0.08 * Math.Max(8, housing.ShortLen);

            var heuristic = PickHeuristic(holeOk, protrusion, housing.ShortLen, separability, aspect, entropy);
            var candidates = SegmentRefineBakeOff.Run(
                bgr, contour, bitPackedMask, maskWidth, maskHeight, templateImage, template);
            var raced = SegmentRefineBakeOff.PickWinner(candidates);

            var method = raced?.Method ?? heuristic.Method;
            var edge = method == SegmentRefineMethod.Template && entropy is >= 4.0 and <= 6.5;
            if (raced is null && heuristic.Edge)
                edge = true;

            var (edgePolarity, tabPolarity) = InferPolarity(bgr, contour);
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
                    crop, fullImageWidth, fullImageHeight, originX, originY);
            }
            else if (view is not null && fullImageWidth > 0 && ownedUpright is null)
            {
                try
                {
                    var tmp = MaskTemplateMatcher.UprightCrop(bgr, contour, 0.05);
                    using (tmp.Upright)
                    {
                        featureRoi = FeatureRoiAdvisor.Suggest(
                            tmp, fullImageWidth, fullImageHeight, originX, originY);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            var why = raced is not null
                ? $"{FormatRace(candidates)}。采用实测胜出：{MethodLabel(method)}。{raced.Note}"
                : $"{heuristic.Why}{FormatRace(candidates)}";

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
                    ? " 凸起锁定在 +短轴。"
                    : " 凸起锁定在 −短轴。";

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
            };
        }
        finally
        {
            ownedUpright?.Dispose();
        }
    }

    public static string MethodLabel(SegmentRefineMethod method) => method switch
    {
        SegmentRefineMethod.LineFit => "直线拟合",
        SegmentRefineMethod.CentroidHoleLine => "质心-内标连线",
        SegmentRefineMethod.CaliperTab => "卡尺长边+凸起极性",
        _ => "模板匹配",
    };

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

    private static (HousingEdgePolarity Edge, TabPolarityLock Tab) InferPolarity(
        Mat bgr, IReadOnlyList<Point2f> contour)
    {
        var bright = MaskCaliperTab.TryRefine(
            bgr, contour, new CaliperRefineOptions(HousingEdgePolarity.BrightToDark));
        var qBright = bright.Pose is null ? 0 : MaskCaliperTab.QualityScore(MaskCaliperTab.LastDebug);
        var dark = MaskCaliperTab.TryRefine(
            bgr, contour, new CaliperRefineOptions(HousingEdgePolarity.DarkToBright));
        var qDark = dark.Pose is null ? 0 : MaskCaliperTab.QualityScore(MaskCaliperTab.LastDebug);

        MaskCaliperTab.CaliperAttempt? best = null;
        var edge = HousingEdgePolarity.Auto;
        if (bright.Pose is not null && qBright >= qDark)
        {
            best = bright;
            edge = HousingEdgePolarity.BrightToDark;
        }
        else if (dark.Pose is not null)
        {
            best = dark;
            edge = HousingEdgePolarity.DarkToBright;
        }

        if (best?.Pose?.TabSign is not { } sign)
            return (HousingEdgePolarity.Auto, TabPolarityLock.Auto);
        var tab = sign > 0 ? TabPolarityLock.PlusShortAxis : TabPolarityLock.MinusShortAxis;
        return (edge, tab);
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

    private static double GrayEntropy(Mat bgr)
    {
        using var gray = new Mat();
        if (bgr.Channels() == 1)
            bgr.CopyTo(gray);
        else
            Cv2.CvtColor(bgr, gray, bgr.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY);

        const int bins = 64;
        var hist = new int[bins];
        var n = gray.Rows * gray.Cols;
        if (n <= 0)
            return 0;

        var indexer = gray.GetGenericIndexer<byte>();
        for (var y = 0; y < gray.Rows; y++)
        {
            for (var x = 0; x < gray.Cols; x++)
                hist[indexer[y, x] >> 2]++;
        }

        var entropy = 0.0;
        var inv = 1.0 / n;
        foreach (var c in hist)
        {
            if (c == 0)
                continue;
            var p = c * inv;
            entropy -= p * Math.Log(p, 2);
        }

        return entropy;
    }

    /// <summary>1 − NCC(图, 自转180°)。越大越容易判头尾；近 0 表示近对称。</summary>
    private static double SelfFlipGap(Mat upright)
    {
        if (upright.Width < 8 || upright.Height < 8)
            return 0;
        using var flipped = new Mat();
        Cv2.Rotate(upright, flipped, RotateFlags.Rotate180);
        if (flipped.Width > upright.Width || flipped.Height > upright.Height)
            return 0;
        using var result = upright.MatchTemplate(flipped, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out _);
        return Math.Clamp(1.0 - maxVal, 0, 1);
    }
}
