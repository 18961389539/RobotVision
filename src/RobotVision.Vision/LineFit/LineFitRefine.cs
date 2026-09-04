using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.Vision;

/// <summary>
/// LineFit 统一精修入口：轮廓鲁棒拟合 → 可选亚像素四边卡尺 → 头尾明暗判决（有向角）。
/// 亚像素与头尾分辨均由此类对外提供，调用方勿再直接使用分散的子步骤 API。
/// </summary>
public static class LineFitRefine
{
    private const double EndProbeAlongHalfLong = 0.7;
    private const double ProbeHalfWidthShortRatio = 0.35;

    /// <summary>LineFit 精修输出：位姿、有向角是否定出、头端点与质量备注。</summary>
    public sealed record Result(
        double AngleDeg,
        Point2d Center,
        double Score,
        bool HeadingResolved,
        Point2d? HeadPoint,
        string? QualityNote);

    /// <summary>
    /// 分割轮廓 + 原图 ROI 全链路精修。轮廓拟合失败时 <see cref="Result.Score"/> 为 0 且角度不可用。
    /// </summary>
    public static Result Refine(
        Mat image,
        IReadOnlyList<Point2f> contour,
        TemplateOptions template,
        string recipeName = "")
    {
        var housing = MaskHousing.Fit(contour);
        var options = RectFitOptions.ForLineFit(template);
        var core = MaskHousing.CorePoints(contour);
        var contourFit = RotatedRectFitter.Fit(core, housing.LongAxisDeg, options);
        if (!contourFit.Ok)
            return new Result(0, default, 0, false, null, null);

        var angle = contourFit.AngleDeg;
        var center = contourFit.Center;
        var longLen = contourFit.LongLen;
        var shortLen = contourFit.ShortLen;
        string? note = null;

        using var gray = ToGray(image);
        if (template.LineFitSubpixel)
        {
            var subOptions = options;
            if (template.LineFitFixAngleDuringSubpixel)
            {
                subOptions = options with
                {
                    Constraints = options.Constraints with { FixedAngleDeg = contourFit.AngleDeg },
                };
            }

            if (RotatedRectSubpixel.Refine(gray, center, longLen, shortLen, angle, subOptions) is { } s)
            {
                angle = s.AngleDeg;
                center = s.Center;
                longLen = s.LongLen;
                shortLen = s.ShortLen;
                var quality = RotatedRectFitQuality.FromSubpixel(s);
                var mode = options.EdgeMeasureMode == RectEdgeMeasureMode.Fuzzy ? "模糊边" : "锐边";
                note = $"rectangle2·亚像素四边（{mode}，平行差 {s.MaxParallelDeg:0.0}°，RMS {s.RmsPx:0.00}px，Q {quality.Score:0.00}）";
            }
            else
            {
                note = "rectangle2·亚像素质量不过门，回退轮廓拟合";
            }
        }

        var residual = Math.Abs(AngleGeometry.UndirectedDeltaDeg(angle, housing.LongAxisDeg));
        var score = Math.Clamp(1.0 - residual / 5.0, 0.2, 1);

        var probeHousing = new HousingFrame(
            new Point2f((float)center.X, (float)center.Y),
            angle,
            longLen,
            shortLen);
        var heading = ResolveHeading(gray, probeHousing, angle, template.RefineLine);
        if (!heading.Resolved)
        {
            var headingNote = string.IsNullOrEmpty(recipeName)
                ? $"直线拟合 {heading.Note}"
                : $"{recipeName} · 直线拟合 {heading.Note}";
            return new Result(angle, center, score, false, null, JoinNote(note, headingNote));
        }

        return new Result(heading.DirectedDeg, center, score, true, heading.HeadPoint, note);
    }

    /// <summary>
    /// 沿壳体长轴自动采头尾明暗，构建示教基准线（较亮一端为头 P2）。供配方页回填或默认头尾签名。
    /// </summary>
    public static RefineLine? TryBuildRefineLine(Mat gray, HousingFrame housing, double undirectedDeg)
    {
        if (gray is null || gray.Empty() || gray.Channels() != 1)
            return null;

        if (!TrySampleEndMeans(gray, housing, undirectedDeg, out var plusMean, out var minusMean,
                out var plusPoint, out var minusPoint))
            return null;

        var headMinusTail = plusMean - minusMean;
        if (Math.Abs(headMinusTail) < RefineLine.MinFlipContrastGray)
            return null;

        var headAtPlus = headMinusTail > 0;
        var head = headAtPlus ? plusPoint : minusPoint;
        var tail = headAtPlus ? minusPoint : plusPoint;
        var w = Math.Max(1, gray.Width);
        var h = Math.Max(1, gray.Height);
        return new RefineLine(
            Math.Clamp(tail.X / w, 0, 1),
            Math.Clamp(tail.Y / h, 0, 1),
            Math.Clamp(head.X / w, 0, 1),
            Math.Clamp(head.Y / h, 0, 1),
            headMinusTail);
    }

