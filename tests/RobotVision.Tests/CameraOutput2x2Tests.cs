using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

public class CameraOutput2x2Tests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rv_2x2_" + Guid.NewGuid().ToString("N"));

    public CameraOutput2x2Tests()
    {
        Directory.CreateDirectory(_dir);
        using var img = new Mat(64, 80, MatType.CV_8UC3, Scalar.All(90));
        Cv2.ImWrite(Path.Combine(_dir, "a.png"), img);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void Half_HalvesWidthAndHeight()
    {
        using var source = new CameraFrame(VisionImage.AllocateZero(80, 64, 3), DateTime.UtcNow);
        using var half = CameraOutput2x2.Half(source);
        Assert.Equal(40, half.Image.Width);
        Assert.Equal(32, half.Image.Height);
    }

    [Fact]
    public void ManagerGrab_FileCamera_KeepsOriginalResolution()
    {
        using var camera = new FileCamera("replay", _dir);
        using var manager = new CameraManager();
        manager.Register(camera);

        using var frame = manager.Grab("replay");
        Assert.Equal(80, frame.Image.Width);
        Assert.Equal(64, frame.Image.Height);
    }

    [Fact]
    public void ManagerGrab_FakeVirtualKind_DoesNotResample()
    {
        using var manager = new CameraManager();
        var camera = new StubCamera();
        manager.Register(camera);

        using var frame = manager.Grab("stub");
        Assert.Equal(8, frame.Image.Width);
        Assert.Equal(8, frame.Image.Height);
    }

    private sealed class StubCamera : ICamera
    {
        public string Id => "stub";
        public CameraKind Kind => CameraKind.Virtual;
        public CameraFrame Grab(CancellationToken ct = default) =>
            new(VisionImage.AllocateZero(8, 8, 3), DateTime.UtcNow);
        public void Dispose() { }
    }
}
