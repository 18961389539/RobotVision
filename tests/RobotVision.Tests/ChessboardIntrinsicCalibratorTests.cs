using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 棋盘格内参标定（ChessboardIntrinsicCalibrator）错误路径测试。
/// 成功路径（≥10 张棋盘图标定收敛、RMS 小）已由 VirtualCameraTests 端到端覆盖，
/// 本类聚焦防护逻辑：
/// - 图像数不足、棋盘规格非法、单元尺寸非法（0/负/NaN/Infinity）；
/// - 混合分辨率批次拒绝（imageSize 全局一致是标定正确性前提）；
/// - 失败分类：不可读文件（路径/权限）与未检出棋盘给出不同引导信息。
/// </summary>
public class ChessboardIntrinsicCalibratorTests
{
    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), "rv_chess_" + Guid.NewGuid().ToString("N"));

    /// <summary>用虚拟棋盘相机写 N 张棋盘帧到目录（帧间自带姿态变化，均可被角点检测命中）。</summary>
    private static List<string> WriteChessboardFrames(string dir, string prefix, int count, int width, int height)
    {
        using var camera = new VirtualCamera(prefix, width, height, "Chessboard", chessCellPx: 40);
        var files = new List<string>();
        for (var i = 0; i < count; i++)
        {
            using var grabbed = camera.Grab();
            using var mat = VisionImageCv.AsMat(grabbed.Image);
            var file = Path.Combine(dir, $"{prefix}_{i:D2}.png");
            Cv2.ImWrite(file, mat);
            files.Add(file);
        }
        return files;
    }

    [Fact]
    public void Calibrate_FewerThanMinImages_Throws()
    {
        var files = Enumerable.Range(0, ChessboardIntrinsicCalibrator.MinImageCount - 1)
            .Select(i => Path.Combine(Path.GetTempPath(), $"ghost_{i}.png")).ToArray();

        var ex = Assert.Throws<VisionException>(() =>
            ChessboardIntrinsicCalibrator.Calibrate("C1", files, new Size(7, 5), 40.0));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains($"{files.Length}/{ChessboardIntrinsicCalibrator.MinImageCount}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Calibrate_InvalidPatternSize_Throws()
    {
        var files = Enumerable.Range(0, ChessboardIntrinsicCalibrator.MinImageCount + 2)
            .Select(i => Path.Combine(Path.GetTempPath(), $"ghost_{i}.png")).ToArray();

        var ex = Assert.Throws<VisionException>(() =>
            ChessboardIntrinsicCalibrator.Calibrate("C1", files, new Size(0, 5), 40.0));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("规格非法", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Calibrate_InvalidSquareSize_Throws(double squareSizeMm)
    {
        var files = Enumerable.Range(0, ChessboardIntrinsicCalibrator.MinImageCount + 2)
            .Select(i => Path.Combine(Path.GetTempPath(), $"ghost_{i}.png")).ToArray();

        var ex = Assert.Throws<VisionException>(() =>
            ChessboardIntrinsicCalibrator.Calibrate("C1", files, new Size(7, 5), squareSizeMm));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("单元尺寸非法", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Calibrate_MixedResolution_Throws()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            // 5 张 640×480 + 5 张 800×600：批次分辨率不一致必须整体拒绝（否则 imageSize 错位）
            var files = WriteChessboardFrames(dir, "a", 5, 640, 480);
            files.AddRange(WriteChessboardFrames(dir, "b", 5, 800, 600));

            var ex = Assert.Throws<VisionException>(() =>
                ChessboardIntrinsicCalibrator.Calibrate("C1", files, new Size(7, 5), 40.0));
            Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
            Assert.Contains("分辨率", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Calibrate_UnreadableFiles_ReportsReadFailure()
    {
        // 12 个不存在的路径：全部落入"无法读取"分类，消息应引导检查路径/格式/权限
        var files = Enumerable.Range(0, ChessboardIntrinsicCalibrator.MinImageCount + 2)
            .Select(i => Path.Combine(Path.GetTempPath(), "no_such_dir", $"ghost_{i}.png")).ToArray();

        var ex = Assert.Throws<VisionException>(() =>
            ChessboardIntrinsicCalibrator.Calibrate("C1", files, new Size(7, 5), 40.0));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("无法读取", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Calibrate_NoChessboardDetected_ReportsDetectionFailure()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            // 12 张纯色图：文件可读但检不出棋盘，消息应引导检查棋盘规格/姿态
            var files = new List<string>();
            for (var i = 0; i < ChessboardIntrinsicCalibrator.MinImageCount + 2; i++)
            {
                using var white = new Mat(640, 480, MatType.CV_8UC3, Scalar.All(255));
                var file = Path.Combine(dir, $"plain_{i:D2}.png");
                Cv2.ImWrite(file, white);
                files.Add(file);
            }

            var ex = Assert.Throws<VisionException>(() =>
                ChessboardIntrinsicCalibrator.Calibrate("C1", files, new Size(7, 5), 40.0));
            Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
            Assert.Contains("未检出棋盘", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
