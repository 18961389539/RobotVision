using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 虚拟相机测试：三种图案的图像规格、帧间姿态变化（标定采集前提）、
/// 棋盘格内角点可检测性（内参标定向导可用）、曝光延时与噪声模拟、参数校验。
/// </summary>
public class VirtualCameraTests
{
    [Fact]
    public void Ctor_InvalidPattern_ThrowsWithValidOptions()
    {
        var ex = Assert.Throws<VisionException>(() => new VirtualCamera("v", pattern: "Noise"));
        Assert.Equal(VisionErrorCode.CameraInitFailed, ex.ErrorCode);
        Assert.Contains("Chessboard", ex.Message);
    }

    [Theory]
    [InlineData(0, 960)]
    [InlineData(1280, 0)]
    public void Ctor_InvalidResolution_Throws(int width, int height)
    {
        Assert.Throws<VisionException>(() => new VirtualCamera("v", width, height));
    }

    [Fact]
    public void Grab_Chessboard_DetectsExpectedInnerCorners()
    {
        // 640×480 / 40px 格 → 棋盘 8×6 格 → 内角点 7×5
        using var camera = new VirtualCamera("v", 640, 480, "Chessboard", chessCellPx: 40);

        using var frame = camera.Grab().Image;
        Assert.Equal(3, frame.Channels());
        Assert.Equal(480, frame.Rows);
        Assert.Equal(640, frame.Cols);

        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        Assert.True(Cv2.FindChessboardCornersSB(gray, new Size(7, 5), out _),
            "虚拟棋盘格应能被角点检测识别（内参标定可用）");
    }

    [Fact]
    public void Grab_Chessboard_FramesDiffer()
    {
        using var camera = new VirtualCamera("v", 640, 480, "Chessboard", chessCellPx: 40);

        using var a = camera.Grab().Image;
        using var b = camera.Grab().Image;
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        Assert.True(Cv2.Mean(diff).Val0 > 0, "连续两帧应有姿态变化");
    }

    [Fact]
    public void Grab_Shapes_NonEmptyAndFramesDiffer()
    {
        using var camera = new VirtualCamera("v", 640, 480, "Shapes");

        using var a = camera.Grab().Image;
        using var b = camera.Grab().Image;
        Assert.False(a.Empty());
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        Assert.True(Cv2.Mean(diff).Val0 > 0);
    }

    [Fact]
    public void Grab_Bars_NoNoise_IsFlatInsideStripe()
    {
        using var camera = new VirtualCamera("v", 640, 480, "Bars");

        using var frame = camera.Grab().Image;
        // 第一根条纹内部取 ROI：无噪声时标准差应为 0
        using var roi = frame[new Rect(5, 10, 20, frame.Rows - 20)];
        Cv2.MeanStdDev(roi, out _, out var std);
        Assert.True(std.Val0 < 0.5, $"纯色条带不应有噪声，实测 std={std.Val0}");
    }

    [Fact]
    public void Grab_Bars_WithNoise_StdDevGreaterThanZero()
    {
        using var camera = new VirtualCamera("v", 640, 480, "Bars", noiseSigma: 20);

        using var frame = camera.Grab().Image;
        using var roi = frame[new Rect(5, 10, 20, frame.Rows - 20)];
        Cv2.MeanStdDev(roi, out _, out var std);
        Assert.True(std.Val0 > 1, $"加噪后纯色条带应有显著波动，实测 std={std.Val0}");
    }

    [Fact]
    public void Grab_IntervalMs_BlocksAtLeastInterval()
    {
        using var camera = new VirtualCamera("v", 640, 480, "Bars", intervalMs: 80);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        camera.Grab().Dispose();
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds >= 60,
            $"IntervalMs=80 时单帧耗时应 ≥60ms，实测 {watch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Kind_IsVirtual()
    {
        using var camera = new VirtualCamera("v");
        Assert.Equal(CameraKind.Virtual, camera.Kind);
        Assert.Equal("v", camera.Id);
    }

    /// <summary>虚拟棋盘格帧序列可直接喂给内参标定器（UI 标定向导的采集来源）。</summary>
    [Fact]
    public void ChessboardFrames_SupportIntrinsicCalibration()
    {
        using var camera = new VirtualCamera("v", 640, 480, "Chessboard", chessCellPx: 40);
        var dir = Path.Combine(Path.GetTempPath(), "rv_vcam_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            for (var i = 0; i < ChessboardIntrinsicCalibrator.MinImageCount + 2; i++)
            {
                using var frame = camera.Grab().Image;
                Cv2.ImWrite(Path.Combine(dir, $"f{i:D2}.png"), frame);
            }

            var profile = ChessboardIntrinsicCalibrator.Calibrate(
                "v", Directory.GetFiles(dir, "*.png").OrderBy(f => f).ToArray(),
                new Size(7, 5), 40.0);

            Assert.True(profile.Rms < 1.0, $"无畸变虚拟相机标定 RMS 应很小，实测 {profile.Rms}");
            Assert.True(profile.CameraMatrix[0] > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>默认规格（1280×960/40px，即 appsettings 的 cam_virtual）内角点为 15×11；
    /// 向导据此自动同步棋盘参数，与物理棋盘默认 9×6 区分。</summary>
    [Fact]
    public void ChessboardInnerCorners_DefaultConfig_Is15x11()
    {
        using var camera = new VirtualCamera("v");
        Assert.Equal(new Size(15, 11), camera.ChessboardInnerCorners);
    }

    [Fact]
    public void ChessboardInnerCorners_NonChessboardPattern_IsZero()
    {
        using var camera = new VirtualCamera("v", 640, 480, "Shapes");
        Assert.Equal(new Size(0, 0), camera.ChessboardInnerCorners);
    }

    /// <summary>按 9×6（物理棋盘默认）查询默认规格虚拟棋盘（15×11）：
    /// 规格不匹配时检测不可靠——这正是向导切虚拟相机时同步内角点的原因。</summary>
    [Fact]
    public void Grab_Chessboard_PatternSizeMismatch_DetectionUnreliable()
    {
        using var camera = new VirtualCamera("v");
        var hits = 0;
        const int frames = 12;
        for (var i = 0; i < frames; i++)
        {
            using var frame = camera.Grab().Image;
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            if (Cv2.FindChessboardCornersSB(gray, new Size(9, 6), out _))
                hits++;
        }
        Assert.True(hits < frames,
            $"9×6 查询 15×11 虚拟棋盘不应 12 帧全部命中（实测 {hits}/{frames}），否则无需同步规格");
    }

    /// <summary>默认规格虚拟相机 + 同步后的内角点规格可直接完成内参标定
    /// （UI 向导选择 cam_virtual 的完整采集路径，帧序列平移提供姿态变化）。</summary>
    [Fact]
    public void ChessboardFrames_DefaultConfig_CalibrateWithSyncedPattern()
    {
        using var camera = new VirtualCamera("v");
        var dir = Path.Combine(Path.GetTempPath(), "rv_vcam_def_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            for (var i = 0; i < ChessboardIntrinsicCalibrator.MinImageCount + 2; i++)
            {
                using var frame = camera.Grab().Image;
                Cv2.ImWrite(Path.Combine(dir, $"f{i:D2}.png"), frame);
            }

            var profile = ChessboardIntrinsicCalibrator.Calibrate(
                "v", Directory.GetFiles(dir, "*.png").OrderBy(f => f).ToArray(),
                camera.ChessboardInnerCorners, 40.0);

            Assert.True(profile.Rms < 1.0, $"实测 RMS {profile.Rms}");
            Assert.Equal(1280, profile.Width);
            Assert.Equal(960, profile.Height);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
