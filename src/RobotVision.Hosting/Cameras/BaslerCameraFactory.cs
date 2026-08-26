using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.Hosting.Cameras;

/// <summary>
/// Basler 相机工厂：pylon .NET 真实相机。
/// 懒连接语义——构造只校验 pylon 运行库，打开设备推迟到首次取图；
/// 创建时顺带做一次连接诊断（失败告警并提示首次取图时自动重试），
/// 启动时相机未上电/网络未通不阻断服务。采集用 GrabOne，连接阶段不 Start 连续采集。
/// </summary>
public sealed class BaslerCameraFactory : ICameraFactory, IDeviceEnumerableFactory
{
    public string TypeName => "Basler";

    public ICamera Create(CameraConfig config, ILogger? logger = null)
    {
        // 不在注册阶段 TryConnectOnce：GigE/Basler 连接可能阻塞数秒～十几秒，
        // 会在 WPF 显示主窗口之前卡住 UI；首次 Grab 时 BaslerCamera 会自动连接。
        return new BaslerCamera(
            config.Id, config.DeviceId, config.ExposureTimeUs, config.Gain, config.GrabTimeoutMs, logger);
    }

    public IReadOnlyList<string> EnumerateDevices() => BaslerCamera.EnumerateDevices();
}
