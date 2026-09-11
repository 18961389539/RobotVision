using RobotVision.Core.Abstractions;

namespace RobotVision.Hosting;

/// <summary>
/// 取图前按 appsettings 当前值下发曝光/增益。
/// 仅对实现 <see cref="IExposureControl"/> 的真实相机生效；File/Virtual 无操作。
/// </summary>
public static class CameraExposureBeforeGrab
{
    public static void Apply(AppConfig cfg, ICamera camera)
    {
        if (cfg is null || camera is not IExposureControl exposure)
            return;

        var camCfg = cfg.Cameras.FirstOrDefault(c =>
            string.Equals(c.Id, camera.Id, StringComparison.OrdinalIgnoreCase));
        if (camCfg is null)
            return;

        // 与 Basler/GigE 连接时规则一致：曝光须 >0；增益 0 是合法值（关闭增益）。
        if (camCfg.ExposureTimeUs is > 0)
            exposure.TrySetExposureTimeUs(camCfg.ExposureTimeUs.Value);
        if (camCfg.Gain is >= 0)
            exposure.TrySetGain(camCfg.Gain.Value);
    }
}
