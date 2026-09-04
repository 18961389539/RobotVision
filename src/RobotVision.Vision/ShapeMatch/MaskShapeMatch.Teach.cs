using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Vision;

public static partial class MaskShapeMatch
{
    public static ShapeModel? BuildTeach(Mat template) =>
        BuildTeach(template, housingInCrop: null);

    /// <summary>示教转正窗 + 源图分割轮廓：写入源图原点相对轮廓质心，供运行时刚体搬运。</summary>
    public static ShapeModel? BuildTeach(UprightCropResult crop, IReadOnlyList<Point2f> sourceContour)
    {
        var hx = crop.RotationCenter.X - crop.CropOriginX;
        var hy = crop.RotationCenter.Y - crop.CropOriginY;
        var model = BuildTeach(crop.Upright, new Point2d(hx, hy));
        if (model is not null)
        {
            var origin = MapCropToSource(crop, new Point2d(model.CenterX, model.CenterY));
            model.BindSourceOrigin(origin, sourceContour);
        }
        return model;
    }

    /// <param name="housingInCrop">转正窗内壳体位置；缺省则从模板灰图阈值轮廓估计。</param>
    public static ShapeModel? BuildTeach(Mat template, Point2d? housingInCrop)
    {
        if (template.Empty())
            return null;
        using var edges = MaskTemplateMatcher.ToCanny8u(template);
        var sampled = SampleEdgePoints(edges);
        if (sampled.Points.Length < MinTeachPoints)
            return null;
        using var gray = ToGray(template);
        var (left, right) = PolarProbes(sampled.Points);
        var delta = SampleGray(gray, right.X + sampled.Cx, right.Y + sampled.Cy)
                    - SampleGray(gray, left.X + sampled.Cx, left.Y + sampled.Cy);
        var dirBins = TeachDirBins(gray, sampled.Points, sampled.Cx, sampled.Cy);
        var (partHalfW, partHalfH) = PartHalfExtents(template.Width, template.Height);
        var (hOx, hOy) = housingInCrop is { } h
            ? (sampled.Cx - h.X, sampled.Cy - h.Y)
            : TeachHousingOffset(gray, sampled.Cx, sampled.Cy);
        return new ShapeModel(sampled.Points, RadiusWeights(sampled.Points), dirBins,
            sampled.Cx, sampled.Cy, left, right, delta, gray.Clone(), partHalfW, partHalfH, hOx, hOy);
    }

    private static (double Ox, double Oy) TeachHousingOffset(Mat gray, double edgeCx, double edgeCy)
    {
        using var mask = new Mat();
        Cv2.Threshold(gray, mask, 80, 255, ThresholdTypes.Binary);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        if (contours.Length == 0)
            return (0, 0);
        var best = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        if (best.Length < 8)
            return (0, 0);
        var pts = new Point2f[best.Length];
        for (var i = 0; i < best.Length; i++)
            pts[i] = new Point2f(best[i].X, best[i].Y);
        var housing = MaskHousing.Fit(pts);
        return (edgeCx - housing.Center.X, edgeCy - housing.Center.Y);
    }

    /// <summary>转正裁剪内零件半宽/半高（与 <see cref="CropMarginRatio"/> 同口径）。</summary>
    private static (double W, double H) PartHalfExtents(int cropW, int cropH)
    {
        var denom = 2.0 * (1.0 + 2.0 * CropMarginRatio);
        return (cropW / denom, cropH / denom);
    }

    /// <summary>对每个示教边点（相对中心坐标）插值其灰度梯度方向 bin（折叠 180° 无向）。</summary>
    private static byte[] TeachDirBins(Mat gray, Point2f[] pts, double cx, double cy)
    {
        var binMap = BuildDirMap(gray);
        try
        {
            var bins = new byte[pts.Length];
            var w = binMap.Cols;
            var h = binMap.Rows;
            for (var i = 0; i < pts.Length; i++)
            {
            // 边点在其 3×3 邻域多数方向：直接采样该点 bin（Sobel 在边缘像素处方向锐利）
            // 注意：pts 是相对中心的坐标，查方向图须加回中心偏移
                var x = (int)Math.Round(Math.Clamp(pts[i].X + cx, 0, w - 1));
                var y = (int)Math.Round(Math.Clamp(pts[i].Y + cy, 0, h - 1));
                bins[i] = binMap.At<byte>(y, x);
            }
            return bins;
        }
        finally
        {
            binMap.Dispose();
        }
    }

