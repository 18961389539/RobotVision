using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 回放相机测试：帧时间戳、帧间隔限速（联调节拍可控）、取消令牌响应、初始化失败错误码。
/// </summary>
public class FileCameraTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rv_filecam_" + Guid.NewGuid().ToString("N"));

    public FileCameraTests()
    {
        Directory.CreateDirectory(_dir);
        for (var i = 0; i < 3; i++)
            Cv2.ImWrite(Path.Combine(_dir, $"f{i}.png"), new Mat(64, 64, MatType.CV_8UC3, Scalar.All(i * 60)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void Grab_ReturnsFrameWithImageAndTimestamp()
    {
        using var camera = new FileCamera("cam", _dir);
        using var frame = camera.Grab();
        Assert.False(frame.Image.Empty());
        Assert.True(frame.CapturedAtUtc <= DateTime.UtcNow);
        Assert.Equal(CameraKind.File, camera.Kind);
        Assert.Equal("cam", camera.Id);
    }

    [Fact]
    public void Grab_IntervalMs_BlocksAtLeastInterval()
    {
        using var camera = new FileCamera("cam", _dir, intervalMs: 80);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        camera.Grab().Dispose();
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds >= 60,
            $"IntervalMs=80 时单帧耗时应 ≥60ms，实测 {watch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Grab_CancelledDuringInterval_Throws()
    {
        using var camera = new FileCamera("cam", _dir, intervalMs: 5000);
        using var cts = new CancellationTokenSource(10);
        Assert.Throws<OperationCanceledException>(() => camera.Grab(cts.Token));
    }

    [Fact]
    public void Grab_ChinesePath_ReadsImage()
    {
        var cnDir = Path.Combine(Path.GetTempPath(), "回放测试_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cnDir);
        try
        {
            using var src = new Mat(32, 32, MatType.CV_8UC3, Scalar.All(80));
            Cv2.ImEncode(".png", src, out var bytes);
            File.WriteAllBytes(Path.Combine(cnDir, "中文.png"), bytes);

            using var camera = new FileCamera("cam", cnDir);
            using var frame = camera.Grab();
            Assert.False(frame.Image.Empty());
            Assert.Equal(32, frame.Image.Width);
        }
        finally
        {
            try { Directory.Delete(cnDir, true); }
            catch (IOException) { }
        }
    }

    [Fact]
    public void Ctor_MissingFolder_ThrowsInitFailed()
    {
        var ex = Assert.Throws<VisionException>(() => new FileCamera("cam", Path.Combine(_dir, "nope")));
        Assert.Equal(VisionErrorCode.CameraInitFailed, ex.ErrorCode);
    }

    [Fact]
    public void Ctor_NoImages_ThrowsInitFailed()
    {
        var empty = Path.Combine(Path.GetTempPath(), "rv_filecam_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var ex = Assert.Throws<VisionException>(() => new FileCamera("cam", empty));
            Assert.Equal(VisionErrorCode.CameraInitFailed, ex.ErrorCode);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }
}
