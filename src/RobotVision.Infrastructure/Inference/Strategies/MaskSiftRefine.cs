using System.Security.Cryptography;
using System.Text;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using RobotVision.Core.Geometry;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 示教模板与当前分割框内原图做 SIFT 匹配，RANSAC 相似变换给出有向角和中心。
/// 模板按配方页转正裁剪（长边水平）；查询用轴对齐包围盒，旋转由特征自己吸收。
/// </summary>
public static class MaskSiftRefine
{
    private const int MaxFeatures = 800;
    private const int MinTeachKeypoints = 16;
    private const int MinQueryKeypoints = 8;
    private const int MinGoodMatches = 8;
    private const int MinInliers = 8;
    private const double LoweRatio = 0.75;
    private const double RansacPx = 3.5;
    private const double ScaleLo = 0.55;
    private const double ScaleHi = 1.8;
    private const double CropPadRatio = 0.12;

    public sealed record Result(double AngleDeg, Point2d Center, double Score, int Inliers, int Matches);

    public sealed record SiftViz(IReadOnlyList<Point2d> Inliers, IReadOnlyList<Point2d> Rejected)
    {
        public static readonly SiftViz Empty = new([], []);
    }

    public sealed record Attempt(Result? Pose, SiftViz Viz)
    {
        public static Attempt Miss(SiftViz? viz = null) => new(null, viz ?? SiftViz.Empty);
    }

    /// <summary>示教图的 SIFT 描述子。由 <see cref="BuildTeach"/> / 缓存持有，匹配时只借阅。</summary>
    public sealed class TeachModel : IDisposable
    {
        internal TeachModel(KeyPoint[] keyPoints, Mat descriptors, double centerX, double centerY)
        {
            KeyPoints = keyPoints;
            Descriptors = descriptors;
            CenterX = centerX;
            CenterY = centerY;
        }

        internal KeyPoint[] KeyPoints { get; }
        internal Mat Descriptors { get; }
        internal double CenterX { get; }
        internal double CenterY { get; }

        public int KeypointCount => KeyPoints.Length;

        public void Dispose() => Descriptors.Dispose();
    }

    internal readonly record struct DebugInfo(int TeachKp, int QueryKp, int Matches, int Inliers, double Scale);

    [ThreadStatic]
    internal static DebugInfo LastDebug;

    private static readonly ThreadLocal<SIFT> Detector = new(CreateDetector);
    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, CachedTeach> Cache = new(StringComparer.OrdinalIgnoreCase);

    private sealed class CachedTeach(string fingerprint, TeachModel model)
    {
        public string Fingerprint { get; } = fingerprint;
        public TeachModel Model { get; } = model;
    }

    public static Result? Refine(Mat image, IReadOnlyList<Point2f> contour, TeachModel teach) =>
        TryRefine(image, contour, teach).Pose;

    public static Attempt TryRefine(Mat image, IReadOnlyList<Point2f> contour, TeachModel? teach)
    {
        LastDebug = default;
        if (image.Empty() || contour.Count < 4 || teach is null || teach.Descriptors.Empty())
            return Attempt.Miss();

        using var query = ExtractQuery(image, contour);
        if (query is null)
            return Attempt.Miss();

        try
        {
            LastDebug = LastDebug with { TeachKp = teach.KeypointCount, QueryKp = query.KeyPoints.Length };
            return Align(teach, query);
        }
        finally
        {
            query.Dispose();
        }
    }

    /// <summary>从示教 PNG/BGR/灰度图提取描述子。关键点太少返回 null。</summary>
    public static TeachModel? BuildTeach(Mat template)
    {
        if (template.Empty())
            return null;

        using var gray = ToGray(template);
        var sift = Detector.Value!;
        var desc = new Mat();
        sift.DetectAndCompute(gray, null, out var kps, desc);
        if (kps.Length < MinTeachKeypoints || desc.Empty())
        {
            desc.Dispose();
            return null;
        }

        return new TeachModel(kps, desc, gray.Width / 2.0, gray.Height / 2.0);
    }

    public static void Warm(RecipeConfig recipe)
    {
        if (!ShouldCache(recipe))
            return;
        GetOrCreate(recipe);
    }

    public static void Remove(string recipeName)
    {
        lock (CacheGate)
        {
            if (Cache.Remove(recipeName, out var cached))
                cached.Model.Dispose();
        }
    }

    /// <summary>命中指纹则复用；模板变更则重建。调用方不得 Dispose 返回对象。</summary>
    public static TeachModel? GetOrCreate(RecipeConfig recipe)
    {
        if (!ShouldCache(recipe))
            return null;

        var fingerprint = Fingerprint(recipe.Template);
        lock (CacheGate)
        {
            if (Cache.TryGetValue(recipe.Name, out var cached) && cached.Fingerprint == fingerprint)
                return cached.Model;

            if (cached is not null)
            {
                Cache.Remove(recipe.Name);
                cached.Model.Dispose();
            }

            using var decoded = MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
            var model = BuildTeach(decoded);
            if (model is null)
                return null;
            Cache[recipe.Name] = new CachedTeach(fingerprint, model);
            return model;
        }
    }

    internal static double QualityScore(DebugInfo d)
    {
        if (d.Inliers < MinInliers)
            return 0;
        var ratio = d.Matches <= 0 ? 0 : (double)d.Inliers / d.Matches;
        var body = Math.Clamp(d.Inliers / 40.0, 0, 1);
        return Math.Clamp(0.45 * ratio + 0.55 * body, 0.15, 1);
    }

