using Microsoft.Extensions.Logging;

namespace RobotVision.Infrastructure.Cameras;

internal static partial class GigEForceIpLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "相机 SN={Sn} MAC={Mac} 原 IP {Old} 与网卡不同网段，已临时 FORCEIP 为 {New}/{Mask}。请在 pylon Viewer 写成永久静态 IP，避免下次再分配。")]
    public static partial void ForceIpApplied(
        ILogger logger, string sn, string mac, string old, string @new, string mask);
}
