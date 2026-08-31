using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;
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
        Assert.False(frame.Image.IsEmpty);
        Assert.True(frame.CapturedAtUtc <= DateTime.UtcNow);
        Assert.Equal(CameraKind.File, camera.Kind);
        Assert.Equal("cam", camera.Id);
    }

    [Fact]
    public void RepeatLast_DoesNotAdvancePlayback()
    {
        using var camera = new FileCamera("cam", _dir);
        using var first = camera.Grab();
        using var again = camera.RepeatLast();
        using var a = VisionImageCv.AsMat(first.Image);
        using var b = VisionImageCv.AsMat(again.Image);
        Assert.Equal(a.At<Vec3b>(0, 0)[0], b.At<Vec3b>(0, 0)[0]);

        using var next = camera.Grab();
        using var c = VisionImageCv.AsMat(next.Image);
        Assert.Equal(60, c.At<Vec3b>(0, 0)[0]);
    }

    [Fact]
    public void RepeatLast_BeforeAnyGrab_EqualsFirstGrab()
    {
        using var camera = new FileCamera("cam", _dir);
        using var frame = camera.RepeatLast();
        using var mat = VisionImageCv.AsMat(frame.Image);
        Assert.Equal(0, mat.At<Vec3b>(0, 0)[0]);
    }

    [Fact]
    public void PlaybackFiles_ListsAllImagesInNameOrder_WithoutAdvancingGrab()
    {
        using var camera = new FileCamera("cam", _dir);
        Assert.Equal(3, camera.PlaybackFiles.Count);
        Assert.Equal("f0.png", Path.GetFileName(camera.PlaybackFiles[0]));
        Assert.Equal("f1.png", Path.GetFileName(camera.PlaybackFiles[1]));
        Assert.Equal("f2.png", Path.GetFileName(camera.PlaybackFiles[2]));

        using var decoded = FileCamera.DecodeFile(camera.PlaybackFiles[1]);
        Assert.False(decoded.Empty());
        Assert.Equal(60, decoded.At<Vec3b>(0, 0)[0]);

        using var frame = camera.Grab();
        using var grabMat = VisionImageCv.AsMat(frame.Image);
        Assert.Equal(0, grabMat.At<Vec3b>(0, 0)[0]);
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
            Assert.False(frame.Image.IsEmpty);
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
