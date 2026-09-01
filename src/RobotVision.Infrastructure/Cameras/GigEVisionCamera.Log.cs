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

    // 断连清理阶段：异常一律不阻断断连流程，但必须留痕。
    // 现场相机断连是高发故障，此前全部静默吞掉，排障时无任何线索。

    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} 断连时停止采集失败（已继续断连）")]
    public static partial void DisconnectStopAcquisitionFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} 断连时停止/释放 GVSP 流失败（已继续断连）")]
    public static partial void DisconnectStreamCleanupFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GigE Vision 相机 {Id} 断连时释放会话失败（已继续断连）")]
    public static partial void DisconnectSessionDisposeFailed(ILogger logger, Exception ex, string id);
}
