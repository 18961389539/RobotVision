using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;
using RobotVision.JlVision;

namespace RobotVision.Teach;

/// <summary>同一帧多路精修（最多六条）的实测结果（配方页赛马，不进 TRIGGER）。</summary>
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "ownedNcc/ownedShape 在 finally 释放；缓存模型由 TeachCache 持有。")]
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
        var list = new List<SegmentRefineCandidate>(6);
        var pts = contour as Point2f[] ?? [.. contour];
        var housing = JlHousing.Fit(pts);

        var line = JlPoseAlign.TryLineFit(pts, housing.LongAxisDeg);
        if (line.Fitted)
        {
            var residual = Math.Abs(AngleGeometry.UndirectedDeltaDeg(line.AngleDeg, housing.LongAxisDeg));
            var score = Math.Clamp(1.0 - residual / 5.0, 0.2, 1);
            list.Add(new(SegmentRefineMethod.LineFit, true, false, score,
                TeachNarrator.LineFitOk(residual), line.AngleDeg));
        }
        else
            list.Add(new(SegmentRefineMethod.LineFit, false, false, 0, TeachNarrator.LineFitMiss));

        if (bitPackedMask is { Length: > 0 } && maskWidth > 0 && maskHeight > 0)
        {
            var hole = JlCentroidHole.TryRefine(bitPackedMask, maskWidth, maskHeight);
            list.Add(hole is null
                ? new(SegmentRefineMethod.CentroidHoleLine, false, true, 0, TeachNarrator.CentroidHoleMiss)
                : new(SegmentRefineMethod.CentroidHoleLine, true, true, hole.Value.Quality,
                    TeachNarrator.CentroidHoleOk(hole.Value.Quality), hole.Value.AngleDeg));
        }
        else
            list.Add(new(SegmentRefineMethod.CentroidHoleLine, false, true, 0, TeachNarrator.CentroidHoleSkip,
                Skipped: true));

        try
        {
            using var scene = JlImageConvert.FromGrayMat(bgr);
            var polarity = options?.HousingEdgePolarity ?? HousingEdgePolarity.Auto;
            var caliper = JlMeasureRefine.TryRefine(scene, pts, polarity);
            if (caliper.Found)
                list.Add(new(SegmentRefineMethod.CaliperTab, true, true, caliper.Score,
                    "JLVision 卡尺 " + caliper.Note, caliper.AngleDeg));
            else
                list.Add(new(SegmentRefineMethod.CaliperTab, false, true, 0, TeachNarrator.CaliperMiss));
        }
        catch
        {
            list.Add(new(SegmentRefineMethod.CaliperTab, false, true, 0, TeachNarrator.CaliperMiss));
        }

        if (template is null || template.Empty())
        {
            list.Add(new(SegmentRefineMethod.Template, false, true, 0, TeachNarrator.TemplateSkip, Skipped: true));
            list.Add(new(SegmentRefineMethod.ShapeMatch, false, true, 0, TeachNarrator.ShapeSkip, Skipped: true));
            list.Add(new(SegmentRefineMethod.Sift, false, true, 0, TeachNarrator.SiftSkip, Skipped: true));
            return list;
        }

        var range = JlHousing.AdaptiveRefineRange(options?.RefineRangeDeg ?? 5, housing);
        var minScore = options is { MatchThreshold: > 0 } ? options.MatchThreshold : 0.6;
        JlNCCModel? ownedNcc = null;
        JlShapeModel? ownedShape = null;
        try
        {
            using var grayTpl = JlImageConvert.ToGray(template);
            ownedNcc = teachCache?.Ncc is null ? JlNccRefine.CreateModel(grayTpl) : null;
            ownedShape = teachCache?.Shape is null ? JlShapeRefine.CreateModel(grayTpl) : null;
            var ncc = teachCache?.Ncc ?? ownedNcc!;
            var shape = teachCache?.Shape ?? ownedShape!;
            using var scene = JlImageConvert.FromGrayMat(bgr);
            var nccHit = JlNccRefine.TryRefine(scene, pts, ncc, range, 0.01, JlFindOptions.ProductDefault);
            if (!nccHit.Found)
                list.Add(new(SegmentRefineMethod.Template, false, true, 0, TeachNarrator.TemplateNoPeak));
            else
            {
                var passed = nccHit.Score >= minScore;
                list.Add(new(SegmentRefineMethod.Template, passed, true, nccHit.Score,
                    TeachNarrator.TemplatePeak(nccHit.Score, minScore, passed), nccHit.AngleDeg));
            }

            var shapeHit = JlShapeRefine.TryRefine(
                scene, pts, shape, range, JlShapeDefaults.FindMinScore, JlFindOptions.ProductDefault);
            if (!shapeHit.Found)
                shapeHit = JlGeometryFallback.TryRefine(scene, pts, options?.HousingEdgePolarity ?? HousingEdgePolarity.Auto);
            if (!shapeHit.Found)
                list.Add(new(SegmentRefineMethod.ShapeMatch, false, true, 0, "JLVision 形状未命中"));
            else
                list.Add(new(SegmentRefineMethod.ShapeMatch, true, true, shapeHit.Score,
                    "JLVision shape " + shapeHit.Note, shapeHit.AngleDeg));

            list.Add(new(SegmentRefineMethod.Sift, shapeHit.Found, true, shapeHit.Found ? shapeHit.Score : 0,
                shapeHit.Found ? "JLVision shape（SIFT 已并入形状匹配）" : TeachNarrator.SiftMiss,
                shapeHit.Found ? shapeHit.AngleDeg : double.NaN));
        }
        catch (Exception)
        {
            list.Add(new(SegmentRefineMethod.Template, false, true, 0, TeachNarrator.TemplateCropFail));
            list.Add(new(SegmentRefineMethod.ShapeMatch, false, true, 0, "JLVision 形状异常"));
            list.Add(new(SegmentRefineMethod.Sift, false, true, 0, TeachNarrator.SiftMiss));
        }
        finally
        {
            ownedNcc?.Dispose();
            ownedShape?.Dispose();
        }

        return list;
    }

    /// <summary>回放打分时复用 JL 示教模型，避免每帧重建。</summary>
    public sealed class TeachCache : IDisposable
    {
        public JlShapeModel? Shape { get; }
        public JlNCCModel? Ncc { get; }

        public TeachCache(JlShapeModel? shape, JlNCCModel? ncc)
        {
            Shape = shape;
            Ncc = ncc;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "模型所有权交给 TeachCache，由 Dispose 释放。")]
        public static TeachCache? TryCreate(Mat? template)
        {
            if (template is null || template.Empty())
                return null;
            using var gray = JlImageConvert.ToGray(template);
            return new TeachCache(JlShapeRefine.CreateModel(gray), JlNccRefine.CreateModel(gray));
        }

        public void Dispose()
        {
            Shape?.Dispose();
            Ncc?.Dispose();
        }
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

            var directed = rows.Count > 0 && rows.Exists(c => c.Directed);
            var okRows = rows.Where(c => c.Ok).ToList();
            var agg = RaceScore.Compute(okRows, n, directed);
            var skipped = rows.Count > 0 && rows.TrueForAll(c => c.Skipped);
            result.Add(new SegmentRefineCandidate(
                method, agg.Ok, directed, agg.Score, TeachNarrator.RaceSummary(agg),
                agg.SampleAngleDeg, agg.AngleStdDeg, skipped));
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
        var ok = candidates.Where(c => c.Ok && c.Score >= RaceScore.OkGate).ToList();
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
        if (double.IsFinite(policy.AngleStdDeg) && policy.AngleStdDeg > TeachThresholds.AngleStdUnstableDeg)
        {
            var stable = pool
                .Where(c => double.IsFinite(c.AngleStdDeg) && c.AngleStdDeg < TeachThresholds.AngleStdStableDeg)
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

        return byScore.Score >= policy.Score + TeachThresholds.WinMarginScore ? byScore : policy;
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
