namespace RobotVision.Infrastructure.Cameras;

/// <summary>硬件端已下发 2×2 全图降采样时，管理器不再做软件再减半。</summary>
internal interface IHardware2x2Output
{
    bool HasHardware2x2 { get; }

    int ExpectedWidth { get; }

    int ExpectedHeight { get; }
}
