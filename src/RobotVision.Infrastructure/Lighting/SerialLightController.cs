using System.IO.Ports;
using System.Text;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 串口光源控制器：东冠（OSE PWD）多通道数字电源，RS232 9600 8N1。
/// 发送串行化、懒打开串口（未接硬件时注册成功、发送失败经返回值/诊断上报）。
/// 协议调试支持十六进制帧（如 <c>00 12 64 FF FF</c>）或 ASCII。
/// <para>
/// ★ <b>帧间隔是本类的关键参数</b>：间隔不足时控制器会**静默吃掉整帧**（不报错、不 NAK），
/// 表现为"UI 说开灯成功，灯却不亮""通道 2 关不掉"。阈值与实测数据见 <see cref="FrameGapMs"/>。
/// 2026-09-14 的"能关灯、不能开灯"就是它叠加协议频闪字节写反共同造成的。
/// </para>
/// <para>
/// 已排除（别重复排查）：后置 <c>DiscardInBuffer()</c>（purge）无关；<c>DtrEnable/RtsEnable</c> 无关；
/// <c>Open()</c> 后静默期无关；与 .NET 版本无关（同一进程内 .NET 10 原始帧只要间隔够就 3/3 通过）。
/// </para>
/// </summary>
public sealed class SerialLightController : ILightController, ILightDiagnostics
{
    /// <summary>
    /// 相邻两帧之间的最小间隔（ms）。
    /// <para>
    /// ★ 这个值必须够大，否则帧会被控制器**静默吃掉**——这是「开灯没效果 / 通道 2 关不掉」的真正根因。
    /// 2026-09-14 在本机 + Prolific USB 转串口 + 东冠 PWD 上实测（同一条 4 帧序列，各 3 次）：
    /// <c>10ms → 0/3</c>、<c>20ms → 0/3</c>（原值，正好卡在失败侧）、<c>30ms → 3/3</c>、
    /// <c>40/50/60/80ms → 各 3/3</c>。阈值在 20~30ms 之间。
    /// </para>
    /// <para>
    /// 取 50ms：约为实测阈值的 2 倍余量（不同适配器/线材/温度下阈值会漂），
    /// 代价可接受（「关灯」2 通道 ≈ 50ms、「开灯」≈ 50ms，取图链路本就有稳定延时）。
    /// </para>
    /// <para>
    /// 注意：手工联调脚本里写 <c>Start-Sleep -Milliseconds 20</c> 时，PowerShell 自身的
    /// cmdlet 开销会把真实间隔拉到 40~60ms，所以脚本"20ms 也能过"是假象，别据此把本值调回 20。
    /// </para>
    /// </summary>
    internal const int FrameGapMs = 50;

    /// <summary>供回归测试读取的帧间隔下限护栏（见 <see cref="FrameGapMs"/> 的实测数据）。</summary>
    internal const int FrameGapMsMinimum = 30;

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _timeoutMs;
    private readonly int _channelCount;
    private readonly object _sendLock = new();

    private SerialPort? _port;
    private bool _disposed;

    /// <summary>上一帧写完的时刻（<see cref="Environment.TickCount64"/>）；0 = 还没发过。用于跨命令的帧间隔。</summary>
    private long _lastWriteTicks;

    public string Id { get; }

    public LightControllerKind Kind => LightControllerKind.Serial;

    /// <summary>最近一次发送失败的原因（串口未打开/被占用等），成功率后清空。见 <see cref="ILightDiagnostics"/>。</summary>
    public string? LastTransportError { get; private set; }

    public SerialLightController(
        string id,
        string portName,
        int baudRate,
        int timeoutMs = 200,
        int channelCount = DongguanLightProtocol.DefaultChannelCount)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("光源控制器 Id 不能为空", nameof(id));
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("串口名不能为空", nameof(portName));
        if (baudRate is < 1200 or > 921600)
            throw new ArgumentOutOfRangeException(nameof(baudRate), "波特率必须在 1200~921600");

