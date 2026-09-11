using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

public sealed class CameraExposureBeforeGrabTests
{
    private sealed class RecordingExposureCamera(string id) : ICamera, IExposureControl
    {
        public string Id { get; } = id;

        public CameraKind Kind => CameraKind.Real;

        public double? LastExposureUs { get; private set; }

        public double? LastGain { get; private set; }

        public CameraFrame Grab(CancellationToken ct = default) =>
            new(VisionImage.AllocateZero(4, 4, 3), DateTime.UtcNow);

        public void Dispose() { }

        public bool TrySetExposureTimeUs(double value)
        {
            LastExposureUs = value;
            return true;
        }

        public bool TrySetGain(double value)
        {
            LastGain = value;
            return true;
        }

        public double? GetExposureTimeUs() => LastExposureUs;

        public double? GetGain() => LastGain;

        public (double Min, double Max)? GetExposureRange() => null;

        public (double Min, double Max)? GetGainRange() => null;
    }

    [Fact]
    public void Apply_SetsExposureAndZeroGain_FromMatchingCameraConfig()
    {
        var cfg = new AppConfig
        {
            Cameras =
            [
                new CameraConfig { Id = "Product", Type = "Basler", ExposureTimeUs = 152, Gain = 0 },
            ],
        };
        var camera = new RecordingExposureCamera("Product");

        CameraExposureBeforeGrab.Apply(cfg, camera);

        Assert.Equal(152, camera.LastExposureUs);
        Assert.Equal(0, camera.LastGain);
    }

    [Fact]
    public void Apply_SkipsWhenNoConfigOrVirtualCamera()
    {
        var cfg = new AppConfig();
        var camera = new RecordingExposureCamera("Product");

        CameraExposureBeforeGrab.Apply(cfg, camera);

        Assert.Null(camera.LastExposureUs);
        Assert.Null(camera.LastGain);
    }

    [Fact]
    public void Apply_IgnoresNullExposureAndNullGain()
    {
        var cfg = new AppConfig
        {
            Cameras = [new CameraConfig { Id = "cam", ExposureTimeUs = null, Gain = null }],
        };
        var camera = new RecordingExposureCamera("cam");

        CameraExposureBeforeGrab.Apply(cfg, camera);

        Assert.Null(camera.LastExposureUs);
        Assert.Null(camera.LastGain);
    }
}
