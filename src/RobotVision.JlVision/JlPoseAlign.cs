using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.JlVision;

/// <summary>精修后处理：出角与 Chamfer 同口径融合线拟合；出心为壳体中心 + 示教偏移。</summary>
public static class JlPoseAlign
{
    /// <summary>
    /// 线拟合与转正粗角相差 &lt;1.5° 时用线拟合方向，头尾仍跟 candidate。
    /// </summary>
    public static double AlignToTeachAngle(IReadOnlyList<Point2f> contour, double candidateDeg)
    {
        var housing = JlHousing.Fit(contour);
        var warp = housing.WarpAngleDeg;
        var seed = Math.Abs(AngleGeometry.NormalizeSignedDeg(warp)) > 90.0
            ? AngleGeometry.CanonWarpDeg(warp)
            : warp;
        var (lfDeg, _, lfOk) = TryLineFit(contour, seed);
        if (!lfOk)
            return candidateDeg;

        var dSeed = Math.Abs(AngleGeometry.NormalizeSignedDeg(lfDeg - seed));
        if (dSeed >= 1.5)
            return candidateDeg;

        var fused = AngleGeometry.FuseDirected(lfDeg, candidateDeg);
        var a = AngleGeometry.NormalizeSignedDeg(fused);
        if (Math.Abs(a) > 90.0)
            a = AngleGeometry.NormalizeSignedDeg(a + 180.0);
        return a;
    }

    /// <summary>壳体中心 + R(θ)×HousingOffset（示教图上边缘中心相对壳体的偏移）。</summary>
    public static Point2d AlignToTeachOrigin(
        IReadOnlyList<Point2f> contour, double angleDeg, double housingOffsetX, double housingOffsetY)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var housing = JlHousing.Fit(contour);
        return new Point2d(
            housing.Center.X + housingOffsetX * cos - housingOffsetY * sin,
            housing.Center.Y + housingOffsetX * sin + housingOffsetY * cos);
    }

    public static (double Ox, double Oy) TeachHousingOffset(Mat templateGray)
    {
        using var gray = templateGray.Channels() == 1 ? templateGray.Clone() : JlImageConvert.ToGray(templateGray);
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
        var housing = JlHousing.Fit(pts);
        var cx = gray.Width / 2.0;
        var cy = gray.Height / 2.0;
        return (cx - housing.Center.X, cy - housing.Center.Y);
    }

    public static (double AngleDeg, Point2d Center, bool Fitted) TryLineFit(
        IReadOnlyList<Point2f> contour, double coarseAngleDeg)
    {
        var horizontal = TryLineFitBands(contour, coarseAngleDeg, horizontalBands: true);
        if (horizontal.Fitted)
            return horizontal;
        return TryLineFitBands(contour, coarseAngleDeg, horizontalBands: false);
    }

    private static (double AngleDeg, Point2d Center, bool Fitted) TryLineFitBands(
        IReadOnlyList<Point2f> contour, double coarseAngleDeg, bool horizontalBands)
    {
        var ptsSrc = JlHousing.CorePoints(contour);
        var cx = ptsSrc.Average(p => p.X);
        var cy = ptsSrc.Average(p => p.Y);

        var rad = -coarseAngleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var pts = new Point2f[ptsSrc.Length];
        for (var i = 0; i < ptsSrc.Length; i++)
        {
            var dx = ptsSrc[i].X - cx;
            var dy = ptsSrc[i].Y - cy;
            pts[i] = new Point2f(
                (float)(cx + dx * cos - dy * sin),
                (float)(cy + dx * sin + dy * cos));
        }

        double yMin = float.MaxValue, yMax = float.MinValue, xMin = float.MaxValue, xMax = float.MinValue;
        foreach (var p in pts)
        {
            if (p.Y < yMin) yMin = p.Y;
            if (p.Y > yMax) yMax = p.Y;
            if (p.X < xMin) xMin = p.X;
            if (p.X > xMax) xMax = p.X;
        }
        var h = yMax - yMin;
        var w = xMax - xMin;
        if (h <= 0 || w <= 0)
            return (coarseAngleDeg, new(cx, cy), false);

        var yCut = yMin + 0.35 * h;
        var xCut = xMin + 0.35 * w;
        var xLo = xMin + 0.15 * w;
        var xHi = xMax - 0.15 * w;
        var yLo = yMin + 0.15 * h;
        var yHi = yMax - 0.15 * h;
        Point2f[] bandA;
        Point2f[] bandB;
        if (horizontalBands)
        {
            bandA = pts.Where(p => p.Y <= yCut && p.X >= xLo && p.X <= xHi).ToArray();
            bandB = pts.Where(p => p.Y >= yMax - 0.35 * h && p.X >= xLo && p.X <= xHi).ToArray();
        }
        else
        {
            bandA = pts.Where(p => p.X <= xCut && p.Y >= yLo && p.Y <= yHi).ToArray();
            bandB = pts.Where(p => p.X >= xMax - 0.35 * w && p.Y >= yLo && p.Y <= yHi).ToArray();
        }

        if (bandA.Length < 8 || bandB.Length < 8)
            return (coarseAngleDeg, new(cx, cy), false);

        var aTop = FitLineAngleDeg(bandA);
        var aBottom = FitLineAngleDeg(bandB);
        if (double.IsNaN(aTop) || double.IsNaN(aBottom))
            return (coarseAngleDeg, new(cx, cy), false);

        var delta = (aTop + aBottom) / 2.0;
        return (AngleGeometry.NormalizeDeg(coarseAngleDeg + delta), new Point2d(cx, cy), true);
    }

    private static double FitLineAngleDeg(Point2f[] points)
    {
        if (points.Length < 8)
            return double.NaN;
        var line = Cv2.FitLine(points, DistanceTypes.Huber, 0, 0.01, 0.01);
        var deg = Math.Atan2(line.Vy, line.Vx) * 180.0 / Math.PI;
        var d = ((deg + 90.0) % 180.0 + 180.0) % 180.0 - 90.0;
        return d == -90.0 ? 90.0 : d;
    }
}