        Id = id;
        _portName = portName;
        _baudRate = baudRate;
        _timeoutMs = Math.Max(1, timeoutMs);
        _channelCount = Math.Clamp(channelCount, DongguanLightProtocol.MinChannel, DongguanLightProtocol.MaxChannel);
    }

    /// <summary>按照明配置点亮光源（幂等）：切到常亮后按通道下发 0–100% 亮度。发送失败返回 false。</summary>
    public bool Apply(LightingConfig lighting)
    {
        if (lighting?.Channels is null || lighting.Channels.Count == 0)
            return true;

        var ok = true;
        foreach (var channel in lighting.Channels)
        {
            var ch = Math.Clamp(channel.Channel, DongguanLightProtocol.MinChannel, DongguanLightProtocol.MaxChannel);
            var brightness = Math.Clamp(channel.Brightness, 0, 255);
            // 必须先切常亮（频闪关闭 0xAA）：频闪模式下无外触发，只发亮度帧灯不会亮。
            // 注意协议里 0x55 = 频闪打开、0xAA = 频闪关闭，与直觉相反，勿改成 0x55。
            ok &= SendFrame(DongguanLightProtocol.BuildSetConstantOn(ch));
            ok &= SendFrame(DongguanLightProtocol.BuildSetBrightness(ch, brightness));
        }

        return ok;
    }

    /// <summary>
    /// 熄灭全部通道（按控制器通道数逐路把亮度设为 0）。
    /// 返回 false 表示至少一帧未发出——上层据此报错，避免「UI 说已熄灯、实际一帧没发」。
    /// </summary>
    public bool TurnOff()
    {
        var ok = true;
        for (var ch = 1; ch <= _channelCount; ch++)
            ok &= SendFrame(DongguanLightProtocol.BuildSetPercent(ch, 0));
        return ok;
    }

    /// <summary>
    /// 发送原始指令：十六进制帧或 ASCII（支持 \r \n \t 转义）。
    /// 与 <see cref="Apply"/>/<see cref="TurnOff"/> 对称返回是否发出——
    /// 协议调试框据此显示成败；失败原因见 <see cref="LastTransportError"/>。
    /// </summary>
    public bool SendRaw(string command)
    {
        if (string.IsNullOrEmpty(command))
            return true;
        return SendFrame(EncodeRaw(command));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        lock (_sendLock)
        {
            try { _port?.Close(); } catch { /* 尽力而为 */ }
            try { _port?.Dispose(); } catch { /* 尽力而为 */ }
            _port = null;
        }
    }

    internal static byte[] EncodeRaw(string command)
    {
        if (DongguanLightProtocol.TryParseHexBytes(command, out var hex))
            return hex;
        return Encoding.ASCII.GetBytes(Unescape(command));
    }

    private static string Unescape(string text) =>
        text.Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);

    /// <summary>
    /// 发送一帧。每帧**写入前**保证与上一帧至少间隔 <see cref="FrameGapMs"/>——
    /// 覆盖跨越两条命令的边界（如「关灯」最后一帧紧接着「开灯」第一帧），
    /// 而原先只在"同一命令内部"补间隔。
    /// <para>
    /// 依据（2026-09-14 硬件实测）：把两条 5 字节帧**零间隔**紧贴发出，第二帧会被控制器吃掉
    /// （通道 2 写 0 后回读仍是原值）；插入 100ms 间隔即正常。把间隔做在"写之前"而不是"写之后"，
    /// 既覆盖边界，又不给最后一条命令白付一次等待。
    /// </para>
    /// <para>
    /// ⚠️ 这只是加固，**并没有解决**"应用发的帧时灵时不灵"——该现象在改动前后同样出现，
    /// 详见类注释「未决项」。
    /// </para>
    /// </summary>
    private bool SendFrame(byte[] frame)
    {
        if (frame.Length == 0)
            return true;
        if (_disposed)
            return false;

        lock (_sendLock)
        {
            try
            {
                EnsurePortOpen();
                if (_port is null)
                    return false;

                var waitMs = FrameGapRemainingMs(_lastWriteTicks, Environment.TickCount64, FrameGapMs);
                if (waitMs > 0)
                    Thread.Sleep(waitMs);

                try { _port.DiscardInBuffer(); } catch { /* 旧应答不影响本帧 */ }
                _port.Write(frame, 0, frame.Length);
                _lastWriteTicks = Environment.TickCount64;
                try { _port.DiscardInBuffer(); } catch { /* 回显丢弃，避免堆积 */ }
                LastTransportError = null;
                return true;
            }
            catch (Exception ex)
            {
                // 只留痕、不外抛：Apply/TurnOff 用返回值表达失败（见 ILightController 注释）。
                // 但失败原因必须留下——否则上层只能报「指令发送失败」，
                // 现场分不清是没插线、串口被占用还是协议不对（2026-09-14 排查成本极高）。
                LastTransportError = $"{_portName}: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }
    }

    /// <summary>
    /// 距离"上一帧写完"还需等待多少毫秒才能发下一帧（0 = 可以立即发）。
    /// <paramref name="lastWriteTicks"/> 为 0 表示还没发过。抽成纯函数便于单测（避免计时型脆弱断言）。
    /// </summary>
    internal static int FrameGapRemainingMs(long lastWriteTicks, long nowTicks, int gapMs)
    {
        if (lastWriteTicks == 0)
            return 0;
        var remain = gapMs - (int)(nowTicks - lastWriteTicks);
        return remain > 0 ? remain : 0;
    }

    /// <summary>懒打开串口：未打开时尝试打开，失败保持 null（发送时静默跳过）。</summary>
    private void EnsurePortOpen()
    {
        if (_port is { IsOpen: true })
            return;

        _port?.Dispose();
        var port = new SerialPort(_portName, _baudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = _timeoutMs,
            WriteTimeout = _timeoutMs,
            // 显式断言 DTR/RTS：默认 false。现场的 USB 转串口（Prolific）适配器下，
            // 手工联调脚本一直是显式置位后才能稳定发帧，故对齐该做法。
            // 注意：2026-09-14 的"应用发的帧时灵时不灵"并未因此项解决（见类注释的未决项），
            // 不要把它当成已验证的修复，只是与可用配置保持一致。
            DtrEnable = true,
            RtsEnable = true,
        };
        try
        {
            port.Open();
        }
        catch
        {
            // 打开失败必须立即释放刚创建的端口对象，否则每次发送都会泄漏 COM 句柄与内部事件句柄。
            port.Dispose();
            throw;
        }

        _port = port;
        _lastWriteTicks = 0;
    }
}
