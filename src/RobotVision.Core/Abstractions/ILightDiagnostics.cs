namespace RobotVision.Core.Abstractions;

/// <summary>
/// 可选诊断接口：控制器把「最近一次传输为什么失败」暴露出来，供上层拼进错误消息。
/// <para>
/// 刻意与 <see cref="ILightController"/> 分开——这是可选能力，Noop 与测试假件无需实现，
/// 加到主接口上会强迫所有实现都写一个用不上的成员。
/// </para>
/// <para>
/// 起因：串口光源在串口未打开/被占用时只会报「指令发送失败」，现场无法判断是没插线、
/// 被别的程序占用，还是协议不对，只能靠猜（2026-09-14 一次排查里反复踩）。
/// </para>
/// </summary>
public interface ILightDiagnostics
{
    /// <summary>
    /// 最近一次发送失败的原因（含异常类型与消息）；发送成功后清空。从未失败为 null。
    /// </summary>
    string? LastTransportError { get; }
}
