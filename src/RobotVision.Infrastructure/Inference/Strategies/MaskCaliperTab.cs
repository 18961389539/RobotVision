using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>卡尺探针布局：齿列/缺口在上下时优先沿短轴布探针，抓左右两条长边。</summary>
public enum CaliperProbeLayout
{
    /// <summary>先上下长边，失败或质量差再试左右长边。</summary>
    Auto = 0,

    /// <summary>沿长轴布探针，短轴方向抓上下两条边（原行为）。</summary>
    AcrossShortAxis = 1,

    /// <summary>沿短轴布探针，长轴方向抓左右两条边（齿列在上、缺口在下时更稳）。</summary>
    AcrossLongAxis = 2,
}

/// <summary>示教锁定的边缘/凸起极性；缺省 Auto 与旧配方一致。</summary>
public sealed record CaliperRefineOptions(
    HousingEdgePolarity EdgePolarity = HousingEdgePolarity.Auto,
    TabPolarityLock TabPolarity = TabPolarityLock.Auto,
    CaliperProbeLayout ProbeLayout = CaliperProbeLayout.Auto)
{
    public static CaliperRefineOptions Default { get; } = new();

    public static CaliperRefineOptions From(TemplateOptions? template) =>
        template is null
            ? Default
            : new(template.HousingEdgePolarity, template.TabPolarity);
}

/// <summary>
/// 卡尺长边 + 凸起极性精修：分割只给粗框，原图 1D 剖面抓两条长边（亚像素），
/// XY 短轴取两线中线、长轴仍用粗中心投影；头尾看壳体边缘外侧哪一侧更暗（凸起）。
/// 约定与示教模板一致：转正后凸起在 +短轴（θ≈0° 时朝下）为 0°，对侧则 +180°。
/// 凸起侧必须每帧实测：无向长边在 [0,180) 换手时 ±短轴会对调，示教锁不能当同号门或回退。
/// </summary>
public static class MaskCaliperTab
{
    /// <summary>沿长轴平均半宽（px），压剖面噪声。</summary>
    private const int SamplingHalfWidth = 2;

    /// <summary>剖面梯度下限（两侧差分，约 2px 跨度）。</summary>
    private const double MinGradient = 10.0;

    /// <summary>每侧最少内点，否则回退。</summary>
    private const int MinInliersPerSide = 8;

    /// <summary>两线夹角超过此值视为抓到凸起/杂边。</summary>
    private const double MaxParallelDeg = 4.0;

    /// <summary>测得宽度相对粗短边的允许范围。</summary>
    private const double WidthRatioLo = 0.45;

    private const double WidthRatioHi = 1.75;

    /// <summary>凸起在边缘外侧的搜索长度相对短边。</summary>
    private const double TabSearchRatio = 0.35;

    private const double MinTabSearchPx = 12.0;

    /// <summary>两侧外侧最暗带灰度差低于此则改用轮廓外伸。</summary>
    private const double MinTabGrayDiff = 4.0;

    public sealed record Result(
        double AngleDeg,
        Point2d Center,
        int? TabSign,
        Point2d TabMarkerFrom,
        Point2d TabMarkerTo);

    /// <summary>卡尺搜索条 / 拟合边（ROI 像素坐标）。</summary>
    public readonly record struct Segment(Point2d A, Point2d B);

    /// <summary>一次精修的画面几何：成功、失败都尽量带齐，供配方测试叠加。</summary>
    public sealed record CaliperViz(
        IReadOnlyList<Segment> SearchBars,
        IReadOnlyList<Segment> InvalidBars,
        IReadOnlyList<Point2d> Inliers,
        IReadOnlyList<Point2d> Rejected,
        Segment? FittedMinus,
        Segment? FittedPlus)
    {
        public static readonly CaliperViz Empty = new([], [], [], [], null, null);
    }

    /// <summary>精修结果；<see cref="Pose"/> 为 null 时策略回退粗角，但 <see cref="Viz"/> 仍可画卡尺。</summary>
    public sealed record CaliperAttempt(Result? Pose, CaliperViz Viz)
    {
        public static CaliperAttempt Miss(CaliperViz? viz = null) => new(null, viz ?? CaliperViz.Empty);
    }

    internal readonly record struct DebugInfo(
        int ValidProbes,
        double ParallelDeg,
        double WidthPx,
        int? TabSign,
        double TabGrayDiff,
        double AngleUndirectedDeg,
        int ProbeCount);

    [ThreadStatic]
    internal static DebugInfo LastDebug;

