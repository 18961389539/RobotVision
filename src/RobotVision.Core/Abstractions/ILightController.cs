using RobotVision.Core.Models;

namespace RobotVision.Core.Abstractions;

/// <summary>光源控制器类型。</summary>
public enum LightControllerKind
{
    /// <summary>无操作虚拟控制器（未接硬件时的调试兜底，同 FileCamera 定位）。</summary>
    None,

    /// <summary>串口光源控制器（RS232，东冠 OSE PWD 数字电源）。</summary>
    Serial,

    /// <summary>Modbus 光源控制器。</summary>
    Modbus,

    /// <summary>UDP 网络光源控制器（频闪控制器，参照 VPDLFramework ECLightControl 传输层）。</summary>
    Udp,

    /// <summary>TCP 光源控制器。</summary>
    Tcp,

    /// <summary>其他/带状态记录的虚拟控制器（测试用）。</summary>
    Virtual,
}

/// <summary>
/// 光源控制器抽象：真实光源控制器（串口/Modbus/TCP）与无操作实现实现同一接口，
/// 上层流程无需区分调试与生产——与 ICamera 的设计同构。
/// </summary>
public interface ILightController : IDisposable
{
    string Id { get; }

    LightControllerKind Kind { get; }

    /// <summary>
    /// 按照明配置点亮光源（幂等）：逐通道设置亮度并点亮。
    /// 空配置/空通道视为成功（无操作）。发送失败必须返回 false，不得吞掉后当作已点亮。
    /// 初始化失败的占位实现（FailedLight）仍抛 1006，与「未注册」同处置。
    /// </summary>
    /// <returns>true 指令已发出或无需发出（空通道/Noop）；false 硬件发送失败。</returns>
    bool Apply(LightingConfig lighting);

    /// <summary>
    /// 熄灭全部通道。
    /// 与 <see cref="Apply"/> 对称：**发送失败必须返回 false，不得吞掉后当作已熄灯**。
    /// 否则串口没打开/协议不对时 UI 会谎报「已熄灯」，而「开灯」如实报错，
    /// 表现为「能关灯、不能开灯」这种无从下手、方向被带偏的现象（2026-09-14 实际踩过）。
    /// 初始化失败的占位实现（FailedLight）仍抛 1006。
    /// </summary>
    /// <returns>true 指令已发出或无需发出（Noop）；false 硬件发送失败。</returns>
    bool TurnOff();

    /// <summary>
    /// 发送原始指令（协议调试用：UI 手动输入指令文本，便于联调控制器协议）。
    /// 不支持的控制器静默无操作（如 Noop）；Serial 支持十六进制帧，Network/Serial 均支持 \r \n \t 转义。
    /// <para>
    /// 与 <see cref="Apply"/>/<see cref="TurnOff"/> 对称：**发送失败必须返回 false**。
    /// 协议调试框是排障时最不能骗人的一步——它若谎报「已发送」，会把方向带到协议/硬件上去
    /// （2026-09-14 排查光源时实际踩过）。
    /// </para>
    /// </summary>
    /// <returns>true 指令已发出或无需发出（Noop/空指令）；false 硬件发送失败。</returns>
    bool SendRaw(string command);
}
