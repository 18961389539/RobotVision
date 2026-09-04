using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Vision;

public static partial class MaskShapeMatch
{
    /// <summary>
    /// 输出角：壳体线拟合修正 MinAreaRect 小偏差 + NCC/Chamfer 残差一致时优先 NCC。
    /// 不改变 Chamfer 搜索坐标系（仍用转正 warp），仅修正报告角。
    /// </summary>
    private static double ResolveOutputAngleDeg(
        IReadOnlyList<Point2f> contour, double cropWarpDeg, double residualDeg, NccSeed ncc,
        double hitRate, double meanDist)
    {
        var (lfDeg, _, lfOk) = MaskTemplateMatcher.RefineByLineFit(contour, cropWarpDeg);
        var chamferAngle = AngleGeometry.NormalizeSignedDeg(cropWarpDeg + residualDeg);
        // 180° 极性支：CanonWarp 把 180 折到 ~0 再加 extraWarp；用无向线拟合补符号。
        if (lfOk && Math.Abs(cropWarpDeg) >= 150.0)
            return AngleGeometry.FuseDirected(lfDeg, chamferAngle);
        // 贴边强：线拟合与 MinAreaRect 接近则用线拟合（0° 缺口使矩形角偏 0.3°）；
        // 无向线拟合落在补角时 |lf-warp|≈180，改信 warp（残差是 0.25° 网格，不叠加）。
        if (hitRate >= 0.55 && meanDist <= 2.5)
        {
            if (lfOk)
            {
                var dWarp = Math.Abs(AngleGeometry.NormalizeSignedDeg(lfDeg - cropWarpDeg));
                if (dWarp < 1.5)
                    return lfDeg;
            }
            return cropWarpDeg;
        }
        var warp = cropWarpDeg;
        if (lfOk)
        {
            var dWarp = Math.Abs(AngleGeometry.NormalizeSignedDeg(lfDeg - cropWarpDeg));
            if (dWarp < 1.5)
                warp = lfDeg;
        }
        var angle = AngleGeometry.NormalizeSignedDeg(warp + residualDeg);
        if (!NccAngleTrustworthy(ncc) || ncc.Score < NccFallbackMinScore)
            return angle;
        var overlayStrong = hitRate >= 0.55 && meanDist <= 2.5;
        if (overlayStrong)
            return angle;
        var nccAngle = AngleGeometry.NormalizeSignedDeg(warp + ncc.RotationDeg);
        var resGap = Math.Abs(AngleGeometry.NormalizeSignedDeg(residualDeg - ncc.RotationDeg));
        var outGap = Math.Abs(AngleGeometry.NormalizeSignedDeg(angle - nccAngle));
        var chamferStrong = meanDist <= 2.0 && Math.Abs(residualDeg) <= 0.20;
        if (!chamferStrong && resGap > 0.22 && outGap > 0.15 && (Math.Abs(warp) < 20.0 || ncc.Score >= 0.32))
            angle = nccAngle;
        return angle;
    }

    /// <summary>
    /// 输出示教边点质心在源图的位置（Halcon 模型原点）。
    /// 已绑定源图偏移时：现场轮廓多边形质心 + R(报告角)×示教偏移（仿射不变量）。
    /// 否则转正窗壳体 + HousingOffset，经 WarpAffine dest→src 映回。
    /// </summary>
    private static Point2d ResolveOutputCenter(
        IReadOnlyList<Point2f> contour, UprightCropResult crop, ShapeModel model,
        MatchHit hit, NccSeed ncc, double angleDeg)
    {
        _ = ncc;
        if (model.HasSourceOrigin)
        {
            var c = PolygonCentroid(contour);
            var rad = angleDeg * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            return new Point2d(
                c.X + model.SourceOx * cos - model.SourceOy * sin,
                c.Y + model.SourceOx * sin + model.SourceOy * cos);
        }
        var geo = GeometricOriginInCrop(crop, model, residualDeg: 0);
        if (hit.HitRate >= 0.55 && hit.MeanDistPx <= 2.5)
        {
            var dx = hit.CenterInUpright.X - geo.X;
            var dy = hit.CenterInUpright.Y - geo.Y;
            if (dx * dx + dy * dy <= 0.08 * 0.08)
                return MapCropToSource(crop, hit.CenterInUpright);
            return MapCropToSource(crop, geo);
        }
        if (hit.HitRate >= ActiveMinHitRate && hit.MeanDistPx <= ActiveMaxMeanDist)
            return MapCropToSource(crop, hit.CenterInUpright);
        return MapCropToSource(crop, geo);
    }