    /// <summary>
    /// 在原图上按壳体框自动放置双边卡尺。失败返回 null（策略默认 1019，不再输出无向粗角）。
    /// 输入轮廓与图像均为同一坐标系（ROI 内）。
    /// </summary>
    public static Result? Refine(
        Mat image, IReadOnlyList<Point2f> contour, CaliperRefineOptions? options = null) =>
        TryRefine(image, contour, options).Pose;

    /// <summary>两线平行度 + 凸起灰度差，供赛马打分。</summary>
    internal static double QualityScore(DebugInfo d)
    {
        var parallel = Math.Clamp(1.0 - d.ParallelDeg / 4.0, 0, 1);
        var tab = Math.Clamp(Math.Abs(d.TabGrayDiff) / 20.0, 0, 1);
        return Math.Clamp(0.55 * parallel + 0.45 * tab, 0.15, 1);
    }

    /// <summary>与 <see cref="Refine"/> 相同，但失败也带回探针/抓边/拟合边，供画面调试。</summary>
    public static CaliperAttempt TryRefine(
        Mat image, IReadOnlyList<Point2f> contour, CaliperRefineOptions? options = null)
    {
        LastDebug = default;
        if (image.Empty() || contour.Count < 4)
            return CaliperAttempt.Miss();

        options ??= CaliperRefineOptions.Default;

        Mat gray = image;
        var owned = false;
        if (image.Channels() != 1)
        {
            gray = new Mat();
            owned = true;
            Cv2.CvtColor(image, gray, image.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY);
        }

        try
        {
            if (options.EdgePolarity == HousingEdgePolarity.Auto)
            {
                var bright = RefineWithLayout(gray, contour, brightToDark: true, options.ProbeLayout);
                if (bright.Pose is not null)
                    return bright;
                var dark = RefineWithLayout(gray, contour, brightToDark: false, options.ProbeLayout);
                return dark.Pose is not null ? dark : bright;
            }

            var brightToDark = options.EdgePolarity != HousingEdgePolarity.DarkToBright;
            return RefineWithLayout(gray, contour, brightToDark, options.ProbeLayout);
        }
        finally
        {
            if (owned)
                gray.Dispose();
        }
    }

    private static CaliperAttempt RefineWithLayout(
        Mat gray, IReadOnlyList<Point2f> contour, bool brightToDark, CaliperProbeLayout layout)
    {
        var work = NormalizeRoiContrast(gray, contour);
        try
        {
            return layout switch
            {
                CaliperProbeLayout.AcrossShortAxis => RefineGrayCore(work, contour, brightToDark, probeAlongLong: true),
                CaliperProbeLayout.AcrossLongAxis => RefineGrayCore(work, contour, brightToDark, probeAlongLong: false),
                _ => PickAutoLayout(work, contour, brightToDark),
            };
        }
        finally
        {
            if (!ReferenceEquals(work, gray))
                work.Dispose();
        }
    }

    private static CaliperAttempt PickAutoLayout(Mat gray, IReadOnlyList<Point2f> contour, bool brightToDark)
    {
        var housing = MaskHousing.Fit(contour);
        var topBottom = RefineGrayCore(gray, contour, brightToDark, probeAlongLong: true);
        var debugTb = LastDebug;
        var scoreTb = topBottom.Pose is null ? 0 : LayoutScore(debugTb, housing.ShortLen);

        var leftRight = RefineGrayCore(gray, contour, brightToDark, probeAlongLong: false);
        var debugLr = LastDebug;
        var scoreLr = leftRight.Pose is null ? 0 : LayoutScore(debugLr, housing.LongLen);

        if (topBottom.Pose is null && leftRight.Pose is not null)
        {
            LastDebug = debugLr;
            return leftRight;
        }

        if (leftRight.Pose is not null)
        {
            var rough = LongEdgeRoughness(contour, housing);
            // 只在上下长边失败，或长边明显是齿列/缺口时，才改抓左右短边。
            var preferLateral = topBottom.Pose is null
                                || rough >= 0.22 && scoreLr >= 0.82 * Math.Max(0.15, scoreTb);
            if (preferLateral)
            {
                LastDebug = debugLr;
                return leftRight;
            }
        }

        LastDebug = debugTb;
        return topBottom;
    }

    private static double LayoutScore(DebugInfo d, double expectedWidth)
    {
        var q = QualityScore(d);
        if (expectedWidth < 8 || d.WidthPx < 1)
            return q;
        var ratio = d.WidthPx / expectedWidth;
        var match = Math.Clamp(1.0 - Math.Abs(ratio - 1.0), 0.15, 1);
        return q * (0.55 + 0.45 * match);
    }


