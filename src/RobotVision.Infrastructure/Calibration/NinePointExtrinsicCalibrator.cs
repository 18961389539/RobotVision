using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>
/// 九点外参标定：机器人带标定针走 9 个点，同时记录像素坐标与机器人坐标，
/// EstimateAffine2D 求像素→机器人的 2x3 仿射矩阵。
/// 建议点位覆盖工作视场，避免共线。
/// </summary>
public static class NinePointExtrinsicCalibrator
{
    public static ExtrinsicProfile Calibrate(
        string stationId, string cameraId, Point2f[] pixelPoints, Point2f[] robotPoints)
    {
        if (pixelPoints.Length != robotPoints.Length || pixelPoints.Length < 3)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"外参标定至少需要 3 组对应点（推荐 9 点），当前 {Math.Min(pixelPoints.Length, robotPoints.Length)} 组");

        // 共线/重合防护：三点最大面积过小说明点集退化（共线/重合），仿射解病态。
        // 建议点位覆盖工作视场四角与中心；只做绝对退化拒绝，不误伤小分布。
        var maxTriArea = MaxTriangleArea(pixelPoints);
        if (maxTriArea < 1e-3)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                "像素点近似共线或重合，仿射估计会病态：请让 9 点覆盖视场四角与中心，避免共线");

        using var src = Mat.FromArray(pixelPoints);
        using var dst = Mat.FromArray(robotPoints);
        using var affine = Cv2.EstimateAffine2D(src, dst)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, "仿射估计失败，请检查点位数据");

        var a = new double[6];
        for (var i = 0; i < 2; i++)
            for (var j = 0; j < 3; j++)
                a[i * 3 + j] = affine.At<double>(i, j);

        double sumSq = 0, maxResidual = 0;
        var residuals = new double[pixelPoints.Length];
        for (var i = 0; i < pixelPoints.Length; i++)
        {
            var tx = a[0] * pixelPoints[i].X + a[1] * pixelPoints[i].Y + a[2];
            var ty = a[3] * pixelPoints[i].X + a[4] * pixelPoints[i].Y + a[5];
            var dx = tx - robotPoints[i].X;
            var dy = ty - robotPoints[i].Y;
            var residual = Math.Sqrt(dx * dx + dy * dy);
            residuals[i] = residual;
            sumSq += residual * residual;
            maxResidual = Math.Max(maxResidual, residual);
        }

        // 留一交叉验证：对每个点，用其余点拟合后预测该点误差——单个抄错/误点在此处最敏感
        var leaveOneOutMax = 0.0;
        for (var i = 0; i < pixelPoints.Length; i++)
        {
            var othersPx = new Point2f[pixelPoints.Length - 1];
            var othersRob = new Point2f[robotPoints.Length - 1];
            var t = 0;
            for (var j = 0; j < pixelPoints.Length; j++)
            {
                if (j == i)
                    continue;
                othersPx[t] = pixelPoints[j];
                othersRob[t] = robotPoints[j];
                t++;
            }

            if (othersPx.Length < 3)
                continue;

            using var s = Mat.FromArray(othersPx);
            using var d = Mat.FromArray(othersRob);
            using var fit = Cv2.EstimateAffine2D(s, d);
            if (fit is null)
                continue;

            // 用留一拟合的仿射预测该点（而非整体仿射），才能暴露单个误点
            var px = fit.At<double>(0, 0) * pixelPoints[i].X + fit.At<double>(0, 1) * pixelPoints[i].Y + fit.At<double>(0, 2);
            var py = fit.At<double>(1, 0) * pixelPoints[i].X + fit.At<double>(1, 1) * pixelPoints[i].Y + fit.At<double>(1, 2);
            var err = Math.Sqrt(
                (px - robotPoints[i].X) * (px - robotPoints[i].X) +
                (py - robotPoints[i].Y) * (py - robotPoints[i].Y));
            leaveOneOutMax = Math.Max(leaveOneOutMax, err);
        }

        return new ExtrinsicProfile
        {
            StationId = stationId,
            CameraId = cameraId,
            Affine = a,
            Rms = Math.Sqrt(sumSq / pixelPoints.Length),
            MaxResidual = maxResidual,
            PointResiduals = residuals,
            LeaveOneOutMax = leaveOneOutMax,
            CalibratedAt = DateTime.Now,
        };
    }

    /// <summary>任意三点组成的最大三角形面积（绝对共线退化检测用）。</summary>
    private static double MaxTriangleArea(Point2f[] points)
    {
        double max = 0;
        for (var i = 0; i < points.Length; i++)
            for (var j = i + 1; j < points.Length; j++)
                for (var k = j + 1; k < points.Length; k++)
                {
                    var area = Math.Abs(
                        (points[j].X - points[i].X) * (points[k].Y - points[i].Y) -
                        (points[j].Y - points[i].Y) * (points[k].X - points[i].X)) / 2.0;
                    max = Math.Max(max, area);
                }
        return max;
    }
}
