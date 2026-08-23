using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.Hosting.Cameras;

/// <summary>File 相机工厂：文件夹回放（无相机联调/算法回归）。</summary>
public sealed class FileCameraFactory : ICameraFactory
{
    public string TypeName => "File";

    public ICamera Create(CameraConfig config, ILogger? logger = null) =>
        new FileCamera(config.Id, config.ResolveCameraFolder(), intervalMs: config.IntervalMs);
}