    /// <summary>ROI 内百分位拉伸，减轻现场亮度跌落对剖面梯度的影响。</summary>
    private static Mat NormalizeRoiContrast(Mat gray, IReadOnlyList<Point2f> contour)
    {
        if (gray.Empty() || contour.Count < 4)
            return gray;

        var rect = Cv2.BoundingRect(contour);
        rect.X = Math.Clamp(rect.X, 0, gray.Width - 1);
        rect.Y = Math.Clamp(rect.Y, 0, gray.Height - 1);
        rect.Width = Math.Min(rect.Width, gray.Width - rect.X);
        rect.Height = Math.Min(rect.Height, gray.Height - rect.Y);
        if (rect.Width < 8 || rect.Height < 8)
            return gray;

        using var roi = new Mat(gray, rect);
        var values = new List<byte>(roi.Rows * roi.Cols);
        for (var y = 0; y < roi.Rows; y++)
        for (var x = 0; x < roi.Cols; x++)
            values.Add(roi.At<byte>(y, x));
        values.Sort();
        var maxVal = values[^1];
        if (maxVal < 20)
            return gray;

        var lo = PercentileSorted(values, 0.05);
        var hi = PercentileSorted(values, 0.98);
        if (hi - lo < 12)
            return gray;

        var dst = new Mat();
        gray.ConvertTo(dst, MatType.CV_8UC1, 255.0 / (hi - lo), -lo * 255.0 / (hi - lo));
        return dst;
    }

    private static double PercentileSorted(List<byte> sorted, double p)
    {
        var idx = (int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1);
        return sorted[idx];
    }