    /// <summary>多边形有符号面积质心（对仿射变换的轮廓点封闭刚体）。</summary>
    internal static Point2d PolygonCentroid(IReadOnlyList<Point2f> contour)
    {
        var n = contour.Count;
        if (n < 3)
            return default;
        var area2 = 0.0;
        var cx = 0.0;
        var cy = 0.0;
        for (var i = 0; i < n; i++)
        {
            var p0 = contour[i];
            var p1 = contour[(i + 1) % n];
            var cross = (double)p0.X * p1.Y - (double)p1.X * p0.Y;
            area2 += cross;
            cx += (p0.X + p1.X) * cross;
            cy += (p0.Y + p1.Y) * cross;
        }
        if (Math.Abs(area2) < 1e-9)
        {
            var sx = 0.0;
            var sy = 0.0;
            for (var i = 0; i < n; i++)
            {
                sx += contour[i].X;
                sy += contour[i].Y;
            }
            return new Point2d(sx / n, sy / n);
        }
        return new Point2d(cx / (3.0 * area2), cy / (3.0 * area2));
    }

    /// <summary>转正窗内：壳体亚像素位置 + 示教原点相对壳体的残差旋转。</summary>
    private static Point2d GeometricOriginInCrop(UprightCropResult crop, ShapeModel model, double residualDeg)
    {
        var hx = crop.RotationCenter.X - crop.CropOriginX;
        var hy = crop.RotationCenter.Y - crop.CropOriginY;
        var rad = residualDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var ox = model.HousingOffsetX * cos - model.HousingOffsetY * sin;
        var oy = model.HousingOffsetX * sin + model.HousingOffsetY * cos;
        return new Point2d(hx + ox, hy + oy);
    }

    /// <summary>转正窗像素 → 源图：WarpAffine dest→src（GetRotationMatrix2D(-θ)，不 Invert）。</summary>
    internal static Point2d MapCropToSource(UprightCropResult crop, Point2d centerInUpright)
    {
        var rx = centerInUpright.X + crop.CropOriginX;
        var ry = centerInUpright.Y + crop.CropOriginY;
        using var m = Cv2.GetRotationMatrix2D(crop.RotationCenter, -crop.WarpAngleDeg, 1.0);
        return new Point2d(
            m.At<double>(0, 0) * rx + m.At<double>(0, 1) * ry + m.At<double>(0, 2),
            m.At<double>(1, 0) * rx + m.At<double>(1, 1) * ry + m.At<double>(1, 2));
    }

    /// <summary>兼容测试：壳体参数忽略，映射与 <see cref="MapCropToSource"/> 相同。</summary>
    internal static Point2d MapCropOffsetToSource(
        UprightCropResult crop, Point2d centerInUpright, Point2f housingCenter)
    {
        _ = housingCenter;
        return MapCropToSource(crop, centerInUpright);
    }