    private static bool ShouldCache(RecipeConfig recipe) =>
        recipe.AngleMode == AngleMode.MaskTemplate
        && recipe.Template.RefineMethod == SegmentRefineMethod.Sift
        && !string.IsNullOrEmpty(recipe.Template.TemplateImageBase64);

    private static string Fingerprint(TemplateOptions template)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(template.TemplateImageBase64));
        return Convert.ToHexString(hash);
    }

    private static SIFT CreateDetector() =>
        SIFT.Create(nFeatures: MaxFeatures, nOctaveLayers: 3, contrastThreshold: 0.02,
            edgeThreshold: 10, sigma: 1.6);

    private static Mat ToGray(Mat src)
    {
        var gray = new Mat();
        if (src.Channels() == 1)
            src.CopyTo(gray);
        else
            Cv2.CvtColor(src, gray, src.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private sealed class QueryView : IDisposable
    {
        public required KeyPoint[] KeyPoints;
        public required Mat Descriptors;
        public required double OriginX;
        public required double OriginY;

        public void Dispose() => Descriptors.Dispose();
    }

    private static QueryView? ExtractQuery(Mat image, IReadOnlyList<Point2f> contour)
    {
        var box = Cv2.BoundingRect(contour);
        var padX = Math.Max(12, (int)(box.Width * CropPadRatio));
        var padY = Math.Max(12, (int)(box.Height * CropPadRatio));
        var x = Math.Max(0, box.X - padX);
        var y = Math.Max(0, box.Y - padY);
        var w = Math.Min(image.Width - x, box.Width + 2 * padX);
        var h = Math.Min(image.Height - y, box.Height + 2 * padY);
        if (w < 32 || h < 32)
            return null;

        using var roi = new Mat(image, new Rect(x, y, w, h));
        using var gray = ToGray(roi);
        var desc = new Mat();
        Detector.Value!.DetectAndCompute(gray, null, out var kps, desc);
        if (kps.Length < MinQueryKeypoints || desc.Empty())
        {
            desc.Dispose();
            return null;
        }

        return new QueryView
        {
            KeyPoints = kps,
            Descriptors = desc,
            OriginX = x,
            OriginY = y,
        };
    }

    private static Attempt Align(TeachModel teach, QueryView query)
    {
        using var matcher = new BFMatcher(NormTypes.L2, crossCheck: false);
        var knn = matcher.KnnMatch(query.Descriptors, teach.Descriptors, k: 2);
        var src = new List<Point2f>();
        var dst = new List<Point2f>();
        foreach (var pair in knn)
        {
            if (pair.Length < 2)
                continue;
            if (pair[0].Distance >= LoweRatio * pair[1].Distance)
                continue;
            src.Add(teach.KeyPoints[pair[0].TrainIdx].Pt);
            dst.Add(query.KeyPoints[pair[0].QueryIdx].Pt);
        }

        LastDebug = LastDebug with { Matches = src.Count };
        if (src.Count < MinGoodMatches)
            return Attempt.Miss();

        using var inliers = new Mat();
        using var affine = Cv2.EstimateAffinePartial2D(
            InputArray.Create(src), InputArray.Create(dst), inliers,
            RobustEstimationAlgorithms.RANSAC, RansacPx, 2000, 0.99, 10);
        if (affine is null || affine.Empty())
            return Attempt.Miss();

        var flags = new bool[src.Count];
        var nIn = 0;
        if (!inliers.Empty())
        {
            for (var i = 0; i < inliers.Rows && i < flags.Length; i++)
            {
                if (inliers.At<byte>(i, 0) == 0)
                    continue;
                flags[i] = true;
                nIn++;
            }
        }

        LastDebug = LastDebug with { Inliers = nIn };
        var viz = BuildViz(query, dst, flags);
        if (nIn < MinInliers)
            return Attempt.Miss(viz);

        var a = affine.At<double>(0, 0);
        var b = affine.At<double>(1, 0);
        var tx = affine.At<double>(0, 2);
        var ty = affine.At<double>(1, 2);
        var scale = Math.Sqrt(a * a + b * b);
        LastDebug = LastDebug with { Scale = scale };
        if (scale < ScaleLo || scale > ScaleHi)
            return Attempt.Miss(viz);

        var angle = AngleGeometry.NormalizeSignedDeg(Math.Atan2(b, a) * 180.0 / Math.PI);
        var qx = a * teach.CenterX - b * teach.CenterY + tx;
        var qy = b * teach.CenterX + a * teach.CenterY + ty;
        var score = QualityScore(LastDebug);
        return new Attempt(
            new Result(angle, new Point2d(query.OriginX + qx, query.OriginY + qy), score, nIn, src.Count),
            viz);
    }

    private static SiftViz BuildViz(QueryView query, List<Point2f> dst, bool[] flags)
    {
        var inn = new List<Point2d>();
        var rej = new List<Point2d>();
        for (var i = 0; i < dst.Count; i++)
        {
            var p = new Point2d(query.OriginX + dst[i].X, query.OriginY + dst[i].Y);
            if (i < flags.Length && flags[i])
                inn.Add(p);
            else
                rej.Add(p);
        }

        return new SiftViz(inn, rej);
    }
}