    /// <param name="probeAlongLong">true=上下长边；false=左右长边（探针沿短轴、搜索沿长轴）。</param>
    private static CaliperAttempt RefineGrayCore(
        Mat gray, IReadOnlyList<Point2f> contour, bool brightToDark, bool probeAlongLong)
    {
        var housing = MaskHousing.Fit(contour);
        var longLen = housing.LongLen;
        var shortLen = housing.ShortLen;
        if (longLen < 16 || shortLen < 8)
            return CaliperAttempt.Miss();

        // 壳体框（已剔凸起）上放卡尺；0° 时长边沿 +x，+短轴朝下
        var theta0 = AngleGeometry.NormalizeDeg(housing.WarpAngleDeg);
        var c0x = housing.Center.X;
        var c0y = housing.Center.Y;
        var probeSpanLen = probeAlongLong ? longLen : shortLen;
        var searchSpanLen = probeAlongLong ? shortLen : longLen;
        var probeCount = MaskHousing.ProbeCount(probeSpanLen);
        var endInset = MaskHousing.ProbeInsetRatio(probeSpanLen, searchSpanLen, probeAlongLong);
        LastDebug = LastDebug with { ProbeCount = probeCount };
        var rad = theta0 * Math.PI / 180.0;
        var lx = Math.Cos(rad);
        var ly = Math.Sin(rad);
        var sx = -Math.Sin(rad);
        var sy = Math.Cos(rad);

        var halfProbe = probeSpanLen / 2.0;
        var uSpan = halfProbe * (1.0 - 2.0 * endInset);
        if (uSpan < 8)
            return CaliperAttempt.Miss();

        var search = searchSpanLen / 2.0 + Math.Max(16.0, 0.40 * searchSpanLen);
        var searchI = Math.Max(8, (int)Math.Ceiling(search));

        var searchBars = new List<Segment>(probeCount);
        var invalidBars = new List<Segment>(probeCount);
        var minus = new List<(double T, double S)>(probeCount);
        var plus = new List<(double T, double S)>(probeCount);

        Point2d At(double t, double s) => new(c0x + t * lx + s * sx, c0y + t * ly + s * sy);

        for (var i = 0; i < probeCount; i++)
        {
            var u = probeCount == 1 ? 0.5 : (double)i / (probeCount - 1);
            var probeCoord = -uSpan + 2.0 * uSpan * u;
            double px, py, dirX, dirY, orthX, orthY;
            if (probeAlongLong)
            {
                px = At(probeCoord, 0).X;
                py = At(probeCoord, 0).Y;
                dirX = lx; dirY = ly;
                orthX = sx; orthY = sy;
            }
            else
            {
                px = At(0, probeCoord).X;
                py = At(0, probeCoord).Y;
                dirX = sx; dirY = sy;
                orthX = lx; orthY = ly;
            }

            var bar = new Segment(
                new Point2d(px - searchI * orthX, py - searchI * orthY),
                new Point2d(px + searchI * orthX, py + searchI * orthY));
            if (!TryProbeEdges(gray, px, py, dirX, dirY, orthX, orthY, searchI, searchSpanLen,
                    brightToDark, out var eMinus, out var ePlus))
            {
                invalidBars.Add(bar);
                continue;
            }

            searchBars.Add(bar);
            if (probeAlongLong)
            {
                minus.Add((probeCoord, eMinus));
                plus.Add((probeCoord, ePlus));
            }
            else
            {
                minus.Add((probeCoord, eMinus));
                plus.Add((probeCoord, ePlus));
            }
        }

        var inMinus = FilterMedian(minus, searchSpanLen);
        var inPlus = FilterMedian(plus, searchSpanLen);
        var inliers = new List<Point2d>(minus.Count + plus.Count);
        var rejected = new List<Point2d>();
        Point2d Hit((double T, double S) p) => probeAlongLong ? At(p.T, p.S) : At(p.S, p.T);
        ClassifyHits(minus, inMinus, Hit, inliers, rejected);
        ClassifyHits(plus, inPlus, Hit, inliers, rejected);
        LastDebug = LastDebug with { ValidProbes = Math.Min(inMinus.Count, inPlus.Count) };

        CaliperViz Viz(Segment? fittedMinus = null, Segment? fittedPlus = null) =>
            BuildViz(searchBars, invalidBars, inliers, rejected, fittedMinus, fittedPlus);

        if (inMinus.Count < MinInliersPerSide || inPlus.Count < MinInliersPerSide)
            return CaliperAttempt.Miss(Viz());

        if (!TryFitSlope(inMinus, out var a1, out var b1) || !TryFitSlope(inPlus, out var a2, out var b2))
            return CaliperAttempt.Miss(Viz());

        var d1 = Math.Atan(a1) * 180.0 / Math.PI;
        var d2 = Math.Atan(a2) * 180.0 / Math.PI;
        var parallel = Math.Abs(d1 - d2);
        var delta = (d1 + d2) / 2.0;
        var width = Math.Abs(b2 - b1);
        LastDebug = LastDebug with
        {
            ParallelDeg = parallel,
            WidthPx = width,
            AngleUndirectedDeg = AngleGeometry.NormalizeDeg(theta0 + delta),
        };

        var fittedMinus = FitEdge(uSpan, a1, b1, probeAlongLong, At);
        var fittedPlus = FitEdge(uSpan, a2, b2, probeAlongLong, At);
        if (parallel > MaxParallelDeg)
            return CaliperAttempt.Miss(Viz(fittedMinus, fittedPlus));
        var ratio = width / searchSpanLen;
        if (ratio < WidthRatioLo || ratio > WidthRatioHi)
            return CaliperAttempt.Miss(Viz(fittedMinus, fittedPlus));

        double cx, cy;
        if (probeAlongLong)
        {
            var sMid = (b1 + b2) / 2.0;
            cx = c0x + sMid * sx;
            cy = c0y + sMid * sy;
        }
        else
        {
            var tMid = (b1 + b2) / 2.0;
            cx = c0x + tMid * lx;
            cy = c0y + tMid * ly;
        }

        var thetaFit = theta0 + delta;

        var radF = thetaFit * Math.PI / 180.0;
        var flx = Math.Cos(radF);
        var fly = Math.Sin(radF);
        var fsx = -Math.Sin(radF);
        var fsy = Math.Cos(radF);

        // 头尾相对壳体短轴：拟合宽度在左右卡尺下是长边，不能当半宽。
        var bodyHalf = housing.ShortLen / 2.0;
        var tabHouse = TabSignFromContour(contour, housing.Center.X, housing.Center.Y, fsx, fsy, bodyHalf);
        var halfCal = probeAlongLong ? width / 2.0 : bodyHalf;
        var tabCal = TabSignFromContour(contour, cx, cy, fsx, fsy, halfCal);
        var tab = tabHouse.Sign is not null && tabCal.Sign is not null
            ? (Math.Abs(tabHouse.Diff) >= Math.Abs(tabCal.Diff) ? tabHouse : tabCal)
            : tabHouse.Sign is not null ? tabHouse : tabCal;
        if (tab.Sign is null)
            tab = TabSignFromCombTexture(gray, housing.Center.X, housing.Center.Y, flx, fly, fsx, fsy,
                housing.LongLen / 2.0, bodyHalf);
        if (tab.Sign is null)
            tab = TabSignFromRoughness(contour, housing.Center.X, housing.Center.Y, flx, fly, fsx, fsy,
                housing.LongLen / 2.0, bodyHalf);
        if (tab.Sign is null)
        {
            var sLo = probeAlongLong ? -width / 2.0 : -bodyHalf;
            var sHi = probeAlongLong ? width / 2.0 : bodyHalf;
            tab = TryTabSign(gray, cx, cy, flx, fly, fsx, fsy, longLen, sLo, sHi);
        }

        LastDebug = LastDebug with
        {
            TabSign = tab.Sign,
            TabGrayDiff = tab.Diff,
        };

        if (tab.Sign is null)
            return CaliperAttempt.Miss(Viz(fittedMinus, fittedPlus));

        var directed = tab.Sign is < 0
            ? thetaFit + 180.0
            : thetaFit;
        var angle = AngleGeometry.NormalizeSignedDeg(directed);

        var tabDirX = (tab.Sign is < 0 ? -1.0 : 1.0) * fsx;
        var tabDirY = (tab.Sign is < 0 ? -1.0 : 1.0) * fsy;
        var markerLen = width / 2.0 + Math.Max(MinTabSearchPx, TabSearchRatio * width);
        return new CaliperAttempt(
            new Result(
                angle,
                new Point2d(cx, cy),
                tab.Sign,
                new Point2d(cx, cy),
                new Point2d(cx + tabDirX * markerLen, cy + tabDirY * markerLen)),
            Viz(fittedMinus, fittedPlus));
    }

