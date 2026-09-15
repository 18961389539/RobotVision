using Microsoft.Extensions.Logging;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 光源管理器日志：只覆盖两个"否则完全静默"的路径。
/// 正常收发不在这里打日志——`LightingManager` 平时是无 logger 也能工作的（测试大量直接 new）。
/// </summary>
internal static partial class LightingManagerLog
{
    /// <summary>
    /// 取图后的**自动**熄灯被控制器拒绝（返回 false）。
    /// 有意不中断取图收尾，但必须留痕：否则灯一直亮着而无人知道为什么，
    /// 而"灯怎么一直亮着"正是这个模块最容易被误判成硬件问题的现象。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto turn-off after grab was rejected by lighting {ControllerId}{Cause}")]
    public static partial void AutoTurnOffRejected(ILogger logger, string controllerId, string cause);

    /// <summary>取图后的自动熄灯抛异常（尽力而为，不向上抛，但要留下堆栈）。</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto turn-off after grab threw for lighting {ControllerId}")]
    public static partial void AutoTurnOffThrew(ILogger logger, Exception ex, string controllerId);

    /// <summary>关闭时门闩排空超时：仍有在途的 Apply/TurnOff 没结束，放弃等待继续关闭，避免进程退不掉。</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Lighting gate drain timed out for {ControllerId} during shutdown")]
    public static partial void GateDrainTimedOut(ILogger logger, string controllerId);

    /// <summary>关闭路径上另一线程恰好完成 Dispose 的竞态（预期，忽略即可）。</summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Lighting gate release raced with dispose during shutdown (expected, ignored)")]
    public static partial void GateReleaseRacedWithDispose(ILogger logger);
}