    internal readonly record struct SubpixelView(
        double AngleDeg, Point2d Center, double LongLen, double ShortLen, double MaxParallelDeg, double RmsPx);

    /// <summary>单测：亚像素四边卡尺（种子矩形 → 精修角/中心/尺寸）。</summary>
    internal static SubpixelView? TrySubpixel(
        Mat gray,
        Point2d center,
        double longLen,
        double shortLen,
        double seedAngleDeg,
        RectFitOptions? options = null)
    {
        if (gray is null || gray.Empty() || gray.Channels() != 1)
            return null;

        var sub = RotatedRectSubpixel.Refine(gray, center, longLen, shortLen, seedAngleDeg, options ?? RectFitOptions.Default);
        if (sub is null)
            return null;

        return new SubpixelView(
            sub.Value.AngleDeg,
            sub.Value.Center,
            sub.Value.LongLen,
            sub.Value.ShortLen,
            sub.Value.MaxParallelDeg,
            sub.Value.RmsPx);
    }

    internal readonly record struct HeadingView(bool Resolved, double DirectedDeg, Point2d HeadPoint, string? Note);

    /// <summary>单测：头尾明暗判决（无向角 → 有向角）。</summary>
    internal static HeadingView ResolveHeading(
        Mat gray,
        HousingFrame housing,
        double undirectedDeg,
        RefineLine? line)
    {
        var center = new Point2d(housing.Center.X, housing.Center.Y);
        line ??= TryBuildRefineLine(gray, housing, undirectedDeg);
        if (line is null)
            return new HeadingView(false, undirectedDeg, center, "头尾明暗差不足或探针越界，180° 未定，保持无向");

        if (!line.HasReliableSignature)
            return new HeadingView(false, undirectedDeg, center, "示教基准线头尾明暗差过小（对称件），180° 未定，保持无向");

        if (!TrySampleEndMeans(gray, housing, undirectedDeg, out var plusMean, out var minusMean,
                out var headCandidate, out var tailCandidate))
            return new HeadingView(false, undirectedDeg, center, "基准线判决：端头探针落在图像外，180° 未定");

        var measured = plusMean - minusMean;
        if (Math.Abs(measured) < RefineLine.MinFlipContrastGray)
        {
            return new HeadingView(false, undirectedDeg, center,
                $"端头实测明暗差仅 {measured:0.0}，不足 {RefineLine.MinFlipContrastGray:0}，180° 未定");
        }

        var headAtPlus = Math.Sign(measured) == Math.Sign(line.HeadMinusTailGray);
        var directed = AngleGeometry.NormalizeSignedDeg(headAtPlus ? undirectedDeg : undirectedDeg + 180.0);
        var headPoint = headAtPlus ? headCandidate : tailCandidate;
        return new HeadingView(true, directed, headPoint, null);
    }

    private static bool TrySampleEndMeans(
        Mat gray,
        HousingFrame housing,
        double undirectedDeg,
        out double plusMean,
        out double minusMean,
        out Point2d plusPoint,
        out Point2d minusPoint)
    {
        plusMean = minusMean = 0;
        plusPoint = minusPoint = default;

        var rad = undirectedDeg * Math.PI / 180.0;
        var dir = new Point2d(Math.Cos(rad), Math.Sin(rad));
        var halfLong = housing.LongLen * 0.5 * EndProbeAlongHalfLong;
        var probeHalf = Math.Clamp(housing.ShortLen * 0.5 * ProbeHalfWidthShortRatio, 3, 40);
        probeHalf = Math.Min(probeHalf, Math.Max(3, housing.ShortLen * 0.5));

        plusPoint = new Point2d(housing.Center.X, housing.Center.Y) + dir * halfLong;
        minusPoint = new Point2d(housing.Center.X, housing.Center.Y) - dir * halfLong;

        return TryMeanGray(gray, plusPoint, probeHalf, out plusMean) &&
               TryMeanGray(gray, minusPoint, probeHalf, out minusMean);
    }

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
        mean = Cv2.Mean(window).Val0;
        return true;
    }

    private static Mat ToGray(Mat view)
    {
        if (view.Channels() == 1)
            return view.Clone();

        var gray = new Mat();
        Cv2.CvtColor(view, gray, view.Channels() == 4
            ? ColorConversionCodes.BGRA2GRAY
            : ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static string JoinNote(string? a, string b) => string.IsNullOrEmpty(a) ? b : a + "；" + b;
}