    private static void ClassifyHits(
        List<(double T, double S)> samples,
        List<(double T, double S)> kept,
        Func<(double T, double S), Point2d> hit,
        List<Point2d> inliers,
        List<Point2d> rejected)
    {
        var keep = kept.ToHashSet();
        foreach (var p in samples)
        {
            var pt = hit(p);
            if (keep.Contains(p))
                inliers.Add(pt);
            else
                rejected.Add(pt);
        }
    }

    private static Segment FitEdge(
        double uSpan, double a, double b, bool probeAlongLong, Func<double, double, Point2d> at)
    {
        Point2d End(double u) => probeAlongLong ? at(u, a * u + b) : at(a * u + b, u);
        return new Segment(End(-uSpan), End(uSpan));
    }

    private static CaliperViz BuildViz(
        List<Segment> searchBars,
        List<Segment> invalidBars,
        List<Point2d> inliers,
        List<Point2d> rejected,
        Segment? fittedMinus,
        Segment? fittedPlus) =>
        new(
            searchBars.ToArray(),
            invalidBars.ToArray(),
            inliers.ToArray(),
            rejected.ToArray(),
            fittedMinus,
            fittedPlus);

    /// <summary>沿短轴采剖面：从两侧外侧向中心找第一条壳体边，避免内孔/槽抢峰。</summary>
    private static bool TryProbeEdges(
        Mat gray, double px, double py,
        double lx, double ly, double sx, double sy,
        int search, double shortLen, bool brightToDark,
        out double sMinus, out double sPlus)
    {
        sMinus = 0;
        sPlus = 0;
        var n = 2 * search + 1;
        Span<double> profile = n <= 256 ? stackalloc double[n] : new double[n];
        for (var i = 0; i < n; i++)
        {
            var s = i - search;
            profile[i] = SampleAveraged(gray, px + s * sx, py + s * sy, lx, ly);
        }

        var iMinus = FindFirstEdgeFromOutside(profile, fromLow: true, search, brightToDark);
        var iPlus = FindFirstEdgeFromOutside(profile, fromLow: false, search, brightToDark);
        if (iMinus < 0 || iPlus < 0)
            return false;

        sMinus = (iMinus - search) + SubpixelOffset(profile, iMinus, peakIsNegativeGradient: brightToDark);
        sPlus = (iPlus - search) + SubpixelOffset(profile, iPlus, peakIsNegativeGradient: !brightToDark);
        if (!double.IsFinite(sMinus) || !double.IsFinite(sPlus))
            return false;
        var w = sPlus - sMinus;
        if (w < shortLen * WidthRatioLo || w > shortLen * WidthRatioHi)
            return false;
        return true;
    }

