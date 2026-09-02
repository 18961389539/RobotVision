using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Vision.Inference.Strategies;

namespace RobotVision.Teach;

/// <summary>分割实例的可迁移场景类（推荐器用，不进 TRIGGER）。</summary>
public enum SceneKind
{
    Unknown = 0,
    HousingWithHole,
    HousingWithTab,
    Silhouette,
    PrintedTexture,
    WeakTextureBar,
    NearCircular,
}

/// <summary>粗分打光：目标相对背景更亮视为暗场剪影。</summary>
public enum LightingClass
{
    Unknown = 0,
    DarkField,
    BrightField,
}

/// <summary>任务约束：决定角度模式与精修资格，而不是只比 NCC/卡尺分。</summary>
public sealed record TaskConstraints(
    bool NeedDirectedAngle = true,
    bool TeachAllowed = true,
    bool AppearanceVaries = false,
    bool HasTwoLandmarks = false,
    bool UseBlobsWithoutModel = false,
    int ExpectedCount = 1);

/// <summary>从轮廓/掩码抽出的场景描述。</summary>
public sealed record SceneDescriptor(
    SceneKind Kind,
    LightingClass Lighting,
    double Aspect,
    double Circularity,
    double TextureEntropy,
    double Separability,
    bool HoleOk,
    double ProtrusionPx,
    double TeachAreaPx,
    string Why)
{
    /// <summary>软分类最高票相对次高票的把握；硬编码测试场景默认为 1。</summary>
    public double KindConfidence { get; init; } = 1;

    public SceneKind? RivalKind { get; init; }

    public IReadOnlyList<string> Conflicts { get; init; } = [];

    /// <summary>目标内熵 − 背景熵；正值表示件上比背景更有纹理。</summary>
    public double RelativeEntropy { get; init; }

    public double HoleQuality { get; init; }
}

/// <summary>一条可落地的配方建议（角度模式 + 可选精修）。</summary>
public sealed record PlaybookCandidate(
    AngleMode AngleMode,
    SegmentRefineMethod? Refine,
    bool EdgeMatch,
    string Title,
    string Why,
    bool IsPrimary);

/// <summary>结果库 / 配方政策对推荐的先验（只进向导与示教，不进 TRIGGER）。</summary>
public sealed record RecipePrior(
    IReadOnlyList<SegmentRefineMethod>? PolicyOrder = null,
    SegmentRefineMethod? Downrank = null,
    string Reason = "");

/// <summary>软分类一张票。</summary>
public readonly record struct SceneKindScore(SceneKind Kind, double Score);

/// <summary>场景 + 任务 + 可选赛马后的完整建议。</summary>
public sealed record PlaybookAdvice(
    PlaybookCandidate Primary,
    IReadOnlyList<PlaybookCandidate> Alternatives,
    SceneKind Scene,
    string SceneLabel,
    string Summary)
{
    public double Confidence { get; init; } = 1;

    public string ConfidenceNote { get; init; } = "";

    public bool IsUncertain => Confidence < 0.5;
}

/// <summary>
/// 跨工业场景的配方推荐：先分类场景，再套任务约束，赛马只在资格方法里比。
/// 不改 TRIGGER；由配方向导 / 示教页调用。
/// </summary>
public static class ScenePlaybook
{
    public static string SceneLabel(SceneKind kind) => kind switch
    {
        SceneKind.HousingWithHole => "带孔/槽壳体",
        SceneKind.HousingWithTab => "细长壳体+凸起",
        SceneKind.Silhouette => "剪影/弱纹理轮廓",
        SceneKind.PrintedTexture => "可分头尾的纹理件",
        SceneKind.WeakTextureBar => "弱纹理细长条",
        SceneKind.NearCircular => "近圆/近方件",
        _ => "未分类",
    };

    public static string AngleModeLabel(AngleMode mode) => mode switch
    {
        AngleMode.MaskMinAreaRect => "最小外接矩形",
        AngleMode.DualCenterLine => "双模型中心连线",
        AngleMode.KeyPointLine => "关键点连线",
        AngleMode.DualBlobCenterLine => "双 BLOB 连线",
        _ => "分割+精修",
    };

    public static string RefineLabel(SegmentRefineMethod? method) =>
        method is null ? "—" : SegmentRefineAdvisor.MethodLabel(method.Value);

