using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.Hosting.Cameras;

/// <summary>
/// Basler 相机工厂：pylon .NET 真实相机。
/// 懒连接语义——构造只校验 pylon 运行库，Open/Start 推迟到首次取图；
/// 创建时顺带做一次连接诊断（成功打 SN/名称日志，失败告警并提示首次取图时自动重试），
/// 启动时相机未上电/网络未通不阻断服务。
/// </summary>
public sealed class BaslerCameraFactory : ICameraFactory, IDeviceEnumerableFactory
{
    public string TypeName => "Basler";

    public ICamera Create(CameraConfig config, ILogger? logger = null)
    {
        var camera = new BaslerCamera(
            config.Id, config.DeviceId, config.ExposureTimeUs, config.Gain, config.GrabTimeoutMs, logger);

        // 注册前诊断连接：成功记录相机身份，失败仅告警（不阻断注册，首次取图自动重连）
        if (logger is not null)
        {
            if (camera.TryConnectOnce())
                logger.LogInformation("Basler 相机 {Id} 已连接: SN={Sn} Name={Name}",
                    config.Id, camera.SerialNumber, camera.FriendlyName);
            else
                logger.LogWarning("Basler 相机 {Id} 当前未连接（首次取图时自动重试）", config.Id);
        }

        return camera;
    }

    public IReadOnlyList<string> EnumerateDevices() => BaslerCamera.EnumerateDevices();
}
