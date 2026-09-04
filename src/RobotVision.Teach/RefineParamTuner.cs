using RobotVision.Core.Recipe;

namespace RobotVision.Teach;

/// <summary>方法锁定后的一档参数组合（向导寻优，不进 TRIGGER）。</summary>
public sealed record RefineParamTrial(
    string Label,
    double Score,
    bool Ok,
    string Note,
    bool Best,
    double MatchThreshold = 0,
    double RefineRangeDeg = 0,
    double AngleStdDeg = double.NaN);

/// <summary>锁定精修方法后的参数建议。空字段表示保持配方原值。</summary>
public sealed record RefineParamSuggestion(
    double? MatchThreshold,
    double? RefineRangeDeg,
    bool? UseEdgeMatch,
    HousingEdgePolarity? EdgePolarity,
    TabPolarityLock? TabPolarity,
    int? ExpectedCount,
    double Score,
    string Summary,
    IReadOnlyList<RefineParamTrial> Trials);

/// <summary>
/// 精修方法已定之后，用整夹已跑出的分做小网格寻优（匹配门、搜索角、期望件数）。
/// 不重跑 TRIGGER，不改光源/检测门往上抬。
/// </summary>
public static class RefineParamTuner
{
    public static RefineParamSuggestion? Tune(
        SegmentRefineMethod? method,
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> perFrame,
        IReadOnlyList<int> instanceCounts,
        TemplateOptions current,
        double teachPeak = 0,
        HousingEdgePolarity inferredEdge = HousingEdgePolarity.Auto,
        TabPolarityLock inferredTab = TabPolarityLock.Auto,
        int taskExpectedCount = 0,
        double sceneAspect = 0,
        bool edgeMatch = false)
    {
        if (method is not { } locked)
            return null;

        var peak = teachPeak >= 0.3 ? teachPeak : current.TeachPeakScore;
        var trials = new List<RefineParamTrial>();
        double? bestTh = null;
        var bestScore = 0.0;
        double bestStd = double.NaN;

        if (CanSweepThreshold(locked) && perFrame.Count >= 2 && HasUsableScores(perFrame, locked))
        {
            var directed = locked != SegmentRefineMethod.LineFit;
            RefineParamTrial? champion = null;
            foreach (var th in ThresholdGrid(peak, current.MatchThreshold))
            {
                var (score, std, note, ok) = ScoreThreshold(perFrame, locked, th, directed);
                var trial = new RefineParamTrial($"匹配门 {th:0.00}", score, ok, note, false, th,
                    current.RefineRangeDeg, std);
                trials.Add(trial);
                if (champion is null || BetterThreshold(trial, champion, peak))
                    champion = trial;
            }

            if (champion is not null)
            {
                bestTh = champion.MatchThreshold;
                bestScore = champion.Score;
                bestStd = champion.AngleStdDeg;
                var winTh = champion.MatchThreshold;
                trials = trials.Select(t => t with { Best = Math.Abs(t.MatchThreshold - winTh) < 0.001 }).ToList();
            }
        }

        double? range = SuggestRange(locked, current.RefineRangeDeg, sceneAspect, bestStd);
        int? count = SuggestExpectedCount(instanceCounts, taskExpectedCount);
        HousingEdgePolarity? edge = inferredEdge != HousingEdgePolarity.Auto &&
                                    inferredEdge != current.HousingEdgePolarity
            ? inferredEdge
            : null;
        TabPolarityLock? tab = inferredTab != TabPolarityLock.Auto && inferredTab != current.TabPolarity
            ? inferredTab
            : null;
        bool? useEdge = locked == SegmentRefineMethod.Template && edgeMatch != current.UseEdgeMatch
            ? edgeMatch
            : null;

        if (bestTh is { } thKeep && Math.Abs(thKeep - current.MatchThreshold) < 0.015)
            bestTh = null;
        if (range is { } rKeep && Math.Abs(rKeep - current.RefineRangeDeg) < 0.2)
            range = null;

        if (bestTh is null && range is null && count is null && edge is null && tab is null && useEdge is null)
            return null;

        var parts = new List<string>();
        if (bestTh is { } thSug)
            parts.Add(TeachNarrator.TunerThreshold(current.MatchThreshold, thSug));
        if (range is { } rSug)
            parts.Add(TeachNarrator.TunerRange(current.RefineRangeDeg, rSug));
        if (useEdge is { } eSug)
            parts.Add(TeachNarrator.TunerEdgeMatch(eSug));
        if (edge is { } ep)
            parts.Add(TeachNarrator.TunerEdgePolarity(ep));
        if (tab is { } tp)
            parts.Add(TeachNarrator.TunerTabPolarity(tp));
        if (count is { } nSug)
            parts.Add(TeachNarrator.TunerExpectedCount(nSug, taskExpectedCount));

        var summary = TeachNarrator.TunerSummary(parts);

        return new RefineParamSuggestion(
            bestTh, range, useEdge, edge, tab,
            taskExpectedCount == 0 ? count : null,
            bestScore,
            summary,
            trials);
    }

