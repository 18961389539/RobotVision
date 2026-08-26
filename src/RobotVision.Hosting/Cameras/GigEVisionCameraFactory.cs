using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.Hosting.Cameras;

/// <summary>
/// 开源 GigE Vision 工厂（GigEVision.Net）。不依赖 pylon；
/// 构造不接触设备，创建后做一次连接诊断，失败不阻断注册。
/// </summary>
public sealed class GigEVisionCameraFactory : ICameraFactory, IDeviceEnumerableFactory
{
    public string TypeName => "GigEVision";

    public ICamera Create(CameraConfig config, ILogger? logger = null)
    {
        // 不在注册阶段 TryConnectOnce（GigE 发现+握手可阻塞 UI 启动）；首次 Grab 时自动连接。
        return new GigEVisionCamera(
            config.Id, config.DeviceId, config.ExposureTimeUs, config.Gain, config.GrabTimeoutMs, logger);
    }

    public IReadOnlyList<string> EnumerateDevices() => GigEVisionCamera.EnumerateDevices();
}
