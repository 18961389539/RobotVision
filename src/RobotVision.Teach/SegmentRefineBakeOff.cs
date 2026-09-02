using System.Diagnostics.CodeAnalysis;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;
using RobotVision.Vision.Inference.Strategies;

namespace RobotVision.Teach;

/// <summary>同一帧四条精修路径的实测结果（配方页赛马，不进 TRIGGER）。</summary>
public sealed record SegmentRefineCandidate(
    SegmentRefineMethod Method,
    bool Ok,
    bool Directed,
    double Score,
    string Note,
    double AngleDeg = double.NaN,
    double AngleStdDeg = double.NaN,
    bool Skipped = false);

/// <summary>对同一分割目标跑卡尺 / 直线 / 孔槽 / 模板，给出可比较的精修分。</summary>
public static class SegmentRefineBakeOff
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Locally built SIFT teach models disposed in finally when not cache-owned.")]
    public static IReadOnlyList<SegmentRefineCandidate> Run(
        Mat bgr,
        IReadOnlyList<Point2f> contour,
        byte[]? bitPackedMask = null,
        int maskWidth = 0,
        int maskHeight = 0,
        Mat? template = null,
        TemplateOptions? options = null,
        TeachCache? teachCache = null)
    {
        var list = new List<SegmentRefineCandidate>(4);
        var housing = MaskHousing.Fit(contour);

        var line = MaskTemplateMatcher.RefineByLineFit(contour, housing.LongAxisDeg);
        if (line.Fitted)
        {
            var residual = Math.Abs(AngleGeometry.UndirectedDeltaDeg(line.AngleDeg, housing.LongAxisDeg));
            var score = Math.Clamp(1.0 - residual / 5.0, 0.2, 1);
            list.Add(new(SegmentRefineMethod.LineFit, true, false, score,
                $"直线拟合过门（残差 {residual:0.00}°）", line.AngleDeg));
        }
        else
            list.Add(new(SegmentRefineMethod.LineFit, false, false, 0, "直线拟合未过门"));

        if (bitPackedMask is { Length: > 0 } && maskWidth > 0 && maskHeight > 0)
        {
            var hole = MaskTemplateMatcher.RefineByCentroidHoleLine(bitPackedMask, maskWidth, maskHeight);
            list.Add(hole is null
                ? new(SegmentRefineMethod.CentroidHoleLine, false, true, 0, "掩码内无稳定孔/槽")
                : new(SegmentRefineMethod.CentroidHoleLine, true, true, hole.Quality,
                    $"质心-内标连线过门（质量 {hole.Quality:0.00}）", hole.AngleDeg));
        }
        else
            list.Add(new(SegmentRefineMethod.CentroidHoleLine, false, true, 0, "无分割掩码，未跑孔槽",
                Skipped: true));

        var caliper = MaskCaliperTab.TryRefine(bgr, contour, CaliperRefineOptions.From(options));
        if (caliper.Pose is not null)
        {
            var q = MaskCaliperTab.QualityScore(MaskCaliperTab.LastDebug);
            list.Add(new(SegmentRefineMethod.CaliperTab, true, true, q,
                $"卡尺过门（平行差 {MaskCaliperTab.LastDebug.ParallelDeg:0.00}°）",
                caliper.Pose.AngleDeg));
        }
        else
            list.Add(new(SegmentRefineMethod.CaliperTab, false, true, 0, "卡尺未过门（无边或凸起不可判）"));

        if (template is null || template.Empty())
        {
            list.Add(new(SegmentRefineMethod.Template, false, true, 0, "未示教模板，未跑匹配", Skipped: true));
            list.Add(new(SegmentRefineMethod.ShapeMatch, false, true, 0, "未示教模板，未跑形状匹配", Skipped: true));
            list.Add(new(SegmentRefineMethod.Sift, false, true, 0, "未示教模板，未跑 SIFT", Skipped: true));
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
                            : $"模板分 {match.Score:0.00} 低于阈值 {minScore:0.00}",
                        AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + match.RotationDeg)));
            }
        }
        catch (InvalidOperationException)
        {
            list.Add(new(SegmentRefineMethod.Template, false, true, 0, "转正裁剪失败"));
        }

        var shapeModel = teachCache?.Shape ?? MaskShapeMatch.BuildTeach(template);
        if (shapeModel is null)
            list.Add(new(SegmentRefineMethod.ShapeMatch, false, true, 0, "示教图边缘太少，未跑形状匹配"));
        else
        {
            var range = MaskHousing.AdaptiveRefineRange(options?.RefineRangeDeg ?? 5, housing);
            var shape = MaskShapeMatch.TryRefine(bgr, contour, shapeModel, range);
            if (shape.Pose is null)
                list.Add(new(SegmentRefineMethod.ShapeMatch, false, true, 0,
                    $"形状匹配未过门（命中 {MaskShapeMatch.LastDebug.HitRate:0.00} 均距 {MaskShapeMatch.LastDebug.MeanDist:0.00}px）"));
            else
                list.Add(new(SegmentRefineMethod.ShapeMatch, true, true, shape.Pose.Score,
                    $"形状匹配过门（命中 {shape.Pose.HitRate:0.00} 均距 {shape.Pose.MeanDistPx:0.00}px）",
                    shape.Pose.AngleDeg));
        }

        var siftOwned = teachCache is null;
        MaskSiftRefine.TeachModel? siftTeach = teachCache?.Sift;
        if (siftTeach is null)
            siftTeach = MaskSiftRefine.BuildTeach(template);
        try
        {
            if (siftTeach is null)
            {
                list.Add(new(SegmentRefineMethod.Sift, false, true, 0, "示教图 SIFT 特征太少"));
            }
            else
            {
                var sift = MaskSiftRefine.TryRefine(bgr, contour, siftTeach);
                if (sift.Pose is null)
                    list.Add(new(SegmentRefineMethod.Sift, false, true, 0, "SIFT 未过门（匹配点不够）"));
                else
                    list.Add(new(SegmentRefineMethod.Sift, true, true, sift.Pose.Score,
                        $"SIFT 过门（内点 {sift.Pose.Inliers}/{sift.Pose.Matches}）",
                        sift.Pose.AngleDeg));
            }
        }
        finally
        {
            if (siftOwned)
                siftTeach?.Dispose();
        }

        return list;
    }

    /// <summary>回放打分时复用示教模型，避免每帧重建 SIFT / 形状点集。</summary>
    public sealed class TeachCache : IDisposable
    {
        public MaskShapeMatch.ShapeModel? Shape { get; }
        public MaskSiftRefine.TeachModel? Sift { get; }

        public TeachCache(MaskShapeMatch.ShapeModel? shape, MaskSiftRefine.TeachModel? sift)
        {
            Shape = shape;
            Sift = sift;
        }

        public static TeachCache? TryCreate(Mat? template)
        {
            if (template is null || template.Empty())
                return null;
            return new TeachCache(MaskShapeMatch.BuildTeach(template), MaskSiftRefine.BuildTeach(template));
        }

        public void Dispose() => Sift?.Dispose();
    }

    /// <summary>
    /// 多帧赛马汇总：分为「过门率 × 过门均分 × 角度一致性」。
    /// 一致性 = 1 − clamp(角σ / 8°)，无角度样本时为 1。空列表计入分母。
    /// </summary>
    public static IReadOnlyList<SegmentRefineCandidate> Aggregate(
        IReadOnlyList<IReadOnlyList<SegmentRefineCandidate>> frames)
    {
        if (frames.Count == 0)
            return [];

        var methods = new List<SegmentRefineMethod>();
        foreach (var frame in frames)
        {
            foreach (var row in frame)
            {
                if (!methods.Contains(row.Method))
                    methods.Add(row.Method);
            }
        }

        var n = frames.Count;
        var result = new List<SegmentRefineCandidate>(methods.Count);
        foreach (var method in methods)
        {
            var rows = new List<SegmentRefineCandidate>(n);
            foreach (var frame in frames)
            {
                foreach (var row in frame)
                {
                    if (row.Method != method)
                        continue;
                    rows.Add(row);
                    break;
                }
            }

            var okRows = rows.Where(c => c.Ok).ToList();
            var directed = rows.Count > 0 && rows.Exists(c => c.Directed);
            var meanOk = okRows.Count == 0 ? 0 : okRows.Average(c => c.Score);
            var period = directed ? 360.0 : 180.0;
            var angles = okRows.Where(c => double.IsFinite(c.AngleDeg)).Select(c => c.AngleDeg).ToList();
            var std = AngleGeometry.CircularStdDeg(angles, period);
            var consistency = angles.Count < 2 ? 1.0 : Math.Clamp(1.0 - std / 8.0, 0, 1);
            var score = (okRows.Count / (double)n) * meanOk * consistency;
            var ok = okRows.Count > 0 && score >= 0.35;
            string note;
            if (okRows.Count == 0)
                note = $"{n} 帧均未过门";
            else if (angles.Count < 2)
                note = $"{okRows.Count}/{n} 过门，均分 {meanOk:0.00}";
            else
                note = $"{okRows.Count}/{n} 过门，均分 {meanOk:0.00}，角σ {std:0.00}°";
            var meanAngle = angles.Count == 0 ? double.NaN : angles[0];
            var stdStored = angles.Count < 2 ? double.NaN : std;
            var skipped = rows.Count > 0 && rows.TrueForAll(c => c.Skipped);
            result.Add(new SegmentRefineCandidate(method, ok, directed, score, note, meanAngle, stdStored, skipped));
        }

        return result;
    }

    /// <summary>
    /// 赛马取胜：有向优先；默认同序孔槽 &gt; 卡尺 &gt; 模板 &gt; 形状匹配 &gt; SIFT &gt; 直线（可用 <paramref name="policyOrder"/> 覆盖）。
    /// 仅当最高分比该次序胜出至少高 0.08 时改用最高分。SIFT/形状匹配与灰度 NCC/卡尺质量不可比，除非双方都有整夹角σ（过门率×稳定性已进复合分）。
    /// 若政策项整夹角σ &gt; 8°，改选角σ &lt; 4° 的过门项。
    /// </summary>
    public static SegmentRefineCandidate? PickWinner(
        IReadOnlyList<SegmentRefineCandidate> candidates,
        IReadOnlyList<SegmentRefineMethod>? policyOrder = null,
        SegmentRefineMethod? downrank = null)
    {
        var ok = candidates.Where(c => c.Ok && c.Score >= 0.35).ToList();
        if (ok.Count == 0)
            return null;

        int Rank(SegmentRefineMethod m)
        {
            var r = DefaultRank(m);
            if (policyOrder is { Count: > 0 })
            {
                var i = -1;
                for (var k = 0; k < policyOrder.Count; k++)
                {
                    if (policyOrder[k] == m)
                    {
                        i = k;
                        break;
                    }
                }

                r = i >= 0 ? i : 100 + DefaultRank(m);
            }

            if (downrank is { } d && m == d)
                r += 8;
            return r;
        }

        var directed = ok.Where(c => c.Directed).ToList();
        var pool = directed.Count > 0 ? directed : ok;
        var policy = pool.OrderBy(c => Rank(c.Method)).ThenByDescending(c => c.Score).First();
        var byScore = pool.OrderByDescending(c => c.Score).ThenBy(c => Rank(c.Method)).First();
        if (double.IsFinite(policy.AngleStdDeg) && policy.AngleStdDeg > 8)
        {
            var stable = pool
                .Where(c => double.IsFinite(c.AngleStdDeg) && c.AngleStdDeg < 4)
                .OrderBy(c => c.AngleStdDeg)
                .ThenBy(c => Rank(c.Method))
                .FirstOrDefault();
            if (stable is not null)
                return stable;
        }

        if (downrank is { } down && byScore.Method == down && policy.Method != down)
            return policy;
        if (!ScoresComparable(byScore, policy))
            return policy;

        return byScore.Score >= policy.Score + 0.08 ? byScore : policy;
    }

    private static int DefaultRank(SegmentRefineMethod m) => m switch
    {
        SegmentRefineMethod.CentroidHoleLine => 0,
        SegmentRefineMethod.CaliperTab => 1,
        SegmentRefineMethod.Template => 2,
        SegmentRefineMethod.ShapeMatch => 3,
        SegmentRefineMethod.Sift => 4,
        _ => 5,
    };

    private static bool ScoresComparable(SegmentRefineCandidate a, SegmentRefineCandidate b)
    {
        static bool FeatureMatch(SegmentRefineMethod m) =>
            m is SegmentRefineMethod.Sift or SegmentRefineMethod.ShapeMatch;
        if (FeatureMatch(a.Method) == FeatureMatch(b.Method))
            return true;
        return double.IsFinite(a.AngleStdDeg) && double.IsFinite(b.AngleStdDeg);
    }
}
