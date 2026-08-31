using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class AssetIntegrityCheckerLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "读取模型清单失败")]
    public static partial void ManifestReadFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "配方 {Recipe} 未钉扎模型 {Model} 的 SHA-256，同名替换不会被 1017 拦住")]
    public static partial void ModelSha256NotPinned(ILogger logger, string recipe, string model);
}