    public static IReadOnlyList<double> ThresholdGrid(double teachPeak, double current)
    {
        var set = new SortedSet<double>();
        if (teachPeak >= 0.3)
        {
            foreach (var f in new[] { 0.80, 0.85, 0.89, 0.93 })
                set.Add(Math.Clamp(teachPeak * f, 0.40, 0.92));
        }
        else
        {
            foreach (var t in new[] { 0.50, 0.55, 0.60, 0.65, 0.70 })
                set.Add(t);
        }

        if (current is >= 0.40 and <= 0.92)
            set.Add(Math.Round(current, 2));
        return set.ToList();
    }

    public static (double Score, double Std, string Note, bool Ok) ScoreThreshold(
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames,
        SegmentRefineMethod method,
        double threshold,
        bool directed)
    {
        var n = frames.Count;
        if (n == 0)
            return (0, double.NaN, "无帧", false);

        var okRows = new List<SegmentRefineCandidate>();
        foreach (var frame in frames)
        {
            SegmentRefineCandidate? hit = null;
            foreach (var c in frame)
            {
                if (c.Method == method)
                {
                    hit = c;
                    break;
                }
            }

            if (hit is { Skipped: false } && hit.Score >= threshold)
                okRows.Add(hit);
        }

        var agg = RaceScore.Compute(okRows, n, directed);
        return (agg.Score, agg.AngleStdDeg, TeachNarrator.RaceSummary(agg), agg.Ok);
    }

    private static bool CanSweepThreshold(SegmentRefineMethod method) =>
        method is SegmentRefineMethod.Template or SegmentRefineMethod.Sift or SegmentRefineMethod.ShapeMatch;

    private static bool HasUsableScores(
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames, SegmentRefineMethod method)
    {
        foreach (var frame in frames)
        {
            foreach (var c in frame)
            {
                if (c.Method == method && !c.Skipped && c.Score > 0.05)
                    return true;
            }
        }

        return false;
    }

    /// <summary>复合分优先；接近时取更高门（少放过低分），不必为 0.01 分差放宽。</summary>
    private static bool BetterThreshold(RefineParamTrial candidate, RefineParamTrial current, double peak)
    {
        if (candidate.Score > current.Score + 0.03)
            return true;
        if (current.Score > candidate.Score + 0.03)
            return false;
        if (peak >= 0.3)
        {
            var prefer = Math.Clamp(peak * 0.85, 0.40, 0.92);
            return Math.Abs(candidate.MatchThreshold - prefer) < Math.Abs(current.MatchThreshold - prefer);
        }

        return candidate.MatchThreshold > current.MatchThreshold;
    }

    private static double? SuggestRange(
        SegmentRefineMethod method, double current, double aspect, double angleStd)
    {
        if (!TemplateOptions.NeedsTaughtImage(method))
            return null;
        if (aspect >= 1.4)
            return null;
        if (!double.IsFinite(angleStd))
            return null;
        if (angleStd > 4 && current < 7.5)
            return Math.Clamp(8, 1, 45);
        if (angleStd < 1.2 && current > 4)
            return Math.Clamp(3, 1, 45);
        return null;
    }

    private static int? SuggestExpectedCount(IReadOnlyList<int> instanceCounts, int taskExpected)
    {
        var positive = instanceCounts.Where(c => c > 0).ToList();
        if (positive.Count < 2)
            return null;
        var mode = positive.GroupBy(c => c).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
        if (mode.Count() < Math.Max(2, (int)Math.Ceiling(0.6 * positive.Count)))
            return null;
        if (mode.Key is < 1 or > 20)
            return null;
        if (mode.Key == taskExpected)
            return null;
        return mode.Key;
    }
}
