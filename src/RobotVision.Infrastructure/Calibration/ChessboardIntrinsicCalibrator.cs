using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>
/// 棋盘格内参标定：FindChessboardCornersSB（自带子像素）+ CalibrateCamera。
/// 拍摄建议：15~25 张，覆盖视场四角，姿态多样（倾斜 20°~45°）。
/// </summary>
public static class ChessboardIntrinsicCalibrator
{
    /// <summary>算法允许的最少图像数（低于此数直接拒绝）。</summary>
    public const int MinImageCount = 10;

    /// <summary>推荐图像数（15~25 张覆盖四角、姿态多样；少于推荐值时质量通常不足）。</summary>
    public const int RecommendedImageCount = 15;

    public static IntrinsicProfile Calibrate(
        string cameraId, IReadOnlyList<string> imageFiles, Size patternSize, double squareSizeMm)
    {
        if (imageFiles.Count < MinImageCount)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"图像数量不足: {imageFiles.Count}/{MinImageCount}");

        // 棋盘规格/单元尺寸前置校验：非法值会让 board 点阵构造异常或标定结果无意义
        if (patternSize.Width <= 0 || patternSize.Height <= 0)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"棋盘格内角点规格非法: {patternSize.Width}x{patternSize.Height}");
        if (squareSizeMm <= 0 || !double.IsFinite(squareSizeMm))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"棋盘格单元尺寸非法: {squareSizeMm}mm");

        var board = new Point3f[patternSize.Width * patternSize.Height];
        var k = 0;
        for (var y = 0; y < patternSize.Height; y++)
            for (var x = 0; x < patternSize.Width; x++)
                board[k++] = new Point3f(x * (float)squareSizeMm, y * (float)squareSizeMm, 0f);

        var objectPoints = new List<Mat>();
        var imagePoints = new List<Mat>();
        Mat[] rvecs = [];
        Mat[] tvecs = [];
        var imageSize = new Size(0, 0);
        try
        {
            for (var idx = 0; idx < imageFiles.Count; idx++)
            {
                using var img = Cv2.ImRead(imageFiles[idx], ImreadModes.Color);
                if (img.Empty())
                    continue;

                // 批次分辨率一致性校验：CalibrateCamera 的 imageSize 是全局的，混合分辨率
                // 会把内参映射到错误的坐标系（imageSize 取最后一张），标定结果完全错误
                if (imageSize.Width == 0)
                {
                    imageSize = img.Size();
                }
                else if (img.Size() != imageSize)
                {
                    throw new VisionException(VisionErrorCode.NotCalibrated,
                        $"图像 {Path.GetFileName(imageFiles[idx])} 分辨率 {img.Width}x{img.Height} " +
                        $"与批次首张 {imageSize.Width}x{imageSize.Height} 不一致（混合分辨率无法标定）");
                }

                using var gray = new Mat();
                Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);

                if (!Cv2.FindChessboardCornersSB(gray, patternSize, out var corners))
                    continue;

                objectPoints.Add(Mat.FromArray(board));
                imagePoints.Add(Mat.FromArray(corners));
            }

            if (imagePoints.Count < MinImageCount)
                throw new VisionException(VisionErrorCode.NotCalibrated,
                    $"有效棋盘图像不足: {imagePoints.Count}/{MinImageCount}，请检查棋盘规格或更换姿态重拍");

            using var cameraMatrix = new Mat(3, 3, MatType.CV_64F, Scalar.All(0));
            cameraMatrix.Set(0, 0, imageSize.Width);
            cameraMatrix.Set(1, 1, imageSize.Height);
            cameraMatrix.Set(0, 2, imageSize.Width / 2.0);
            cameraMatrix.Set(1, 2, imageSize.Height / 2.0);
            cameraMatrix.Set(2, 2, 1.0);

            using var distCoeffs = new Mat();
            var rms = Cv2.CalibrateCamera(
                objectPoints, imagePoints, imageSize, cameraMatrix, distCoeffs,
                out rvecs, out tvecs);

            // 逐图重投影 RMS：定位单张坏图（拍糊/棋盘部分出界），供剔除
            var perImageRms = new double[imagePoints.Count];
            for (var i = 0; i < imagePoints.Count; i++)
            {
                using var projected = new Mat();
                Cv2.ProjectPoints(objectPoints[i], rvecs[i], tvecs[i], cameraMatrix, distCoeffs, projected);

                // ProjectPoints 输出 CV_32FC2（Point2f），不能 At<double> 读——
                // 两个 float 拼成一个 double 数值无意义。图像点/投影点可能是 N×1 或 1×N 布局，
                // 按行/列数判断用 At<Vec2f>(i,0) 还是 At<Vec2f>(0,i)。
                var count = Math.Max(projected.Rows, projected.Cols);
                var err = 0.0;
                for (var j = 0; j < count; j++)
                {
                    var p = PointAt(projected, j);
                    var ip = PointAt(imagePoints[i], j);
                    var dx = p.Item0 - ip.Item0;
                    var dy = p.Item1 - ip.Item1;
                    err += dx * dx + dy * dy;
                }
                perImageRms[i] = Math.Sqrt(err / count);
            }

            var matrix = new double[9];
            var m = 0;
            for (var i = 0; i < 3; i++)
                for (var j = 0; j < 3; j++)
                    matrix[m++] = cameraMatrix.At<double>(i, j);

            // 无畸变结果（distCoeffs 为空）时输出空数组，避免对空 Mat At<double>(0,0) 越界
            double[] dist;
            if (distCoeffs.Empty())
            {
                dist = [];
            }
            else
            {
                dist = new double[Math.Max(distCoeffs.Cols, 1)];
                for (var i = 0; i < dist.Length; i++)
                    dist[i] = distCoeffs.At<double>(0, i);
            }

            return new IntrinsicProfile
            {
                CameraId = cameraId,
                Width = imageSize.Width,
                Height = imageSize.Height,
                CameraMatrix = matrix,
                DistCoeffs = dist,
                Rms = rms,
                ImageCount = imagePoints.Count,
                PerImageRms = perImageRms,
                CalibratedAt = DateTime.Now,
            };
        }
        finally
        {
            foreach (var mat in objectPoints.Concat(imagePoints).Concat(rvecs).Concat(tvecs))
                mat.Dispose();
        }
    }

    /// <summary>按 Mat 实际布局读取第 i 个二维点（N×1 用 (i,0)，1×N 用 (0,i)）。</summary>
    private static Vec2f PointAt(Mat m, int i) =>
        m.Rows >= m.Cols ? m.At<Vec2f>(i, 0) : m.At<Vec2f>(0, i);
}
