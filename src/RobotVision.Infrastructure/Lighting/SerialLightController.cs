using System.IO.Ports;
using System.Text;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 串口光源控制器（RS232/RS485）：参照 NetworkLightController 的结构——
/// 发送串行化（每实例锁）、懒打开串口（未接硬件时注册成功、发送静默失败）、
/// 指令帧 ASCII 行协议（<c>SET &lt;ch&gt; &lt;brightness&gt;\r\n</c>）。
/// 实际控制器协议不同时替换 <see cref="BuildSetFrame"/> / <see cref="BuildOffFrame"/>。
/// </summary>
public sealed class SerialLightController : ILightController
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _timeoutMs;
    private readonly object _sendLock = new();

    private SerialPort? _port;
    private bool _disposed;

    public string Id { get; }

    public LightControllerKind Kind => LightControllerKind.Serial;

    public SerialLightController(string id, string portName, int baudRate, int timeoutMs = 200)
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
    }

    /// <summary>按照明配置点亮光源（幂等）：逐通道设置亮度并点亮。发送失败返回 false。</summary>
    public bool Apply(LightingConfig lighting)
    {
        if (lighting?.Channels is null || lighting.Channels.Count == 0)
            return true;

        var ok = true;
        foreach (var channel in lighting.Channels)
        {
            var brightness = Math.Clamp(channel.Brightness, 0, 255);
            ok &= SendFrame(BuildSetFrame(channel.Channel, brightness));
        }

        return ok;
    }

    /// <summary>熄灭全部通道。</summary>
    public void TurnOff() => SendFrame(BuildOffFrame());

    /// <summary>发送原始指令（协议调试）：\r \n \t 转义解析后按 ASCII 写入串口。</summary>
    public void SendRaw(string command)
    {
        if (string.IsNullOrEmpty(command))
            return;
        SendFrame(Encoding.ASCII.GetBytes(Unescape(command)));
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

    // ---- 指令帧（ASCII 行协议；按实际控制器协议替换） ----

    private static byte[] BuildSetFrame(int channel, int brightness) =>
        Encoding.ASCII.GetBytes($"SET {channel} {brightness}\r\n");

    private static byte[] BuildOffFrame() =>
        Encoding.ASCII.GetBytes("OFF ALL\r\n");

    private static string Unescape(string text) =>
        text.Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);

    // ---- 传输层 ----

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
                _port.Write(frame, 0, frame.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }
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
            ReadTimeout = _timeoutMs,
            WriteTimeout = _timeoutMs,
        };
        port.Open();
        _port = port;
    }
}
