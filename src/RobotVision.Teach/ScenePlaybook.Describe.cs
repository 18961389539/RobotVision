using OpenCvSharp;
using RobotVision.JlVision;

namespace RobotVision.Teach;

/// <summary>
/// ScenePlaybook —— 场景分类与图像度量：从轮廓/掩码算轴比、圆度、熵、0/180 可分性、孔/凸起，
/// 加权软分类成 <see cref="SceneKind"/>。<b>只做"这是什么场景"，不做"该用哪种精修"（在 Recommend）。</b>
/// </summary>
public static partial class ScenePlaybook
{
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

        var housing = JlHousing.Fit(contour);
        var obb = JlHousing.FitObb(contour);
        var aspect = JlHousing.Aspect(obb);
        var protrusion = Math.Max(0, obb.ShortLen - housing.ShortLen);
        var arr = contour as Point2f[] ?? contour.ToArray();
        var area = Cv2.ContourArea(arr);
        var peri = Cv2.ArcLength(arr, true);
        var circularity = peri > 1e-3 ? Math.Clamp(4 * Math.PI * area / (peri * peri), 0, 1) : 0;

        var holeQuality = 0.0;
        var holeOk = false;
        if (bitPackedMask is { Length: > 0 } && maskWidth > 0 && maskHeight > 0)
        {
            var hole = JlCentroidHole.TryRefine(bitPackedMask, maskWidth, maskHeight);
            if (hole is not null)
            {
                holeOk = true;
                holeQuality = hole.Value.Quality;
            }
        }

        var entropy = 0.0;
        var relativeEntropy = 0.0;
        var separability = 0.0;
        try
        {
            var crop = JlTemplateIo.UprightCrop(bgr, contour, 0.05);
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
                ? FormattableString.Invariant($"掩码内有孔/槽（质量 {holeQuality:0.00}），头尾可用几何偏置，不必靠灰度。")
                : "掩码内有稳定孔/槽，头尾可用几何偏置，不必靠灰度。",
            SceneKind.HousingWithTab => FormattableString.Invariant($"短轴外伸 {protrusion:0.0}px、轴比 {aspect:0.0}，适合卡尺抓长边。"),
            SceneKind.Silhouette => FormattableString.Invariant($"相对熵 {relativeEntropy:+0.0;-0.0}、件内熵 {entropy:0.0}，更像剪影，灰度 NCC 容易漂。"),
            SceneKind.PrintedTexture => FormattableString.Invariant($"0°/180° 分差 {separability:0.00}，相对熵 {relativeEntropy:+0.0;-0.0}，局部纹理可做模板。"),
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
            ShortLenPx = housing.ShortLen,
        };
    }

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
        else if (holeOk && hole >= 0.40 && separability >= TeachThresholds.SeparabilityOrientable)
            list.Add("有孔且 0/180 可分，孔槽不独占。");
        if (tab >= 0.45 && printed >= 0.45)
            list.Add("凸起壳体同时有可分纹理。");
        if (circ >= 0.40 && tab >= 0.40)
            list.Add("近圆与细长信号冲突。");
        return list;
    }

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
