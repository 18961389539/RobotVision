using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.Vision.Inference.Strategies;

/// <summary>
/// LineFit 有向角判决：在「无向精修角 [0,180)」已给出的前提下，用示教基准线的头尾明暗签名
/// （<see cref="RefineLine.HeadMinusTailGray"/>）消 180° 歧义，输出有向角 [0,360)。
///
/// 原理：无向角已确定长轴这条「线」，剩下只有「箭头朝哪一端」两种可能。把两条候选方向的端头
/// 各放一个明暗探针窗，实测哪一端更像示教时画线终点（头）那一端，即定为头。若零件两端明暗几乎
/// 无差别（对称件 / 无可靠示教签名 / 现场光照把差抹平），判为不可定，保持无向并交上层提示。
/// </summary>
public static class LineFitHeading
{
    /// <summary>探针窗中心沿长轴离质心的距离（占长半轴比例）：0.7×半长≈落在两端内侧，躲开角点。</summary>
    private const double EndProbeAlongHalfLong = 0.7;

    /// <summary>探针窗半宽占短半轴比例，并夹在 [3, 40]px。</summary>
    private const double ProbeHalfWidthShortRatio = 0.35;

    /// <summary>判决结果：有向角（仅 <see cref="Resolved"/> 为 true 时有效）+ 头端点（供箭头回显）+ 说明。</summary>
    public readonly record struct Result(bool Resolved, double DirectedDeg, Point2d HeadPoint, string? Note);

    /// <summary>
    /// 尝试用示教基准线把无向角 <paramref name="undirectedDeg"/> 定成有向角。
    /// <paramref name="housing"/> 是当前实例壳体（质心 + 无向长轴 + 长短轴长），<paramref name="gray"/> 为精修所用图。
    /// 返回 <see cref="Result.Resolved"/>=false 时保持无向（<see cref="Result.DirectedDeg"/> 原样返回入参）。
    /// </summary>
    public static Result Resolve(Mat gray, HousingFrame housing, double undirectedDeg, RefineLine? line)
    {
        if (line is null)
            return new Result(false, undirectedDeg, housing.Center.ToDouble(), null);

        if (!line.HasReliableSignature)
            return new Result(false, undirectedDeg, housing.Center.ToDouble(),
                "示教基准线头尾明暗差过小（对称件），180° 未定，保持无向");

        var rad = undirectedDeg * Math.PI / 180.0;
        var dir = new Point2d(Math.Cos(rad), Math.Sin(rad));
        var halfLong = housing.LongLen * 0.5 * EndProbeAlongHalfLong;
        var probeHalf = Math.Clamp(housing.ShortLen * 0.5 * ProbeHalfWidthShortRatio, 3, 40);
        // 探针沿短轴方向也限制一下，别越出壳体短边
        probeHalf = Math.Min(probeHalf, Math.Max(3, housing.ShortLen * 0.5));

        var headCandidate = housing.Center.ToDouble() + dir * halfLong;   // +长轴端
        var tailCandidate = housing.Center.ToDouble() - dir * halfLong;   // −长轴端

        if (!TryMeanGray(gray, headCandidate, probeHalf, out var plusMean) ||
            !TryMeanGray(gray, tailCandidate, probeHalf, out var minusMean))
            return new Result(false, undirectedDeg, housing.Center.ToDouble(),
                "基准线判决：端头探针落在图像外，180° 未定");

        var measured = plusMean - minusMean;
        if (Math.Abs(measured) < RefineLine.MinFlipContrastGray)
            return new Result(false, undirectedDeg, housing.Center.ToDouble(),
                $"端头实测明暗差仅 {measured:0.0}，不足 {RefineLine.MinFlipContrastGray:0}，180° 未定");

        // 示教：head − tail 的符号；实测：(+长轴端) − (−长轴端) 的符号。
        // 同号 → 头在 +长轴端，有向角 = undirectedDeg；异号 → 头在 −长轴端，翻转 180°。
        var headAtPlus = Math.Sign(measured) == Math.Sign(line.HeadMinusTailGray);
        var directed = AngleGeometry.NormalizeSignedDeg(headAtPlus ? undirectedDeg : undirectedDeg + 180.0);
        var headPoint = headAtPlus ? headCandidate : tailCandidate;
        return new Result(true, directed, headPoint, null);
    }

    /// <summary>以 (cx,cy) 为中心、半宽 <paramref name="half"/> 的正方形窗求平均灰度；越界裁剪，退化则 false。</summary>
    private static bool TryMeanGray(Mat gray, Point2d center, double half, out double mean)
    {
        mean = 0;
        var x0 = (int)Math.Round(center.X - half);
        var y0 = (int)Math.Round(center.Y - half);
        var x1 = (int)Math.Round(center.X + half);
        var y1 = (int)Math.Round(center.Y + half);
        x0 = Math.Clamp(x0, 0, gray.Width - 1);
        y0 = Math.Clamp(y0, 0, gray.Height - 1);
        x1 = Math.Clamp(x1, x0 + 1, gray.Width);
        y1 = Math.Clamp(y1, y0 + 1, gray.Height);
        if (x1 - x0 < 2 || y1 - y0 < 2)
            return false;

        using var window = gray.SubMat(new Rect(x0, y0, x1 - x0, y1 - y0));
        var avg = Cv2.Mean(window);
        mean = avg.Val0;
        return true;
    }

    private static Point2d ToDouble(this Point2f p) => new(p.X, p.Y);
}