    public static SceneDescriptor Describe(
        Mat bgr,
        IReadOnlyList<Point2f> contour,
        byte[]? bitPackedMask = null,
        int maskWidth = 0,
        int maskHeight = 0)
    {
        if (contour.Count < 3)
            return new SceneDescriptor(SceneKind.Unknown, LightingClass.Unknown, 0, 0, 0, 0, false, 0, 0,
                "轮廓点数不足，无法分类。");

        var housing = MaskHousing.Fit(contour);
        var obb = MaskHousing.FitObb(contour);
        var aspect = MaskHousing.Aspect(obb);
        var protrusion = Math.Max(0, obb.ShortLen - housing.ShortLen);
        var arr = contour as Point2f[] ?? contour.ToArray();
        var area = Cv2.ContourArea(arr);
        var peri = Cv2.ArcLength(arr, true);
        var circularity = peri > 1e-3 ? Math.Clamp(4 * Math.PI * area / (peri * peri), 0, 1) : 0;

        var holeQuality = 0.0;
        var holeOk = false;
        if (bitPackedMask is { Length: > 0 } && maskWidth > 0 && maskHeight > 0)
        {
            var hole = MaskTemplateMatcher.RefineByCentroidHoleLine(bitPackedMask, maskWidth, maskHeight);
            if (hole is not null)
            {
                holeOk = true;
                holeQuality = hole.Quality;
            }
        }

        var entropy = 0.0;
        var relativeEntropy = 0.0;
        var separability = 0.0;
        try
        {
            var crop = MaskTemplateMatcher.UprightCrop(bgr, contour, 0.05);
            using (crop.Upright)
            {
                entropy = GrayEntropy(crop.Upright);
                separability = SelfFlipGap(crop.Upright);
            }
        }
        catch (InvalidOperationException)
        {
        }

        relativeEntropy = RelativeGrayEntropy(bgr, arr);

        var lighting = ClassifyLighting(bgr, arr);
        var votes = ScoreKinds(holeOk, protrusion, housing.ShortLen, separability, aspect, entropy,
            circularity, relativeEntropy, holeQuality);
        var (kind, kindConf, rival) = PickKind(votes);
        var conflicts = ConflictNotes(votes, holeOk, separability);
        var why = kind switch
        {
            SceneKind.HousingWithHole => holeQuality > 0
                ? $"掩码内有孔/槽（质量 {holeQuality:0.00}），头尾可用几何偏置，不必靠灰度。"
                : "掩码内有稳定孔/槽，头尾可用几何偏置，不必靠灰度。",
            SceneKind.HousingWithTab => $"短轴外伸 {protrusion:0.0}px、轴比 {aspect:0.0}，适合卡尺抓长边。",
            SceneKind.Silhouette => $"相对熵 {relativeEntropy:+0.0;-0.0}、件内熵 {entropy:0.0}，更像剪影，灰度 NCC 容易漂。",
            SceneKind.PrintedTexture => $"0°/180° 分差 {separability:0.00}，相对熵 {relativeEntropy:+0.0;-0.0}，局部纹理可做模板。",
            SceneKind.WeakTextureBar => "细长且弱纹理，有向角难稳，无头尾需求时用直线/外接矩形。",
            SceneKind.NearCircular => "轴比接近 1，长边卡尺不可靠；有向角需要孔、第二特征或特征框。",
            _ => "几何/纹理都不典型，建议先确认任务约束再选模式。",
        };
        if (rival is { } rk)
            why += $" 次类 {SceneLabel(rk)}。";
        if (conflicts.Count > 0)
            why += " " + string.Join(" ", conflicts);

        return new SceneDescriptor(kind, lighting, aspect, circularity, entropy, separability, holeOk,
            protrusion, area, why)
        {
            KindConfidence = kindConf,
            RivalKind = rival,
            Conflicts = conflicts,
            RelativeEntropy = relativeEntropy,
            HoleQuality = holeQuality,
        };
    }

