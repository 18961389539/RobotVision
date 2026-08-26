using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Geometry;

/// <summary>
/// 最小外接矩形长边方向：OpenCV MinAreaRect 留在 Infrastructure，Core 只保留纯函数归一化。
/// </summary>
public static class MinAreaRectGeometry
{
    public static (ImagePoint Center, double AngleDeg) LongAxis(IReadOnlyList<ImagePoint> contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        var points = new Point2f[contour.Count];
        for (var i = 0; i < contour.Count; i++)
            points[i] = new Point2f((float)contour[i].X, (float)contour[i].Y);
        return LongAxis(points);
    }

    public static (ImagePoint Center, double AngleDeg) LongAxis(IReadOnlyList<Point2f> contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        var rect = Cv2.MinAreaRect(contour);

        // OpenCV >= 4.5 约定 angle ∈ (0, 90]，且不保证 Width 对应长边，
        // 统一换算为长边方向后再归一化。
        var deg = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;
        return (new ImagePoint(rect.Center.X, rect.Center.Y), AngleGeometry.NormalizeDeg(deg));
    }
}