    /// <summary>
    /// fromLow：从 -s（图像上侧，θ≈0）往中心走；
    /// 否则从 +s 往中心走。亮场抓从背景走进壳体的亮→暗，暗场抓暗→亮。
    /// </summary>
    private static int FindFirstEdgeFromOutside(
        Span<double> profile, bool fromLow, int search, bool brightToDark)
    {
        var n = profile.Length;
        if (fromLow)
        {
            for (var i = 1; i < search; i++)
            {
                if (!HasFiniteTriplet(profile, i))
                    continue;
                var g = profile[i + 1] - profile[i - 1];
                var score = brightToDark ? -g : g;
                if (score >= MinGradient)
                    return i;
            }
        }
        else
        {
            for (var i = n - 2; i > search; i--)
            {
                if (!HasFiniteTriplet(profile, i))
                    continue;
                var g = profile[i + 1] - profile[i - 1];
                var score = brightToDark ? g : -g;
                if (score >= MinGradient)
                    return i;
            }
        }

        return -1;
    }

    private static bool HasFiniteTriplet(Span<double> profile, int i) =>
        double.IsFinite(profile[i - 1]) && double.IsFinite(profile[i]) && double.IsFinite(profile[i + 1]);

    private static double SubpixelOffset(Span<double> profile, int i, bool peakIsNegativeGradient)
    {
        if (i <= 1 || i >= profile.Length - 2)
            return 0;
        if (!HasFiniteTriplet(profile, i - 1) || !HasFiniteTriplet(profile, i) || !HasFiniteTriplet(profile, i + 1))
            return 0;
        var prev = GradientScore(profile, i - 1, peakIsNegativeGradient);
        var best = GradientScore(profile, i, peakIsNegativeGradient);
        var next = GradientScore(profile, i + 1, peakIsNegativeGradient);
        var denom = prev - 2 * best + next;
        if (Math.Abs(denom) < 1e-9)
            return 0;
        return Math.Clamp(0.5 * (prev - next) / denom, -0.5, 0.5);
    }

    private static double GradientScore(Span<double> profile, int k, bool peakIsNegativeGradient)
    {
        var g = profile[k + 1] - profile[k - 1];
        return peakIsNegativeGradient ? -g : g;
    }

    private static double SampleAveraged(Mat gray, double x, double y, double lx, double ly)
    {
        var sum = 0.0;
        var count = 0;
        for (var k = -SamplingHalfWidth; k <= SamplingHalfWidth; k++)
        {
            var v = SampleBilinear(gray, x + k * lx, y + k * ly);
            if (!double.IsFinite(v))
                continue;
            sum += v;
            count++;
        }

        return count == 0 ? double.NaN : sum / count;
    }

    private static double SampleBilinear(Mat gray, double x, double y)
    {
        var w = gray.Width;
        var h = gray.Height;
        if (x < 0 || y < 0 || x >= w - 1 || y >= h - 1)
            return double.NaN;
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var dx = x - x0;
        var dy = y - y0;
        var v00 = gray.At<byte>(y0, x0);
        var v10 = gray.At<byte>(y0, x0 + 1);
        var v01 = gray.At<byte>(y0 + 1, x0);
        var v11 = gray.At<byte>(y0 + 1, x0 + 1);
        return (1 - dx) * (1 - dy) * v00 + dx * (1 - dy) * v10 + (1 - dx) * dy * v01 + dx * dy * v11;
    }

    /// <summary>凸起把单侧若干探针的边向外拉；按该侧 s 的中位数剔点。</summary>
    private static List<(double T, double S)> FilterMedian(List<(double T, double S)> samples, double shortLen)
    {
        if (samples.Count == 0)
            return samples;
        var ordered = samples.Select(p => p.S).OrderBy(v => v).ToArray();
        var med = ordered[ordered.Length / 2];
        var thresh = Math.Max(2.5, 0.08 * shortLen);
        var kept = samples.Where(p => Math.Abs(p.S - med) <= thresh).ToList();
        return kept.Count >= MinInliersPerSide ? kept : samples;
    }

    private static bool TryFitSlope(List<(double T, double S)> pts, out double a, out double b)
    {
        a = 0;
        b = 0;
        if (pts.Count < 3)
            return false;
        var meanT = pts.Average(p => p.T);
        var meanS = pts.Average(p => p.S);
        var varT = 0.0;
        var cov = 0.0;
        foreach (var p in pts)
        {
            var dt = p.T - meanT;
            varT += dt * dt;
            cov += dt * (p.S - meanS);
        }
        if (varT < 1e-6)
            return false;
        a = cov / varT;
        b = meanS - a * meanT;
        return true;
    }

