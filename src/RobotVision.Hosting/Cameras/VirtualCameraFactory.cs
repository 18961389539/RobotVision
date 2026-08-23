using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.Hosting.Cameras;

/// <summary>Virtual 相机工厂：程序生成测试图像（棋盘格/图形/色条），产线调试与算法回归。</summary>
public sealed class VirtualCameraFactory : ICameraFactory
{
    public string TypeName => "Virtual";

    public ICamera Create(CameraConfig config, ILogger? logger = null) =>
        new VirtualCamera(
            config.Id, config.Width, config.Height, config.Pattern,
            config.IntervalMs, config.NoiseSigma, config.ChessCellPx);
}
