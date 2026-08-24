using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Lighting;
using Xunit;

namespace RobotVision.Tests;

public sealed class FailedCameraTests
{
    [Fact]
    public void Grab_ThrowsCameraInitFailed_WithCtorMessage()
    {
        using var camera = new FailedCamera("cam_basler", CameraKind.Real, "pylon 运行库缺失");
        var ex = Assert.Throws<VisionException>(() => camera.Grab());
        Assert.Equal(VisionErrorCode.CameraInitFailed, ex.ErrorCode);
        Assert.Contains("pylon", ex.Message, StringComparison.Ordinal);
        Assert.Equal("cam_basler", camera.Id);
        Assert.Equal(CameraKind.Real, camera.Kind);
    }

    [Fact]
    public void CameraManager_RegisteredFailedCamera_GrabReturns1011_Not1002()
    {
        using var manager = new CameraManager();
        manager.Register(new FailedCamera("cam_basler", CameraKind.Real, "初始化失败"));

        Assert.True(manager.IsRegistered("cam_basler"));
        var ex = Assert.Throws<VisionException>(() => manager.Grab("cam_basler"));
        Assert.Equal(VisionErrorCode.CameraInitFailed, ex.ErrorCode);
        Assert.NotEqual(VisionErrorCode.CameraNotRegistered, ex.ErrorCode);
    }

    [Fact]
    public void FailedLight_Apply_ThrowsLightNotRegistered()
    {
        using var light = new FailedLightController("light_net", "COM 口被占用");
        var manager = new LightingManager();
        manager.Register(light);

        Assert.True(manager.IsRegistered("light_net"));
        var ex = Assert.Throws<VisionException>(() =>
            manager.Apply("light_net", new LightingConfig
            {
                Channels = [new LightingChannelConfig { Channel = 1, Brightness = 128 }],
            }));
        Assert.Equal(VisionErrorCode.LightNotRegistered, ex.ErrorCode);
        Assert.Contains("COM", ex.Message, StringComparison.Ordinal);
    }
}