    private static (int? Sign, double Diff) TryTabSign(
        Mat gray, double cx, double cy,
        double lx, double ly, double sx, double sy,
        double longLen, double sEdgeMinus, double sEdgePlus)
    {
        var extra = Math.Max(MinTabSearchPx, TabSearchRatio * Math.Abs(sEdgePlus - sEdgeMinus));
        var tHalf = 0.22 * longLen;
        const int nT = 11;
        var minusVals = new List<double>(64);
        var plusVals = new List<double>(64);
        var sLo = (int)Math.Floor(sEdgeMinus - extra);
        var sHi = (int)Math.Ceiling(sEdgePlus + extra);
        for (var i = 0; i < nT; i++)
        {
            var t = nT == 1 ? 0.0 : -tHalf + 2.0 * tHalf * i / (nT - 1);
            var bx = cx + t * lx;
            var by = cy + t * ly;
            for (var s = sLo; s <= sHi; s++)
            {
                var g = SampleBilinear(gray, bx + s * sx, by + s * sy);
                if (!double.IsFinite(g))
                    continue;
                if (s < sEdgeMinus - 0.5 && s >= sEdgeMinus - extra)
                    minusVals.Add(g);
                else if (s > sEdgePlus + 0.5 && s <= sEdgePlus + extra)
                    plusVals.Add(g);
            }
        }

        if (minusVals.Count < 8 || plusVals.Count < 8)
            return (null, 0);
        var darkMinus = DarkestQuartile(minusVals);
        var darkPlus = DarkestQuartile(plusVals);
        var diff = darkMinus - darkPlus; // 正 = +短轴外侧更暗
        if (Math.Abs(diff) < MinTabGrayDiff)
            return (null, diff);
        return (diff > 0 ? 1 : -1, diff);
    }

    private static double DarkestQuartile(List<double> values)
    {
        values.Sort();
        var n = Math.Max(1, values.Count / 4);
        var sum = 0.0;
        for (var i = 0; i < n; i++)
            sum += values[i];
        return sum / n;
    }

    /// <summary>灰度外侧差不够时：看轮廓哪一侧比壳体半宽多伸出一截（分割若画出凸起即可用）。</summary>
    private static (int? Sign, double Diff) TabSignFromContour(
        IReadOnlyList<Point2f> contour, double cx, double cy, double sx, double sy, double halfW)
    {
        var minS = double.MaxValue;
        var maxS = double.MinValue;
        foreach (var p in contour)
        {
            var s = (p.X - cx) * sx + (p.Y - cy) * sy;
            if (s < minS) minS = s;
            if (s > maxS) maxS = s;
        }

        var plusOver = maxS - halfW;
        var minusOver = -halfW - minS;
        var diff = plusOver - minusOver;
        if (plusOver < 4 && minusOver < 4)
            return (null, diff);
        if (Math.Abs(diff) < 4)
            return (null, diff);
        return (diff > 0 ? 1 : -1, diff);
    }

    /// <summary>暗场齿列：沿两条长边外侧扫灰度峰，齿多的一侧为凸起。</summary>
    private static (int? Sign, double Diff) TabSignFromCombTexture(
        Mat gray, double cx, double cy,
        double lx, double ly, double sx, double sy,
        double halfLong, double halfShort)
    {
        var plus = CountEdgeTeeth(gray, cx, cy, lx, ly, sx, sy, +1, halfLong, halfShort);
        var minus = CountEdgeTeeth(gray, cx, cy, lx, ly, sx, sy, -1, halfLong, halfShort);
        var diff = plus - minus;
        if (Math.Max(plus, minus) < 6)
            return (null, diff);
        if (Math.Min(plus, minus) > 2)
            return (null, diff);
        if (Math.Abs(diff) < 4)
            return (null, diff);
        return (diff > 0 ? 1 : -1, diff);
    }

