using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.JlVision;

/// <summary>质心-内孔/细长槽连线（分割位掩码几何，不依赖 Chamfer）。</summary>
public static class JlCentroidHole
{
    public readonly record struct Result(double AngleDeg, Point2d Centroid, double Quality);

    private const double MinHoleAreaPx = 30;
    private const double MinBaselinePx = 5;
    private const double SlotAspectThreshold = 2.5;

    public static Result? TryRefine(byte[] bitPackedMask, int width, int height)
    {
        if (bitPackedMask.Length == 0 || width <= 0 || height <= 0)
            return null;

        var buf = new byte[width * height];
        for (var i = 0; i < buf.Length; i++)
            buf[i] = (byte)(((bitPackedMask[i >> 3] >> (i & 7)) & 1) * 255);
        using var mask = Mat.FromPixelData(height, width, MatType.CV_8UC1, buf);

        var m = Cv2.Moments(mask, true);
        if (Math.Abs(m.M00) < 1e-9)
            return null;
        var centroid = new Point2d(m.M10 / m.M00, m.M01 / m.M00);

        Cv2.FindContours(mask, out var contours, out var hierarchy, RetrievalModes.CComp,
            ContourApproximationModes.ApproxSimple);
        double bestArea = 0;
        Point[]? bestHole = null;
        for (var i = 0; i < contours.Length; i++)
        {
            if (hierarchy[i].Parent < 0)
                continue;
            var area = Cv2.ContourArea(contours[i]);
            if (area < MinHoleAreaPx || area <= bestArea)
                continue;
            bestArea = area;
            bestHole = contours[i];
        }
        if (bestHole is null || bestHole.Length < 5)
            return null;

        using var holeArray = InputArray.Create(bestHole);
        var ellipse = Cv2.FitEllipse(holeArray);
        var holeCenter = new Point2d(ellipse.Center.X, ellipse.Center.Y);

        var dx = holeCenter.X - centroid.X;
        var dy = holeCenter.Y - centroid.Y;
        if (dx * dx + dy * dy < MinBaselinePx * MinBaselinePx)
            return null;

        var major = Math.Max(ellipse.Size.Width, ellipse.Size.Height);
        var minor = Math.Min(ellipse.Size.Width, ellipse.Size.Height);
        double angleDeg;
        if (major >= SlotAspectThreshold * minor)
        {
            var axisDeg = AngleGeometry.NormalizeDeg(
                ellipse.Size.Width >= ellipse.Size.Height ? ellipse.Angle : ellipse.Angle + 90.0);
            var rad = axisDeg * Math.PI / 180.0;
            var ax = Math.Cos(rad);
            var ay = Math.Sin(rad);
            var along = dx * ax + dy * ay;
            var across = -dx * ay + dy * ax;
            var bit = Math.Abs(along) >= Math.Abs(across) ? Math.Sign(along) : Math.Sign(across);
            angleDeg = bit >= 0 ? axisDeg : AngleGeometry.NormalizeSignedDeg(axisDeg + 180.0);
        }
        else
        {
            var (_, ang) = AngleGeometry.FromTwoPoints(centroid.X, centroid.Y, holeCenter.X, holeCenter.Y);
            angleDeg = ang;
        }

        var offset = Math.Sqrt(dx * dx + dy * dy);
        var quality = Math.Clamp(
            0.45 + 0.30 * Math.Clamp(bestArea / 200.0, 0, 1) + 0.25 * Math.Clamp(offset / 16.0, 0, 1),
            0.45, 0.98);
        return new Result(angleDeg, centroid, quality);
    }
}
