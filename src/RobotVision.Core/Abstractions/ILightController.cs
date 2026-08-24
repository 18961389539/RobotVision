using RobotVision.Core.Models;

namespace RobotVision.Core.Abstractions;

/// <summary>光源控制器类型。</summary>
public enum LightControllerKind
{
    /// <summary>无操作虚拟控制器（未接硬件时的调试兜底，同 FileCamera 定位）。</summary>
    None,

    /// <summary>串口光源控制器（RS232/RS485，如奥普特/康耐视控制器）。</summary>
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
    /// 实现必须容忍空配置/全禁用通道（静默无操作），不得抛异常。
    /// </summary>
    void Apply(LightingConfig lighting);

    /// <summary>熄灭全部通道。</summary>
    void TurnOff();

    /// <summary>
    /// 发送原始指令（协议调试用：UI 手动输入指令文本，便于联调控制器协议）。
    /// 不支持的控制器静默无操作（如 Noop）；Network 实现支持 \r \n \t 转义解析。
    /// </summary>
    void SendRaw(string command);
}