    public static PlaybookAdvice Recommend(
        TaskConstraints task,
        SceneDescriptor? scene = null,
        IReadOnlyList<SegmentRefineCandidate>? bakeoff = null,
        RecipePrior? prior = null,
        IReadOnlyDictionary<SceneKind, int>? sceneVotes = null)
    {
        if (task.UseBlobsWithoutModel)
        {
            var blob = new PlaybookCandidate(AngleMode.DualBlobCenterLine, null, false, "双 BLOB 连线",
                "无需模型：主 BLOB 定位，主→次连线定向。请在配方里配好阈值与主次。", true);
            return Finish(blob, [KeyPointAlt(), DualModelAlt()], scene, task,
                "任务勾选了无模型双斑点，优先双 BLOB 连线。", prior, sceneVotes);
        }

        if (task.HasTwoLandmarks)
        {
            var dual = new PlaybookCandidate(AngleMode.DualCenterLine, null, false, "双模型中心连线",
                "两个稳定特征（孔、PIN、Mark）时，连线比壳体精修更稳。请配置模型 A/B。", true);
            var alts = new List<PlaybookCandidate> { KeyPointAlt(), BlobAlt() };
            if (scene is not null)
                alts.Add(HeuristicRefine(task, scene) with { IsPrimary = false });
            return Finish(dual, alts, scene, task, "任务声明有两个定位特征，推荐连线模式而不是在精修里硬选。",
                prior, sceneVotes);
        }

        var descriptor = scene ?? UnknownScene();
        var heuristic = HeuristicRefine(task, descriptor);
        var raced = PickWinnerForTask(bakeoff ?? [], task, descriptor, prior);
        var keepUntaught = KeepHeuristicUntilTaught(heuristic, bakeoff);
        PlaybookCandidate primary;
        string summary;
        if (raced is not null && heuristic.AngleMode == AngleMode.MaskTemplate && !keepUntaught)
        {
            var edge = raced.Method == SegmentRefineMethod.Template &&
                       (heuristic.EdgeMatch || descriptor.TextureEntropy is >= 4.0 and <= 6.5);
            primary = new PlaybookCandidate(AngleMode.MaskTemplate, raced.Method, edge,
                $"{AngleModeLabel(AngleMode.MaskTemplate)} · {RefineLabel(raced.Method)}",
                $"整夹/本帧赛马在任务约束下胜出：{raced.Note}", true);
            summary = $"{SceneLabel(descriptor.Kind)}。{descriptor.Why} 采用赛马：{RefineLabel(raced.Method)}。";
        }
        else
        {
            primary = heuristic;
            summary = $"{SceneLabel(descriptor.Kind)}。{descriptor.Why} {heuristic.Why}";
            if (keepUntaught && raced is not null)
                summary += $" 尚未示教，主建议保持 {RefineLabel(heuristic.Refine)}；免模板赛马胜出 {RefineLabel(raced.Method)}（{raced.Note}），可作备选。采用后请示教。";
        }

        var alternatives = BuildAlternatives(task, descriptor, primary);
        if (descriptor.Kind == SceneKind.NearCircular && task.NeedDirectedAngle && !descriptor.HoleOk)
            summary += " 近圆件仅靠长边无法定头尾：优先双特征连线，或示教时框选不对称块。";
        if (!task.TeachAllowed && primary.Refine is { } r && TemplateOptions.NeedsTaughtImage(r))
            summary += " 当前不允许示教，已避开模板/SIFT/形状匹配。";
        if (descriptor.Lighting == LightingClass.DarkField && primary.Refine == SegmentRefineMethod.Template)
            summary += " 暗场下灰度模板容易漂，若不稳定请改形状匹配。";
        if (prior?.Downrank is { } down && primary.Refine == down && !string.IsNullOrEmpty(prior.Reason))
            summary += $" 结果库：{prior.Reason}，已压低当前方法，请看备选。";

        return Finish(primary, alternatives, descriptor, task, summary, prior, sceneVotes, keepUntaught, raced, bakeoff);
    }

    /// <summary>模板/SIFT/形状匹配是否真正跑过（有示教图）。未示教的 0 分不算败，不能压过启发式。</summary>
    public static bool TaughtMethodsRaced(IReadOnlyList<SegmentRefineCandidate>? bakeoff) =>
        bakeoff is { Count: > 0 } && bakeoff.Any(c => TemplateOptions.NeedsTaughtImage(c.Method) && !c.Skipped);

    /// <summary>启发式要示教、但赛马里模板类都没跑：保持启发式，不采用卡尺等免模板胜出。</summary>
    public static bool KeepHeuristicUntilTaught(
        PlaybookCandidate heuristic, IReadOnlyList<SegmentRefineCandidate>? bakeoff) =>
        heuristic.Refine is { } r && TemplateOptions.NeedsTaughtImage(r) && !TaughtMethodsRaced(bakeoff);

    /// <summary>把角度模式建议落到精修枚举（配方页示教/试触发仍在分割+精修内）。</summary>
    public static SegmentRefineMethod? RefineMethodOf(PlaybookCandidate c) =>
        c.Refine ?? (c.AngleMode == AngleMode.MaskMinAreaRect ? SegmentRefineMethod.LineFit : null);

    public static bool SameRecipe(PlaybookCandidate a, PlaybookCandidate b) =>
        a.AngleMode == b.AngleMode && a.Refine == b.Refine;

