using Microsoft.Extensions.Logging;

namespace RobotVision.Infrastructure.Inference;

internal static partial class YoloDotNetEngineFactoryLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "OpenVINO GPU 不可用（{Reason}），模型 {Model} 已回退 OpenVINO CPU；后续模型将跳过 GPU")]
    public static partial void GpuFallback(ILogger logger, Exception ex, string reason, string model);
}
