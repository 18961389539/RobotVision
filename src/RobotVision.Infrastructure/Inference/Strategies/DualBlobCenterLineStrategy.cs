using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式五：双BLOB连线（纯图像处理，无需模型）。阈值分割 + 连通域分析：
/// BLOB1（面积在 [MinArea,MaxArea] 内）只在配方 <c>Roi</c>（ROI1）内检测，质心定位 XY；
/// 设了 <see cref="BlobOptions.SecondaryRoi"/> 时 BLOB2 只在 ROI2 内检测（互斥，不扫对方区域）；
/// 未设次区时仍用主包围盒按 CropExpandRatio 外扩窗口找次 BLOB。
/// 角度 = 主质心→次质心连线（(-180,180] 有方向）。次 BLOB 缺失时该目标不输出。
/// </summary>
public sealed class DualBlobCenterLineStrategy : IAngleStrategy
{
    /// <summary>单个连通域的标记结果（全图坐标系）。</summary>
    private readonly record struct BlobInfo(int Label, int Area, double Cx, double Cy, Rect Box);

    public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default)
    {
        using var mat = VisionImageCv.AsMat(undistorted);
        return ComputeCore(mat, recipe, ct);
    }

    private static List<PixelPose> ComputeCore(Mat undistorted, RecipeConfig recipe, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var opt = recipe.Blob;

        using var gray = ToGray(undistorted);
        var useSecondaryRoi = recipe.SecondarySearchRoi is not null;
        // 双 ROI：BLOB1 只在 ROI1。主区未设时不退回全图，避免把 ROI2 里的斑当成 BLOB1。
        if (useSecondaryRoi && recipe.Roi is null)
            return [];

        var labeledPrimary = LabelRegion(gray, recipe.Roi, opt, ct);
        var primaries = labeledPrimary
            .Where(b => b.Area >= opt.MinArea && b.Area <= opt.MaxArea)
            .ToList();
        var secondaries = useSecondaryRoi
            ? LabelRegion(gray, recipe.SecondarySearchRoi, opt, ct)
                .Where(b => b.Area >= opt.SecondaryMinArea && b.Area <= opt.SecondaryMaxArea)
                .ToList()
            : labeledPrimary
                .Where(b => b.Area >= opt.SecondaryMinArea && b.Area <= opt.SecondaryMaxArea)
                .ToList();

        var poses = new List<PixelPose>();
        foreach (var primary in primaries)
        {
            var window = useSecondaryRoi
                ? new Rect(0, 0, gray.Width, gray.Height)
                : Expand(primary.Box, opt.CropExpandRatio, gray.Width, gray.Height);

            BlobInfo? best = null;
            var bestDistance = double.MaxValue;
            foreach (var candidate in secondaries)
            {
                if (SameBlob(primary, candidate))
                    continue;
                if (!useSecondaryRoi &&
                    (candidate.Cx < window.Left || candidate.Cx >= window.Right ||
                     candidate.Cy < window.Top || candidate.Cy >= window.Bottom))
                    continue;

                var distance = Distance(primary.Cx, primary.Cy, candidate.Cx, candidate.Cy);
                if (distance < opt.MinPairDistancePx || distance > opt.MaxPairDistancePx)
                    continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (best is not { } secondary)
                continue;

            var angleDeg = AngleGeometry.NormalizeSignedDeg(
                Math.Atan2(secondary.Cy - primary.Cy, secondary.Cx - primary.Cx) * 180.0 / Math.PI);
            var score = Math.Clamp((double)primary.Area / opt.MaxArea, 0, 1);
            poses.Add(new PixelPose(primary.Cx, primary.Cy, angleDeg, score)
            {
                Overlay = new PoseOverlay
                {
                    Boxes =
                    [
                        new PixelRect(primary.Box.X, primary.Box.Y, primary.Box.Width, primary.Box.Height),
                        new PixelRect(secondary.Box.X, secondary.Box.Y, secondary.Box.Width, secondary.Box.Height),
                    ],
                    Baseline =
                    [
                        new PixelPoint(primary.Cx, primary.Cy),
                        new PixelPoint(secondary.Cx, secondary.Cy),
                    ],
                },
            });
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }

    /// <summary>只在给定 ROI（null = 全图）内二值化并标记连通域，坐标已偏回全图。</summary>
    private static List<BlobInfo> LabelRegion(Mat gray, Roi? roi, BlobOptions opt, CancellationToken ct)
    {
        double ox = 0, oy = 0;
        using var roiOwned = roi is null ? null : RoiHelper.Crop(gray, roi, out ox, out oy);
        var view = roiOwned ?? gray;

        using var binary = Binarize(view, opt);
        ct.ThrowIfCancellationRequested();

        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);
        ct.ThrowIfCancellationRequested();

        var blobs = new List<BlobInfo>(Math.Max(0, count - 1));
        for (var i = 1; i < count; i++)
        {
            var area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
            blobs.Add(new BlobInfo(
                i, area,
                centroids.At<double>(i, 0) + ox,
                centroids.At<double>(i, 1) + oy,
                new Rect(
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Left) + (int)Math.Round(ox),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Top) + (int)Math.Round(oy),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Width),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Height))));
        }

        return blobs;
    }

    private static bool SameBlob(BlobInfo a, BlobInfo b) =>
        Distance(a.Cx, a.Cy, b.Cx, b.Cy) < 1.5;

    private static Mat ToGray(Mat image)
    {
        var gray = image.Channels() switch
        {
            1 => image.Clone(),
            3 => new Mat(),
            4 => new Mat(),
            _ => throw new ArgumentException($"不支持的图像通道数: {image.Channels()}", nameof(image)),
        };
        if (image.Channels() == 3)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else if (image.Channels() == 4)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
        if (gray.Depth() == MatType.CV_8U)
            return gray;

        var normalized = new Mat();
        Cv2.Normalize(gray, normalized, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        gray.Dispose();
        return normalized;
    }

    private static Mat Binarize(Mat gray, BlobOptions opt)
    {
        var binary = new Mat();
        var type = opt.DetectDark ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
        if (opt.UseOtsu)
            type |= ThresholdTypes.Otsu;
        Cv2.Threshold(gray, binary, opt.Threshold, 255, type);

        if (opt.OpenKernelSize > 1)
        {
            using var kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new Size(opt.OpenKernelSize, opt.OpenKernelSize));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);
        }
        return binary;
    }

    private static Rect Expand(Rect box, double ratio, int imageWidth, int imageHeight)
    {
        var dx = (int)Math.Round(box.Width * ratio);
        var dy = (int)Math.Round(box.Height * ratio);
        var expanded = new Rect(box.X - dx, box.Y - dy,
            box.Width + 2 * dx, box.Height + 2 * dy);
        return expanded & new Rect(0, 0, imageWidth, imageHeight);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
