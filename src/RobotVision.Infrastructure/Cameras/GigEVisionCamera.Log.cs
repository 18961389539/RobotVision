using Microsoft.Extensions.Logging;

namespace RobotVision.Infrastructure.Cameras;

internal static partial class GigEVisionCameraLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} 采集失败（{Message}），尝试自动重连")]
    public static partial void GrabFailedRetry(ILogger logger, string id, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} 采集异常，尝试自动重连")]
    public static partial void GrabExceptionRetry(ILogger logger, Exception ex, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "GigE Vision 相机 {Id} 已连接: SN={Sn} IP={Ip} Name={Name}")]
    public static partial void Connected(ILogger logger, string id, string sn, string ip, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} 连接失败")]
    public static partial void ConnectFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} {Node}={Value} 超出 [{Min}, {Max}]")]
    public static partial void ParameterOutOfRange(ILogger logger, string id, string node, long value, long min, long max);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} 写入 {Node} 失败")]
    public static partial void WriteFailed(ILogger logger, Exception ex, string id, string node);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GigE Vision 相机 {Id} 枚举 {Node}={Value} 跳过")]
    public static partial void EnumSkipped(ILogger logger, Exception ex, string id, string node, string value);
}