    /// <summary>
    /// 灰度梯度方向 bin 图（CV_8U）：Sobel 两向 → atan2 方向 → 折叠 [0,180) → 量化 DirBins。
    /// 现场搜索图与示教图各算一次，搜索循环全程复用（开销 = 1 次 Sobel，可忽略）。
    /// </summary>
    private static Mat BuildDirMap(Mat gray)
    {
        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, ksize: 3);
        Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, ksize: 3);
        var binMap = new Mat(gray.Rows, gray.Cols, MatType.CV_8UC1, Scalar.All(0));
        var bx = gx.GetGenericIndexer<float>();
        var by = gy.GetGenericIndexer<float>();
        var bm = binMap.GetGenericIndexer<byte>();
        for (var y = 0; y < gray.Rows; y++)
        for (var x = 0; x < gray.Cols; x++)
        {
            var dx = bx[y, x];
            var dy = by[y, x];
            // 弱梯度（平坦区/插值模糊带）方向无意义 → 0xFF 无效标记（运行时豁免方向检查）
            if (dx * dx + dy * dy < MinDirGradientSq)
            {
                bm[y, x] = 0xFF;
                continue;
            }
            var deg = Math.Atan2(dy, dx) * 180.0 / Math.PI; // [-180,180]
            if (deg < 0)
                deg += 180.0;
            // 折叠：θ 与 θ+180 同边
            var bin = (int)(deg / DirBinWidthDeg);
            bm[y, x] = (byte)Math.Clamp(bin, 0, DirBins - 1);
        }
        return binMap;
    }

    /// <summary>Sobel 梯度幅值（CV_32F），大角度 warp 下与 DT 混合以降低 Canny 边带漂移的均距。</summary>
    private static Mat BuildGradientMagnitude(Mat gray, out float peak)
    {
        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, ksize: 3);
        Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, ksize: 3);
        var mag = new Mat(gray.Rows, gray.Cols, MatType.CV_32FC1);
        peak = 0f;
        var bx = gx.GetGenericIndexer<float>();
        var by = gy.GetGenericIndexer<float>();
        var bm = mag.GetGenericIndexer<float>();
        for (var y = 0; y < gray.Rows; y++)
        for (var x = 0; x < gray.Cols; x++)
        {
            var g = MathF.Sqrt(bx[y, x] * bx[y, x] + by[y, x] * by[y, x]);
            bm[y, x] = g;
            if (g > peak)
                peak = g;
        }
        return mag;
    }

    /// <summary>强对比度处压低距离：Canny DT 漂移时仍能对齐到真实边。</summary>
    private static double BlendDistance(
        double dtDist, MatIndexer<float>? gradIdx, float gradPeak, int w, int h, double x, double y)
    {
        if (gradIdx is null || gradPeak <= 1e-6f)
            return dtDist;
        if (x < 1 || y < 1 || x >= w - 2 || y >= h - 2)
            return dtDist;
        var grad = SampleGradNeighborhoodMax(gradIdx, w, h, x, y);
        var norm = grad / gradPeak;
        if (norm < GradEdgePeakRatio)
            return dtDist;
        var t = (norm - (float)GradEdgePeakRatio) / (float)(1.0 - GradEdgePeakRatio);
        t = Math.Clamp(t, 0, 1);
        var gradDist = HitDistPx * (1.0 - t);
        var wBlend = t * 0.8;
        return dtDist * (1.0 - wBlend) + gradDist * wBlend;
    }

    private static float SampleGradNeighborhoodMax(MatIndexer<float> gradIdx, int w, int h, double x, double y)
    {
        var max = 0f;
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        for (var dy = -2; dy <= 2; dy++)
        for (var dx = -2; dx <= 2; dx++)
        {
            var xi = Math.Clamp(x0 + dx, 0, w - 1);
            var yi = Math.Clamp(y0 + dy, 0, h - 1);
            max = Math.Max(max, gradIdx[yi, xi]);
        }
        return max;
    }

    private readonly record struct SampledEdges(Point2f[] Points, double Cx, double Cy);

    private static SampledEdges SampleEdgePoints(Mat edges)
    {
        var raw = new List<Point2f>();
        var indexer = edges.GetGenericIndexer<byte>();
        var border = Math.Min(TeachBorderPx, Math.Min(edges.Rows, edges.Cols) / 4);
        for (var y = border; y < edges.Rows - border; y++)
        for (var x = border; x < edges.Cols - border; x++)
        {
            if (indexer[y, x] == 0)
                continue;
            raw.Add(new Point2f(x, y));
        }
        if (raw.Count == 0)
            return new SampledEdges([], 0, 0);
        var cx = 0.0;
        var cy = 0.0;
        foreach (var p in raw)
        {
            cx += p.X;
            cy += p.Y;
        }
        cx /= raw.Count;
        cy /= raw.Count;
        var step = 1;
        if (raw.Count > MaxTeachPoints)
            step = Math.Max(1, (int)Math.Ceiling(raw.Count / (double)MaxTeachPoints));
        var minSq = PointStridePx * PointStridePx;
        var kept = new List<Point2f>(Math.Min(MaxTeachPoints, raw.Count));
        for (var i = 0; i < raw.Count && kept.Count < MaxTeachPoints; i += step)
        {
            var p = raw[i];
            var rel = new Point2f((float)(p.X - cx), (float)(p.Y - cy));
            var ok = true;
            for (var k = 0; k < kept.Count; k++)
            {
                var dx = rel.X - kept[k].X;
                var dy = rel.Y - kept[k].Y;
                if (dx * dx + dy * dy < minSq)
                {
                    ok = false;
                    break;
                }
            }
            if (ok)
                kept.Add(rel);
        }
        return new SampledEdges(kept.ToArray(), cx, cy);
    }

    private static double[] RadiusWeights(Point2f[] pts)
    {
        var maxR = 1.0;
        foreach (var p in pts)
        {
            var r = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            if (r > maxR)
                maxR = r;
        }
        var w = new double[pts.Length];
        for (var i = 0; i < pts.Length; i++)
        {
            var r = Math.Sqrt(pts[i].X * pts[i].X + pts[i].Y * pts[i].Y) / maxR;
            w[i] = 0.35 + r * r;
        }
        return w;
    }
}