    /// <summary>打开配方向导时按当前编辑器预填任务约束。期望件数 0 表示不检查，不抬成 1。</summary>
    public static TaskConstraints FromRecipe(RecipeConfig recipe)
    {
        var mask = recipe.AngleMode == AngleMode.MaskTemplate;
        var refine = recipe.Template.RefineMethod;
        var directed = recipe.RotationCompensation == RotationCompensationMode.EccentricTool
                       || recipe.AngleMode is AngleMode.DualCenterLine or AngleMode.KeyPointLine
                           or AngleMode.DualBlobCenterLine
                       || (mask && refine != SegmentRefineMethod.LineFit);
        var teach = !string.IsNullOrEmpty(recipe.Template.TemplateImageBase64)
                    || (mask && TemplateOptions.NeedsTaughtImage(refine));
        return new TaskConstraints(
            directed,
            teach,
            AppearanceVaries: false,
            HasTwoLandmarks: recipe.AngleMode == AngleMode.DualCenterLine,
            UseBlobsWithoutModel: recipe.AngleMode == AngleMode.DualBlobCenterLine,
            ExpectedCount: Math.Clamp(recipe.Template.ExpectedCount, 0, 20));
    }

    public static RecipePrior? FromTemplate(TemplateOptions? template) =>
        template?.RefinePolicyOrder is { Count: > 0 } order ? new RecipePrior(order) : null;

    /// <summary>结果库信号压低当前精修方法；空信号且无政策序则返回 null。</summary>
    public static RecipePrior? FromHealth(
        bool refineFailHigh,
        bool angleBimodal,
        bool scoreDrift,
        SegmentRefineMethod? current,
        IReadOnlyList<SegmentRefineMethod>? policyOrder = null)
    {
        SegmentRefineMethod? down = null;
        var reasons = new List<string>();
        if (current is { } method)
        {
            if (refineFailHigh)
            {
                down = method;
                reasons.Add("1019 精修失败偏高");
            }

            if (angleBimodal)
            {
                down = method;
                reasons.Add("合格角呈 ±180° 双峰");
            }

            if (scoreDrift && TemplateOptions.NeedsTaughtImage(method))
            {
                down = method;
                reasons.Add("精修分相对示教峰下降");
            }
        }

        if (down is null && policyOrder is not { Count: > 0 })
            return null;
        return new RecipePrior(policyOrder is { Count: > 0 } ? policyOrder : null, down, string.Join("；", reasons));
    }

    public static RecipePrior? Merge(params RecipePrior?[] parts)
    {
        IReadOnlyList<SegmentRefineMethod>? order = null;
        SegmentRefineMethod? down = null;
        var reasons = new List<string>();
        foreach (var p in parts)
        {
            if (p is null)
                continue;
            if (p.PolicyOrder is { Count: > 0 })
                order = p.PolicyOrder;
            if (p.Downrank is { } d)
            {
                down = d;
                if (!string.IsNullOrEmpty(p.Reason))
                    reasons.Add(p.Reason);
            }
        }

        if (order is null && down is null)
            return null;
        return new RecipePrior(order, down, string.Join("；", reasons));
    }

    public static bool SuppressGrayTemplate(SceneDescriptor? scene) =>
        scene is { Lighting: LightingClass.DarkField, Kind: not SceneKind.PrintedTexture };

    public static bool IsEligible(
        SegmentRefineMethod method, TaskConstraints task, SceneDescriptor? scene = null)
    {
        if (!task.NeedDirectedAngle && method == SegmentRefineMethod.LineFit)
            return true;
        if (task.NeedDirectedAngle && method == SegmentRefineMethod.LineFit)
            return false;
        if (!task.TeachAllowed && TemplateOptions.NeedsTaughtImage(method))
            return false;
        if ((task.AppearanceVaries || SuppressGrayTemplate(scene)) && method == SegmentRefineMethod.Template)
            return false;
        return true;
    }

    public static SegmentRefineCandidate? PickWinnerForTask(
        IReadOnlyList<SegmentRefineCandidate> candidates,
        TaskConstraints task,
        SceneDescriptor? scene = null,
        RecipePrior? prior = null)
    {
        var eligible = candidates.Where(c => IsEligible(c.Method, task, scene)).ToList();
        return SegmentRefineBakeOff.PickWinner(eligible, prior?.PolicyOrder, prior?.Downrank);
    }