    private static bool PreferFlippedCrop(
        double polar0, double polar180, double polarTeach, MatchHit? a, MatchHit? b)
    {
        if (b is null)
            return false;
        if (a is null)
            return true; // 主窗未过门而翻转窗命中：翻转是唯一生存假设
        if (Math.Abs(polarTeach) < 12)
            // 示教极性弱（近对称件）：180° 相位本就难以观测，信任首达主窗，
            // 仅当翻转窗几何显著更优时才翻转（加大几何优势门槛防抖动）。
            return b.MeanDistPx + 0.5 < a.MeanDistPx;
        // 极性证据门：主/翻两支极性须分离到示教幅度的显著比例才可判 180° 相位。
        // 中小角度旋转使灰度探针落在插值模糊区，polar0≈polar180≈0（同号小值），
        // 旧逻辑 1e-6 级噪声差即误走翻转支 → 输出差 180°（实测 8.7°/20° 事故）。
        // 证据不足时信任主窗（首达且几何已过门）比赌翻转更安全。
        if (Math.Abs(polar0 - polar180) < 0.5 * Math.Abs(polarTeach))
            return false;
        // 主窗极性不可观测（≈0）时不可单凭翻转窗极性投票——中大角实测 37° 误翻至 -143°。
        if (Math.Abs(polar0) < 0.35 * Math.Abs(polarTeach))
            return false;
        return polar180 > polar0;
    }

    private static double PolarAgree(Mat upright, ShapeModel model, MatchHit hit)
    {
        using var gray = ToGray(upright);
        var rad = hit.RotationDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2d Map(Point2f p) => new(
            hit.CenterInUpright.X + p.X * cos - p.Y * sin,
            hit.CenterInUpright.Y + p.X * sin + p.Y * cos);
        var l = Map(model.PolarLeft);
        var r = Map(model.PolarRight);
        var delta = SampleGray(gray, r.X, r.Y) - SampleGray(gray, l.X, l.Y);
        return Math.Sign(model.PolarDelta) * delta;
    }

    private static (Point2f Left, Point2f Right) PolarProbes(Point2f[] pts)
    {
        var minX = pts[0].X;
        var maxX = pts[0].X;
        var minY = pts[0].Y;
        var maxY = pts[0].Y;
        foreach (var p in pts)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }
        var sx = Math.Max(8, maxX - minX);
        var sy = Math.Max(8, maxY - minY);
        return (
            new Point2f(minX + 0.22f * sx, minY + 0.32f * sy),
            new Point2f(maxX - 0.22f * sx, maxY - 0.32f * sy));
    }

    private static ShapeViz BuildViz(
        Mat dt, Point2f[] pts, double deg, double mx, double my,
        ChamferScale scale = default, ShapeSearchDebug? searchDebug = null, int pyramidLevels = 1)
    {
        var ax = scale.Active ? scale.Ax : 1.0;
        var ay = scale.Active ? scale.Ay : 1.0;
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var inn = new List<Point2d>();
        var rej = new List<Point2d>();
        var hist = new int[8];
        var indexer = dt.GetGenericIndexer<float>();
        var w = dt.Cols;
        var h = dt.Rows;
        foreach (var p in pts)
        {
            var px = p.X * ax;
            var py = p.Y * ay;
            var x = mx + px * cos - py * sin;
            var y = my + px * sin + py * cos;
            var at = new Point2d(x, y);
            if (x < 1 || y < 1 || x >= w - 2 || y >= h - 2)
            {
                rej.Add(at);
                hist[7]++;
                continue;
            }
            var dist = SampleBilinear(indexer, x, y);
            var bin = Math.Clamp((int)Math.Floor(dist), 0, 7);
            hist[bin]++;
            if (dist <= HitDistPx)
                inn.Add(at);
            else
                rej.Add(at);
        }
        return new ShapeViz(inn, rej, searchDebug, hist, pyramidLevels);
    }

    private static ShapeViz MapVizToSource(UprightCropResult crop, ShapeViz viz)
    {
        Point2d Map(Point2d p) => MaskTemplateMatcher.MapUprightToSource(crop, p);
        return new ShapeViz(
            viz.Inliers.Select(Map).ToArray(),
            viz.Rejected.Select(Map).ToArray(),
            viz.SearchDebug,
            viz.DistHistogram,
            viz.PyramidLevels);
    }
}
