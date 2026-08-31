using System.Net;
using Microsoft.Extensions.Logging;

namespace RobotVision.Infrastructure.Communication;

internal static partial class TcpServerManagerLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "TCP 监听已热重启: {Old} → {New}")]
    public static partial void HotRestartSucceeded(ILogger logger, string old, string @new);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "监听热重启失败，回滚到 {Old}")]
    public static partial void HotRestartFailed(ILogger logger, Exception ex, string old);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "回滚监听也失败，服务保持停止状态")]
    public static partial void HotRestartRollbackFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "TCP 服务已启动: {Address}:{Port}")]
    public static partial void ServiceStarted(ILogger logger, IPAddress address, int port);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "连接被拒绝（IP 不在白名单）: {Remote}")]
    public static partial void ConnectionRejectedByWhitelist(ILogger logger, string remote);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "连接被拒绝（达到连接上限 {Limit}）: {Remote}")]
    public static partial void ConnectionRejectedByLimit(ILogger logger, int limit, string remote);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "客户端 #{Id} 接入: {Remote}，当前连接数 {Count}")]
    public static partial void ClientConnected(ILogger logger, long id, string remote, int count);

    [LoggerMessage(EventId = 8, Level = LogLevel.Critical, Message = "TCP 监听循环异常退出，尝试重启监听")]
    public static partial void AcceptLoopCrashed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 9, Level = LogLevel.Critical, Message = "TCP 监听重启失败")]
    public static partial void AcceptLoopRestartFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "客户端 #{Id} 读空闲超时（{Timeout}ms 无数据），断开")]
    public static partial void ClientReadIdleTimeout(ILogger logger, long id, long timeout);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "客户端 #{Id} 收到: {Line}")]
    public static partial void ClientLineReceived(ILogger logger, long id, string line);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "PLC 调试模式：{Original} → {Coerced}（请求: {Line}）")]
    public static partial void PlcDebugModeCoercedReply(ILogger logger, string original, string coerced, string line);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "客户端 #{Id} 写应答超时/取消，断开")]
    public static partial void ClientWriteTimeout(ILogger logger, long id);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "客户端 #{Id} 被手动断开")]
    public static partial void ClientManuallyDisconnected(ILogger logger, long id);

    [LoggerMessage(EventId = 15, Level = LogLevel.Warning, Message = "客户端 #{Id} 连接异常")]
    public static partial void ClientConnectionFault(ILogger logger, Exception ex, long id);

    [LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "客户端 #{Id} 断开，当前连接数 {Count}")]
    public static partial void ClientDisconnected(ILogger logger, long id, int count);

    [LoggerMessage(EventId = 17, Level = LogLevel.Warning, Message = "内部错误应答（详情仅日志）: 配方 {Recipe} · {Message}")]
    public static partial void InternalErrorReply(ILogger logger, string recipe, string message);

    [LoggerMessage(EventId = 18, Level = LogLevel.Error, Message = "处理请求异常: {Line}")]
    public static partial void RequestProcessingFailed(ILogger logger, Exception ex, string line);
}