    private static int CountEdgeTeeth(
        Mat gray, double cx, double cy,
        double lx, double ly, double sx, double sy,
        int side, double halfLong, double halfShort)
    {
        var nT = Math.Clamp((int)Math.Round(halfLong * 1.2), 32, 96);
        var tSpan = 0.42 * halfLong;
        var sLo = 0.30 * halfShort;
        var sHi = 1.35 * halfShort;
        var nS = Math.Max(4, (int)Math.Ceiling(sHi - sLo));
        Span<double> profile = nT <= 128 ? stackalloc double[nT] : new double[nT];
        var finite = 0;
        for (var i = 0; i < nT; i++)
        {
            var t = nT == 1 ? 0.0 : -tSpan + 2.0 * tSpan * i / (nT - 1);
            var best = double.NegativeInfinity;
            for (var k = 0; k < nS; k++)
            {
                var s = side * (sLo + (sHi - sLo) * k / Math.Max(1, nS - 1));
                var g = SampleBilinear(gray, cx + t * lx + s * sx, cy + t * ly + s * sy);
                if (!double.IsFinite(g))
                    continue;
                if (g > best)
                    best = g;
            }

            profile[i] = best;
            if (double.IsFinite(best))
                finite++;
        }

        if (finite < nT / 2)
            return 0;

        var sum = 0.0;
        var peak = double.NegativeInfinity;
        var valley = double.PositiveInfinity;
        for (var i = 0; i < nT; i++)
        {
            if (!double.IsFinite(profile[i]))
                continue;
            sum += profile[i];
            if (profile[i] > peak) peak = profile[i];
            if (profile[i] < valley) valley = profile[i];
        }

        var mean = sum / finite;
        if (mean > 160)
            return 0;

        var thresh = mean + 0.22 * Math.Max(8, peak - valley);
        var minGap = Math.Max(2, nT / 28);
        var last = -minGap;
        var teeth = 0;
        for (var i = 1; i < nT - 1; i++)
        {
            if (!double.IsFinite(profile[i - 1]) || !double.IsFinite(profile[i]) || !double.IsFinite(profile[i + 1]))
                continue;
            if (profile[i] < thresh)
                continue;
            if (profile[i] >= profile[i - 1] && profile[i] >= profile[i + 1] && i - last >= minGap)
            {
                teeth++;
                last = i;
            }
        }

        return teeth;
    }

    /// <summary>长边外侧弧长：齿列周长远大于缺口，可作头尾；也用于 Auto 判断要不要改抓左右边。</summary>
    private static (int? Sign, double Diff) TabSignFromRoughness(
        IReadOnlyList<Point2f> contour,
        double cx, double cy,
        double lx, double ly, double sx, double sy,
        double halfLong, double halfShort)
    {
        var arcs = OuterArcLengths(contour, cx, cy, lx, ly, sx, sy, halfShort);
        var diff = arcs.Plus - arcs.Minus;
        var minArc = 0.35 * Math.Max(16, halfLong);
        if (arcs.Plus < minArc && arcs.Minus < minArc)
            return (null, diff);
        if (Math.Abs(diff) < 0.18 * Math.Max(arcs.Plus, arcs.Minus))
            return (null, diff);
        return (diff > 0 ? 1 : -1, diff);
    }

    private static double LongEdgeRoughness(IReadOnlyList<Point2f> contour, HousingFrame housing)
    {
        var rad = housing.WarpAngleDeg * Math.PI / 180.0;
        var lx = Math.Cos(rad);
        var ly = Math.Sin(rad);
        var sx = -Math.Sin(rad);
        var sy = Math.Cos(rad);
        var arcs = OuterArcLengths(contour, housing.Center.X, housing.Center.Y, lx, ly, sx, sy, housing.ShortLen / 2.0);
        var len = Math.Max(8, housing.LongLen);
        var ePlus = Math.Max(0, arcs.Plus / len - 1.0);
        var eMinus = Math.Max(0, arcs.Minus / len - 1.0);
        // 两侧都锯齿（上齿下列缺口）才改抓左右；单侧小凸起仍走上下长边。
        if (ePlus > 0.10 && eMinus > 0.10)
            return Math.Clamp(0.5 * (ePlus + eMinus), 0, 1);
        return Math.Clamp(Math.Max(ePlus, eMinus) - 0.45, 0, 1);
    }

    private static (double Minus, double Plus) OuterArcLengths(
        IReadOnlyList<Point2f> contour, double cx, double cy,
        double lx, double ly, double sx, double sy, double halfShort)
    {
        var n = contour.Count;
        if (n < 8 || halfShort < 4)
            return (0, 0);

        var outer = 0.42 * halfShort;
        var tLimit = 0.0;
        for (var i = 0; i < n; i++)
        {
            var t = Math.Abs((contour[i].X - cx) * lx + (contour[i].Y - cy) * ly);
            if (t > tLimit)
                tLimit = t;
        }

        var end = 0.82 * Math.Max(8, tLimit);
        var minus = 0.0;
        var plus = 0.0;
        for (var i = 0; i < n; i++)
        {
            var a = contour[i];
            var b = contour[(i + 1) % n];
            var ta = (a.X - cx) * lx + (a.Y - cy) * ly;
            var tb = (b.X - cx) * lx + (b.Y - cy) * ly;
            if (Math.Abs(ta) > end || Math.Abs(tb) > end)
                continue;

            var sa = (a.X - cx) * sx + (a.Y - cy) * sy;
            var sb = (b.X - cx) * sx + (b.Y - cy) * sy;
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (sa > outer && sb > outer)
                plus += len;
            else if (sa < -outer && sb < -outer)
                minus += len;
        }

        return (minus, plus);
    }
}
