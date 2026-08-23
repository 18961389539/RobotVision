using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>
/// 旋转中心标定：第 4 轴带标记（针尖/特征点）旋转多个角度，记录标记的像素坐标，
/// 拟合轨迹圆得到轴心。≥5 点用 FitEllipse（附带长短轴比质检），3~4 点用代数圆拟合。
/// 角度须等间隔拉开（如 0°/120°/240° 或每 45°），近距角度会使圆拟合病态。
/// </summary>
public static class RotationCenterCalibrator
{
    public static RotationCenterProfile Calibrate(string stationId, string cameraId, Point2f[] points)
    {
        if (points.Length < 3)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"旋转中心标定至少需要 3 个点（推荐 5~9 个角度），当前 {points.Length} 个");

        var maxPairwise = MaxPairwiseDistance(points);
        if (maxPairwise < 2.0)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                "标记点几乎重合：旋转轴心与标记近乎共轴（工具同心，无需补偿），或第 4 轴未实际旋转");

        double cx, cy, radius, axisRatio;
        if (points.Length >= 5)
            (cx, cy, radius, axisRatio) = FitEllipseCenter(points);
        else
            (cx, cy, radius, axisRatio) = FitCircleCenter(points);

        // 拟合半径远大于点跨度 = 角度跨度不足（近距角度），圆心误差被放大
        if (radius > 2.0 * maxPairwise)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"拟合半径 {radius:0.0}px 远大于标记点跨度 {maxPairwise:0.0}px：旋转角度跨度不足，请等间隔分布（如 0°/120°/240° 或每 45°）");

        double sumSq = 0;
        foreach (var p in points)
        {
            var dev = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)) - radius;
            sumSq += dev * dev;
        }

        return new RotationCenterProfile
        {
            StationId = stationId,
            CameraId = cameraId,
            Cx = cx,
            Cy = cy,
            RadiusPx = radius,
            Rms = Math.Sqrt(sumSq / points.Length),
            AxisRatio = axisRatio,
            PointCount = points.Length,
            CalibratedAt = DateTime.Now,
        };
    }

    private static (double Cx, double Cy, double Radius, double AxisRatio) FitEllipseCenter(Point2f[] points)
    {
        try
        {
            using var mat = Mat.FromArray(points);
            var ellipse = Cv2.FitEllipse(mat);
            var a = ellipse.Size.Width / 2.0;
            var b = ellipse.Size.Height / 2.0;
            var radius = (a + b) / 2.0;
            var axisRatio = Math.Max(a, b) / Math.Max(Math.Min(a, b), 1e-9);
            return (ellipse.Center.X, ellipse.Center.Y, radius, axisRatio);
        }
        catch (OpenCVException ex)
        {
            throw new VisionException(VisionErrorCode.NotCalibrated, $"椭圆拟合失败: {ex.Message}");
        }
    }

    /// <summary>代数圆拟合（Kåsa 法）：x²+y² = D·x + E·y + F 的最小二乘解，3 点时即精确外接圆。</summary>
    private static (double Cx, double Cy, double Radius, double AxisRatio) FitCircleCenter(Point2f[] points)
    {
        using var a = new Mat(points.Length, 3, MatType.CV_64F);
        using var b = new Mat(points.Length, 1, MatType.CV_64F);
        for (var i = 0; i < points.Length; i++)
        {
            var x = (double)points[i].X;
            var y = (double)points[i].Y;
            a.Set(i, 0, x);
            a.Set(i, 1, y);
            a.Set(i, 2, 1.0);
            b.Set(i, 0, x * x + y * y);
        }

        using var sol = new Mat();
        if (!Cv2.Solve(a, b, sol, DecompTypes.SVD))
            throw new VisionException(VisionErrorCode.NotCalibrated, "圆拟合失败：标记点共线或数据异常");

        var cx = sol.At<double>(0, 0) / 2.0;
        var cy = sol.At<double>(1, 0) / 2.0;
        var r2 = sol.At<double>(2, 0) + cx * cx + cy * cy;
        if (r2 <= 0)
            throw new VisionException(VisionErrorCode.NotCalibrated, "圆拟合结果无效：请检查标记点数据");

        return (cx, cy, Math.Sqrt(r2), 1.0);
    }

    private static double MaxPairwiseDistance(Point2f[] points)
    {
        double max = 0;
        for (var i = 0; i < points.Length; i++)
            for (var j = i + 1; j < points.Length; j++)
                max = Math.Max(max, points[i].DistanceTo(points[j]));
        return max;
    }
}