    public static (HousingEdgePolarity Edge, TabPolarityLock Tab) InferPolarity(
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

    private static PlaybookAdvice Finish(
        PlaybookCandidate primary,
        IReadOnlyList<PlaybookCandidate> alternatives,
        SceneDescriptor? scene,
        TaskConstraints task,
        string summary,
        RecipePrior? prior = null,
        IReadOnlyDictionary<SceneKind, int>? sceneVotes = null,
        bool keepUntaught = false,
        SegmentRefineCandidate? raced = null,
        IReadOnlyList<SegmentRefineCandidate>? bakeoff = null)
    {
        var kind = scene?.Kind ?? SceneKind.Unknown;
        var extras = new List<string>();
        if (task.NeedDirectedAngle)
            extras.Add("要有向角");
        else
            extras.Add("允许无向角");
        if (!task.TeachAllowed)
            extras.Add("免示教");
        if (task.AppearanceVaries)
            extras.Add("外观会变");
        if (scene?.Lighting == LightingClass.DarkField)
            extras.Add("暗场");
        else if (scene?.Lighting == LightingClass.BrightField)
            extras.Add("亮场");
        var head = extras.Count == 0 ? summary : $"{string.Join(" · ", extras)}。{summary}";
        var (confidence, note) = MeasureConfidence(primary, scene, bakeoff, task, keepUntaught, raced, prior, sceneVotes);
        return new PlaybookAdvice(primary, alternatives, kind, SceneLabel(kind), head)
        {
            Confidence = confidence,
            ConfidenceNote = note,
        };
    }

    private static (double Confidence, string Note) MeasureConfidence(
        PlaybookCandidate primary,
        SceneDescriptor? scene,
        IReadOnlyList<SegmentRefineCandidate>? bakeoff,
        TaskConstraints task,
        bool keepUntaught,
        SegmentRefineCandidate? raced,
        RecipePrior? prior,
        IReadOnlyDictionary<SceneKind, int>? sceneVotes)
    {
        var notes = new List<string>();
        var kindC = scene?.KindConfidence ?? (task.HasTwoLandmarks || task.UseBlobsWithoutModel ? 0.9 : 0.35);
        var bakeC = 0.55;
        if (keepUntaught)
        {
            bakeC = 0.42;
            notes.Add("尚未示教，赛马未否决启发式");
        }
        else if (raced is not null)
        {
            var eligible = (bakeoff ?? [])
                .Where(c => c.Ok && IsEligible(c.Method, task, scene))
                .OrderByDescending(c => c.Score)
                .ToList();
            if (eligible.Count >= 2)
            {
                var gap = eligible[0].Score - eligible[1].Score;
                bakeC = Math.Clamp(0.40 + gap / 0.20, 0.40, 1.0);
                if (gap < 0.08)
                    notes.Add("赛马前两名接近，请核对备选");
            }
            else
                bakeC = 0.72;

            if (double.IsFinite(raced.AngleStdDeg) && raced.AngleStdDeg > 4)
            {
                bakeC *= 0.72;
                notes.Add($"整夹角σ {raced.AngleStdDeg:0.0}° 偏大");
            }
        }

        var voteC = kindC;
        if (sceneVotes is { Count: > 0 } && scene is not null)
        {
            var total = sceneVotes.Values.Sum();
            if (total > 0)
            {
                sceneVotes.TryGetValue(scene.Kind, out var n);
                voteC = n / (double)total;
                if (voteC < 0.70)
                    notes.Add("各帧场景类不一致");
            }
        }

        if (scene?.Conflicts is { Count: > 0 } conflicts)
            notes.AddRange(conflicts);

        var c = 0.40 * kindC + 0.40 * bakeC + 0.20 * voteC;
        if (prior?.Downrank is { } d && primary.Refine == d)
        {
            c *= 0.75;
            notes.Add(string.IsNullOrEmpty(prior.Reason) ? "结果库提示当前方法不稳" : prior.Reason);
        }

        c = Math.Clamp(c, 0.05, 0.99);
        var label = c >= 0.75 ? "较有把握" : c >= 0.50 ? "中等把握" : "把握不足，请点选备选";
        var note = $"推荐置信 {c:0.00}（{label}）" + (notes.Count == 0 ? "" : "：" + string.Join("。", notes));
        return (c, note);
    }

    private static SceneDescriptor UnknownScene() =>
        new(SceneKind.Unknown, LightingClass.Unknown, 0, 0, 0, 0, false, 0, 0, "尚未分析画面，仅按任务约束推荐。")
        {
            KindConfidence = 0,
        };

    /// <summary>加权软分类：孔不独占；票分连续，避免固定熵/分差硬切。</summary>
    public static IReadOnlyList<SceneKindScore> ScoreKinds(
        bool holeOk,
        double protrusion,
        double shortLen,
        double separability,
        double aspect,
        double entropy,
        double circularity,
        double relativeEntropy = 0,
        double holeQuality = 0.85)
    {
        var scores = new Dictionary<SceneKind, double>();
        void Add(SceneKind k, double s)
        {
            if (s <= 0.05)
                return;
            scores[k] = Math.Clamp(s, 0, 1);
        }

        var shortSafe = Math.Max(8, shortLen);
        var protRel = protrusion / shortSafe;
        var sepN = Math.Clamp(separability / 0.16, 0, 1);
        var lowEntropy = Math.Clamp((4.8 - entropy) / 4.8, 0, 1);
        var aspectBar = Math.Clamp((aspect - 1.25) / 1.0, 0, 1);
        var aspectRound = aspect < 1.5 ? Math.Clamp((1.5 - aspect) / 0.5, 0, 1) : 0;
        var textureOnPart = Math.Clamp((entropy - 2.2) / 3.0, 0, 1);

        if (holeOk)
            Add(SceneKind.HousingWithHole, 0.35 + 0.50 * Math.Clamp(holeQuality, 0, 1));

        Add(SceneKind.NearCircular, aspectRound * circularity);
        Add(SceneKind.WeakTextureBar, aspectBar * lowEntropy * (1 - sepN) * (aspect >= 1.55 ? 1.0 : 0.7));
        Add(SceneKind.PrintedTexture, sepN * textureOnPart);
        Add(SceneKind.HousingWithTab,
            Math.Clamp(protRel / 0.12, 0, 1) * 0.65 + Math.Clamp((aspect - 1.45) / 0.9, 0, 1) * 0.35);

        var silhouetteTex = Math.Clamp((4.5 - entropy) / 4.5, 0, 1);
        if (relativeEntropy <= 1.0)
        {
            var slender = Math.Clamp((aspect - 1.7) / 0.8, 0, 1);
            Add(SceneKind.Silhouette,
                silhouetteTex * Math.Clamp((aspect - 1.15) / 0.7, 0, 1) * (1 - sepN) * (1 - 0.45 * slender));
        }

        return scores.Select(kv => new SceneKindScore(kv.Key, kv.Value)).OrderByDescending(s => s.Score).ToList();
    }

    private static (SceneKind Kind, double Confidence, SceneKind? Rival) PickKind(
        IReadOnlyList<SceneKindScore> votes)
    {
        if (votes.Count == 0 || votes[0].Score < 0.18)
            return (SceneKind.Unknown, 0, null);
        var top = votes[0];
        SceneKindScore? second = votes.Count > 1 ? votes[1] : null;
        var conf = second is { } s && s.Score > 0.05
            ? Math.Clamp(top.Score / (top.Score + s.Score), 0.15, 0.99)
            : Math.Clamp(0.55 + 0.45 * top.Score, 0.55, 0.99);
        var rival = second is { } r && r.Score >= 0.72 * top.Score ? r.Kind : (SceneKind?)null;
        return (top.Kind, conf, rival);
    }

    private static List<string> ConflictNotes(
        IReadOnlyList<SceneKindScore> votes, bool holeOk, double separability)
    {
        double ScoreOf(SceneKind k)
        {
            foreach (var v in votes)
            {
                if (v.Kind == k)
                    return v.Score;
            }

            return 0;
        }

        var list = new List<string>();
        var hole = ScoreOf(SceneKind.HousingWithHole);
        var printed = ScoreOf(SceneKind.PrintedTexture);
        var tab = ScoreOf(SceneKind.HousingWithTab);
        var circ = ScoreOf(SceneKind.NearCircular);
        if (holeOk && hole >= 0.40 && printed >= 0.45)
            list.Add("有孔且有可分纹理，孔槽不独占。");
        else if (holeOk && hole >= 0.40 && separability >= 0.08)
            list.Add("有孔且 0/180 可分，孔槽不独占。");
        if (tab >= 0.45 && printed >= 0.45)
            list.Add("凸起壳体同时有可分纹理。");
        if (circ >= 0.40 && tab >= 0.40)
            list.Add("近圆与细长信号冲突。");
        return list;
    }

    private static PlaybookCandidate HeuristicRefine(TaskConstraints task, SceneDescriptor scene)
    {
        if (!task.NeedDirectedAngle)
        {
            if (scene.Kind is SceneKind.WeakTextureBar or SceneKind.NearCircular or SceneKind.Unknown)
                return new PlaybookCandidate(AngleMode.MaskMinAreaRect, null, false, "最小外接矩形",
                    "不需要头尾时，外接矩形足够且免示教。", true);
            return new PlaybookCandidate(AngleMode.MaskTemplate, SegmentRefineMethod.LineFit, false,
                "分割+直线拟合", "无向长边比卡尺极性更稳。", true);
        }

        if (!task.TeachAllowed)
        {
            var method = scene.Kind switch
            {
                SceneKind.HousingWithHole => SegmentRefineMethod.CentroidHoleLine,
                SceneKind.NearCircular => scene.HoleOk
                    ? SegmentRefineMethod.CentroidHoleLine
                    : SegmentRefineMethod.CaliperTab,
                _ => SegmentRefineMethod.CaliperTab,
            };
            return new PlaybookCandidate(AngleMode.MaskTemplate, method, false,
                $"分割+精修 · {RefineLabel(method)}",
                "不允许示教，只用免模板路径（卡尺或孔槽）。", true);
        }

        var (refine, edge, why) = scene.Kind switch
        {
            SceneKind.HousingWithHole => (SegmentRefineMethod.CentroidHoleLine, false,
                "孔/槽定头尾，免模板。"),
            SceneKind.HousingWithTab => (SegmentRefineMethod.CaliperTab, false,
                "细长+凸起，卡尺抓长边；头尾用凸起极性。"),
            SceneKind.Silhouette => task.AppearanceVaries
                ? (SegmentRefineMethod.ShapeMatch, false, "剪影且外观会变，形状匹配比灰度 NCC 稳。")
                : (SegmentRefineMethod.ShapeMatch, true, "剪影优先形状匹配；若示教模板也可开边缘图定角。"),
            SceneKind.PrintedTexture => (SegmentRefineMethod.Template,
                scene.TextureEntropy is >= 4.0 and <= 6.5,
                "纹理可分头尾，示教后用模板；中等熵建议边缘图定角。"),
            SceneKind.WeakTextureBar => (SegmentRefineMethod.CaliperTab, false,
                "要有向角但纹理弱，先试卡尺；不稳再改双特征。"),
            SceneKind.NearCircular => scene.HoleOk
                ? (SegmentRefineMethod.CentroidHoleLine, false, "近圆有孔，走质心-内标。")
                : (SegmentRefineMethod.Template, true, "近圆无孔：示教不对称块做模板，或改双特征连线。"),
            _ => (SegmentRefineMethod.CaliperTab, false, "先用卡尺探路，赛马后再改。"),
        };

        if (task.AppearanceVaries && refine == SegmentRefineMethod.Template)
        {
            refine = SegmentRefineMethod.ShapeMatch;
            edge = false;
            why = "外观会变，避开灰度模板，改形状匹配。";
        }

        if (SuppressGrayTemplate(scene) && refine == SegmentRefineMethod.Template)
        {
            refine = SegmentRefineMethod.ShapeMatch;
            edge = false;
            why += " 暗场剪影，避开灰度 NCC，改形状匹配。";
        }
        else if (scene.Lighting == LightingClass.BrightField && refine == SegmentRefineMethod.CaliperTab)
            why += " 亮场壳体，卡尺按亮→暗抓边。";
        else if (scene.Lighting == LightingClass.DarkField && refine == SegmentRefineMethod.CaliperTab)
            why += " 暗场壳体，卡尺按暗→亮抓边。";

        if (scene.KindConfidence < 0.55)
            why += " 场景不完全典型，请看备选。";
        if (scene.RivalKind == SceneKind.PrintedTexture && refine == SegmentRefineMethod.CentroidHoleLine)
            why += " 同时有可分纹理，模板已列入备选。";
        if (scene.Conflicts.Count > 0 && scene.KindConfidence < 0.72)
            why += " 分类有冲突，请核对备选。";

        return new PlaybookCandidate(AngleMode.MaskTemplate, refine, edge,
            $"分割+精修 · {RefineLabel(refine)}", why, true);
    }

    private static List<PlaybookCandidate> BuildAlternatives(
        TaskConstraints task, SceneDescriptor scene, PlaybookCandidate primary)
    {
        var list = new List<PlaybookCandidate>();
        void Add(PlaybookCandidate c)
        {
            if (SameRecipe(c, primary))
                return;
            if (c.Refine is { } m && !IsEligible(m, task, scene) && c.AngleMode == AngleMode.MaskTemplate)
                return;
            list.Add(c with { IsPrimary = false });
        }

        Add(DualModelAlt());
        Add(KeyPointAlt());
        Add(BlobAlt());
        if (task.TeachAllowed && primary.Refine != SegmentRefineMethod.Template)
            Add(new PlaybookCandidate(AngleMode.MaskTemplate, SegmentRefineMethod.Template, true,
                "分割+模板（边缘定角）", "丝印/齿列不对称时用；需示教。", false));
        if (task.TeachAllowed && primary.Refine != SegmentRefineMethod.ShapeMatch)
            Add(new PlaybookCandidate(AngleMode.MaskTemplate, SegmentRefineMethod.ShapeMatch, false,
                "分割+形状匹配", "剪影/磨损时比灰度模板稳。", false));
        if (scene.HoleOk && primary.Refine != SegmentRefineMethod.CentroidHoleLine)
            Add(new PlaybookCandidate(AngleMode.MaskTemplate, SegmentRefineMethod.CentroidHoleLine, false,
                "分割+孔槽连线", "掩码能画出孔时最省心。", false));
        if (primary.Refine != SegmentRefineMethod.CaliperTab && task.NeedDirectedAngle)
            Add(new PlaybookCandidate(AngleMode.MaskTemplate, SegmentRefineMethod.CaliperTab, false,
                "分割+卡尺", "免示教，细长壳体常用。", false));
        if (!task.NeedDirectedAngle && primary.AngleMode != AngleMode.MaskMinAreaRect)
            Add(new PlaybookCandidate(AngleMode.MaskMinAreaRect, null, false, "最小外接矩形",
                "只要粗角、不要头尾。", false));
        return list.Take(4).ToList();
    }

    private static PlaybookCandidate DualModelAlt() =>
        new(AngleMode.DualCenterLine, null, false, "双模型中心连线",
            "两个检测目标连线定向。", false);

    private static PlaybookCandidate KeyPointAlt() =>
        new(AngleMode.KeyPointLine, null, false, "关键点连线",
            "同一姿态模型上两点连线。", false);

    private static PlaybookCandidate BlobAlt() =>
        new(AngleMode.DualBlobCenterLine, null, false, "双 BLOB 连线",
            "无需模型的主次斑点。", false);

    private static LightingClass ClassifyLighting(Mat bgr, Point2f[] contour)
    {
        if (bgr.Empty() || contour.Length < 3)
            return LightingClass.Unknown;
        using var gray = ToGray(bgr);
        using var mask = new Mat(gray.Size(), MatType.CV_8UC1, Scalar.All(0));
        var pts = contour.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
        Cv2.DrawContours(mask, new[] { pts }, -1, Scalar.All(255), -1);
        var inside = Cv2.Mean(gray, mask);
        using var inv = new Mat();
        Cv2.BitwiseNot(mask, inv);
        var outside = Cv2.Mean(gray, inv);
        var delta = inside.Val0 - outside.Val0;
        if (delta > 12)
            return LightingClass.DarkField;
        if (delta < -12)
            return LightingClass.BrightField;
        return LightingClass.Unknown;
    }

    private static Mat ToGray(Mat src)
    {
        if (src.Channels() == 1)
            return src.Clone();
        var gray = new Mat();
        Cv2.CvtColor(src, gray, src.Channels() == 4
            ? ColorConversionCodes.BGRA2GRAY
            : ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static double GrayEntropy(Mat bgr)
    {
        using var gray = ToGray(bgr);
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

    private static double RelativeGrayEntropy(Mat bgr, Point2f[] contour)
    {
        if (bgr.Empty() || contour.Length < 3)
            return 0;
        using var gray = ToGray(bgr);
        using var mask = new Mat(gray.Size(), MatType.CV_8UC1, Scalar.All(0));
        var pts = contour.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
        Cv2.DrawContours(mask, new[] { pts }, -1, Scalar.All(255), -1);
        var inside = GrayEntropyMasked(gray, mask);
        using var inv = new Mat();
        Cv2.BitwiseNot(mask, inv);
        return inside - GrayEntropyMasked(gray, inv);
    }

    private static double GrayEntropyMasked(Mat gray, Mat mask)
    {
        const int bins = 64;
        var hist = new int[bins];
        var n = 0;
        var g = gray.GetGenericIndexer<byte>();
        var m = mask.GetGenericIndexer<byte>();
        for (var y = 0; y < gray.Rows; y++)
        {
            for (var x = 0; x < gray.Cols; x++)
            {
                if (m[y, x] == 0)
                    continue;
                hist[g[y, x] >> 2]++;
                n++;
            }
        }

        if (n < 16)
            return 0;
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
