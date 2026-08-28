using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>同一帧四条精修路径的实测结果（配方页赛马，不进 TRIGGER）。</summary>
public sealed record SegmentRefineCandidate(
    SegmentRefineMethod Method,
    bool Ok,
    bool Directed,
    double Score,
    string Note);

/// <summary>对同一分割目标跑卡尺 / 直线 / 孔槽 / 模板，给出可比较的精修分。</summary>
public static class SegmentRefineBakeOff
{
    public static IReadOnlyList<SegmentRefineCandidate> Run(
        Mat bgr,
        IReadOnlyList<Point2f> contour,
        byte[]? bitPackedMask = null,
        int maskWidth = 0,
        int maskHeight = 0,
        Mat? template = null,
        TemplateOptions? options = null)
    {
        var list = new List<SegmentRefineCandidate>(4);
        var housing = MaskHousing.Fit(contour);

        var line = MaskTemplateMatcher.RefineByLineFit(contour, housing.LongAxisDeg);
        if (line.Fitted)
        {
            var residual = Math.Abs(AngleGeometry.UndirectedDeltaDeg(line.AngleDeg, housing.LongAxisDeg));
            var score = Math.Clamp(1.0 - residual / 5.0, 0.2, 1);
            list.Add(new(SegmentRefineMethod.LineFit, true, false, score,
                $"直线拟合过门（残差 {residual:0.00}°）"));
        }
        else
            list.Add(new(SegmentRefineMethod.LineFit, false, false, 0, "直线拟合未过门"));

        if (bitPackedMask is { Length: > 0 } && maskWidth > 0 && maskHeight > 0)
        {
            var hole = MaskTemplateMatcher.RefineByCentroidHoleLine(bitPackedMask, maskWidth, maskHeight);
            list.Add(hole is null
                ? new(SegmentRefineMethod.CentroidHoleLine, false, true, 0, "掩码内无稳定孔/槽")
                : new(SegmentRefineMethod.CentroidHoleLine, true, true, hole.Quality,
                    $"质心-内标连线过门（质量 {hole.Quality:0.00}）"));
        }
        else
            list.Add(new(SegmentRefineMethod.CentroidHoleLine, false, true, 0, "无分割掩码，未跑孔槽"));

        var caliper = MaskCaliperTab.TryRefine(bgr, contour, CaliperRefineOptions.From(options));
        if (caliper.Pose is not null)
        {
            var q = MaskCaliperTab.QualityScore(MaskCaliperTab.LastDebug);
            list.Add(new(SegmentRefineMethod.CaliperTab, true, true, q,
                $"卡尺过门（平行差 {MaskCaliperTab.LastDebug.ParallelDeg:0.00}°）"));
        }
        else
            list.Add(new(SegmentRefineMethod.CaliperTab, false, true, 0, "卡尺未过门（无边或极性冲突）"));

        if (template is null || template.Empty())
        {
            list.Add(new(SegmentRefineMethod.Template, false, true, 0, "未示教模板，未跑匹配"));
            return list;
        }

        try
        {
            var crop = MaskTemplateMatcher.UprightCrop(bgr, contour, 0.15);
            using (crop.Upright)
            {
                var range = MaskHousing.AdaptiveRefineRange(options?.RefineRangeDeg ?? 5, housing);
                var minScore = options is { MatchThreshold: > 0 } ? options.MatchThreshold : 0.6;
                var match = options?.UseEdgeMatch == true
                    ? MaskTemplateMatcher.MatchBestHybrid(crop.Upright, template, range, 0.01)
                    : MaskTemplateMatcher.MatchBest(crop.Upright, template, range, 0.01);
                if (match is null)
                    list.Add(new(SegmentRefineMethod.Template, false, true, 0, "模板匹配无峰"));
                else
                    list.Add(new(SegmentRefineMethod.Template, match.Score >= minScore, true, match.Score,
                        match.Score >= minScore
                            ? $"模板过门（NCC {match.Score:0.00}）"
                            : $"模板分 {match.Score:0.00} 低于阈值 {minScore:0.00}"));
            }
        }
        catch (InvalidOperationException)
        {
            list.Add(new(SegmentRefineMethod.Template, false, true, 0, "转正裁剪失败"));
        }

        return list;
    }

    /// <summary>
    /// 赛马取胜：有向优先；默认同序孔槽 &gt; 卡尺 &gt; 模板 &gt; 直线。
    /// 仅当最高分比该次序胜出至少高 0.08 时改用最高分（分数不可比时避免模板 NCC 压过卡尺）。
    /// </summary>
    public static SegmentRefineCandidate? PickWinner(IReadOnlyList<SegmentRefineCandidate> candidates)
    {
        var ok = candidates.Where(c => c.Ok && c.Score >= 0.35).ToList();
        if (ok.Count == 0)
            return null;

        int Rank(SegmentRefineMethod m) => m switch
        {
            SegmentRefineMethod.CentroidHoleLine => 0,
            SegmentRefineMethod.CaliperTab => 1,
            SegmentRefineMethod.Template => 2,
            _ => 3,
        };

        var directed = ok.Where(c => c.Directed).ToList();
        var pool = directed.Count > 0 ? directed : ok;
        var policy = pool.OrderBy(c => Rank(c.Method)).ThenByDescending(c => c.Score).First();
        var byScore = pool.OrderByDescending(c => c.Score).ThenBy(c => Rank(c.Method)).First();
        return byScore.Score >= policy.Score + 0.08 ? byScore : policy;
    }
}
