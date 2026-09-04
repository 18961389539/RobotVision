using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Teach;

/// <summary>
/// ScenePlaybook —— 推荐决策核心：先套任务约束（无模型双斑点/双特征连线），否则按场景启发式选精修，
/// 再在"任务资格方法"内用赛马覆写，并算置信度与备选。<b>不做场景度量（Describe.cs）、不做先验存取（Priors.cs）。</b>
/// </summary>
public static partial class ScenePlaybook
{
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
            var blobAlts = new List<PlaybookCandidate> { KeyPointAlt(), DualModelAlt() };
            if (task.TeachAllowed)
                blobAlts.Add(DualTemplateAlt());
            return Finish(blob, blobAlts, scene, task,
                "任务勾选了无模型双斑点，优先双 BLOB 连线。", prior, sceneVotes);
        }

        if (task.HasTwoLandmarks)
        {
            var dual = new PlaybookCandidate(AngleMode.DualCenterLine, null, false, "双模型中心连线",
                "两个稳定特征（孔、PIN、Mark）时，连线比壳体精修更稳。请配置模型 A/B。", true);
            var alts = new List<PlaybookCandidate> { KeyPointAlt(), BlobAlt() };
            if (task.TeachAllowed)
                alts.Add(DualTemplateAlt());
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
                       (heuristic.EdgeMatch || descriptor.TextureEntropy
                           is >= TeachThresholds.EdgeMatchEntropyLo and <= TeachThresholds.EdgeMatchEntropyHi);
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

            if (double.IsFinite(raced.AngleStdDeg) && raced.AngleStdDeg > TeachThresholds.AngleStdStableDeg)
            {
                bakeC *= 0.72;
                notes.Add(FormattableString.Invariant($"整夹角σ {raced.AngleStdDeg:0.0}° 偏大"));
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
        var note = FormattableString.Invariant($"推荐置信 {c:0.00}（{label}）") + (notes.Count == 0 ? "" : "：" + string.Join("。", notes));
        return (c, note);
    }

    private static SceneDescriptor UnknownScene() =>
        new(SceneKind.Unknown, LightingClass.Unknown, 0, 0, 0, 0, false, 0, 0, "尚未分析画面，仅按任务约束推荐。")
        {
            KindConfidence = 0,
        };

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
                scene.TextureEntropy is >= TeachThresholds.EdgeMatchEntropyLo and <= TeachThresholds.EdgeMatchEntropyHi,
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
        if (task.TeachAllowed)
            Add(DualTemplateAlt());
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

    private static PlaybookCandidate DualTemplateAlt() =>
        new(AngleMode.DualTemplateCenterLine, null, false, "双模板连线",
            "两个可示教 Mark：模板1 定位、模板2 定向。", false);
}
