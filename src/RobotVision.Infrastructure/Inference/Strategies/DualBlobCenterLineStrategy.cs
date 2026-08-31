using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 模式五：双BLOB连线（纯图像处理，无需模型）。阈值分割 + 连通域分析：
/// 主 BLOB（面积在 [MinArea,MaxArea] 内）质心定位 XY；主包围盒按 CropExpandRatio 四边外扩
/// 圈定次 BLOB 搜索窗口，窗口内面积合格且距主质心 [Min,Max]PairDistancePx 的最近连通域
/// 为次 BLOB；角度 = 主质心→次质心连线（(-180,180] 有方向，主次有序天然消 180° 歧义）。
/// 次 BLOB 缺失时该目标不输出（不猜测配对，与双模型模式同语义）。
/// 全图只做一次阈值+标记，次 BLOB 复用标记结果按质心入窗筛选，不为每个主 BLOB 重裁重算。
/// </summary>
public sealed class DualBlobCenterLineStrategy : IAngleStrategy
{
    /// <summary>单个连通域的标记结果（坐标为分析图内坐标系）。</summary>
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
        double ox = 0, oy = 0;
        using var roiOwned = recipe.Roi is null ? null : RoiHelper.Crop(gray, recipe.Roi, out ox, out oy);
        var view = roiOwned ?? gray;

        using var binary = Binarize(view, opt);
        ct.ThrowIfCancellationRequested();

        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);
        ct.ThrowIfCancellationRequested();

        // label 0 是背景
        var blobs = new List<BlobInfo>(Math.Max(0, count - 1));
        for (var i = 1; i < count; i++)
        {
            var area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
            blobs.Add(new BlobInfo(
                i, area,
                centroids.At<double>(i, 0), centroids.At<double>(i, 1),
                new Rect(
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Left),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Top),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Width),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Height))));
        }

        var poses = new List<PixelPose>();
        foreach (var primary in blobs)
        {
            if (primary.Area < opt.MinArea || primary.Area > opt.MaxArea)
                continue;

            var window = Expand(primary.Box, opt.CropExpandRatio, view.Width, view.Height);
            BlobInfo? best = null;
            var bestDistance = double.MaxValue;
            foreach (var candidate in blobs)
            {
                if (candidate.Label == primary.Label ||
                    candidate.Area < opt.SecondaryMinArea ||
                    candidate.Area > opt.SecondaryMaxArea ||
                    candidate.Cx < window.Left || candidate.Cx >= window.Right ||
                    candidate.Cy < window.Top || candidate.Cy >= window.Bottom)
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
            // Score 无模型置信度可用：取主面积占面积窗上限比例（单调稳定，仅用于多位姿排序）
            var score = Math.Clamp((double)primary.Area / opt.MaxArea, 0, 1);
            // 叠加给主/次 BLOB 包围盒（连通域统计量，零额外开销；不提取轮廓避免每帧 FindContours）
            poses.Add(new PixelPose(primary.Cx + ox, primary.Cy + oy, angleDeg, score)
            {
                Overlay = new PoseOverlay
                {
                    Boxes =
                    [
                        new PixelRect(primary.Box.X + ox, primary.Box.Y + oy,
                            primary.Box.Width, primary.Box.Height),
                        new PixelRect(secondary.Box.X + ox, secondary.Box.Y + oy,
                            secondary.Box.Width, secondary.Box.Height),
                    ],
                    // 角度基线：主质心 → 次质心（与角度计算同两点，画面上可验证配对）
                    Baseline =
                    [
                        new PixelPoint(primary.Cx + ox, primary.Cy + oy),
                        new PixelPoint(secondary.Cx + ox, secondary.Cy + oy),
                    ],
                },
            });
        }

        return poses.OrderByDescending(p => p.Score).ToList();
    }

    /// <summary>灰度化：BGR/BGRA 转单通道；单通道与浮点图归一到 8U（阈值分割要求）。</summary>
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

        // 16U/32F 等非 8U：按类型全量程归一到 [0,255] 再二值化（工业相机 10/12bit 常见）
        var normalized = new Mat();
        Cv2.Normalize(gray, normalized, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        gray.Dispose();
        return normalized;
    }

    /// <summary>二值化（Otsu/固定阈值，亮/暗）+ 可选开运算分离轻微粘连的主次 BLOB。</summary>
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

    /// <summary>包围盒四边各外扩（边长×ratio），裁剪到图像范围内。</summary>
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
