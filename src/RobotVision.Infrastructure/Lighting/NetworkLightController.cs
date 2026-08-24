using System.Net;
using System.Net.Sockets;
using System.Text;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>网络光源控制器使用的传输协议。</summary>
public enum LightProtocol
{
    /// <summary>UDP（无连接，指令即发即走；频闪控制器常用，参照 ECLightControl.LightEthernetType=0）。</summary>
    Udp,

    /// <summary>TCP（有连接，需保活与重连；参照 ECLightControl.LightEthernetType=1）。</summary>
    Tcp,
}

/// <summary>
/// 网络光源控制器：UDP/TCP 双协议，参照 VPDLFramework 的 <c>ECLightControl</c> 传输层结构——
/// 发送串行化（每实例锁）、TCP 断线自动重连、TCP 心跳保活。
/// 指令帧默认采用 ASCII 行协议（<c>SET &lt;ch&gt; &lt;brightness&gt;\r\n</c>），
/// 实际控制器协议不同时替换 <see cref="BuildSetFrame"/> / <see cref="BuildOffFrame"/> / <see cref="BuildHeartbeatFrame"/>。
/// </summary>
public sealed class NetworkLightController : ILightController
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly int _reconnectAttempts;
    private readonly IPEndPoint? _localEndPoint;
    private readonly object _sendLock = new();
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(2);

    private UdpClient? _udp;
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private System.Timers.Timer? _heartbeatTimer;
    private bool _disposed;

    public string Id { get; }

    public LightControllerKind Kind =>
        Protocol == LightProtocol.Udp ? LightControllerKind.Udp : LightControllerKind.Tcp;

    /// <summary>传输协议（UDP / TCP）。</summary>
    public LightProtocol Protocol { get; }

    public NetworkLightController(
        string id,
        LightProtocol protocol,
        string host,
        int port,
        int timeoutMs = 200,
        int reconnectAttempts = 3,
        IPEndPoint? localEndPoint = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("光源控制器 Id 不能为空", nameof(id));
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("光源控制器主机不能为空", nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "端口必须在 1~65535");

        Id = id;
        Protocol = protocol;
        _host = host;
        _port = port;
        _timeoutMs = Math.Max(1, timeoutMs);
        _reconnectAttempts = Math.Max(0, reconnectAttempts);
        _localEndPoint = localEndPoint;
    }

    /// <summary>
    /// 按照明配置点亮光源（幂等）：逐通道设置亮度并点亮。
    /// 空配置/空通道静默无操作；发送失败仅记录，不抛异常（保持上层流程健壮）。
    /// </summary>
    public void Apply(LightingConfig lighting)
    {
        if (lighting?.Channels is null || lighting.Channels.Count == 0)
            return;

        foreach (var channel in lighting.Channels)
        {
            var brightness = Math.Clamp(channel.Brightness, 0, 255);
            SendFrame(BuildSetFrame(channel.Channel, brightness));
        }
    }

    /// <summary>熄灭全部通道。</summary>
    public void TurnOff() => SendFrame(BuildOffFrame());

    /// <summary>
    /// 发送原始指令（协议调试）：输入文本中的 \r \n \t 转义序列解析为真实字符后发送。
    /// 例：输入 "SET 1 128\r\n" → 发送字节 53 45 54 20 31 20 31 32 38 0D 0A。
    /// </summary>
    public void SendRaw(string command)
    {
        if (string.IsNullOrEmpty(command))
            return;
        SendFrame(Encoding.ASCII.GetBytes(Unescape(command)));
    }

    private static string Unescape(string text) =>
        text.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t");

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        StopHeartbeat();

        lock (_sendLock)
        {
            try { _tcpStream?.Dispose(); } catch { /* 尽力而为 */ }
            try { _tcp?.Dispose(); } catch { /* 尽力而为 */ }
            try { _udp?.Dispose(); } catch { /* 尽力而为 */ }
            _tcpStream = null;
            _tcp = null;
            _udp = null;
        }
    }

    // ---- 指令帧（ASCII 行协议；按实际控制器协议替换） ----

    /// <summary>点亮指定通道并设亮度：SET &lt;通道号&gt; &lt;亮度0-255&gt;\r\n</summary>
    private static byte[] BuildSetFrame(int channel, int brightness) =>
        Encoding.ASCII.GetBytes($"SET {channel} {brightness}\r\n");

    /// <summary>熄灭全部通道：OFF ALL\r\n</summary>
    private static byte[] BuildOffFrame() =>
        Encoding.ASCII.GetBytes("OFF ALL\r\n");

    /// <summary>心跳帧：PING\r\n（TCP 保活）</summary>
    private static byte[] BuildHeartbeatFrame() =>
        Encoding.ASCII.GetBytes("PING\r\n");

    // ---- 传输层（参照 ECLightControl） ----

    private void SendFrame(byte[] frame)
    {
        if (frame.Length == 0)
            return;

        lock (_sendLock)
        {
            if (_disposed)
                return;
            try
            {
                if (Protocol == LightProtocol.Udp)
                    SendUdp(frame);
                else
                    SendTcp(frame);
            }
            catch
            {
                // 发送失败不抛异常：上层取图流程不受光源故障影响（尽力而为语义）
            }
        }
    }

    private void SendUdp(byte[] frame)
    {
        _udp ??= _localEndPoint is null
            ? new UdpClient()
            : new UdpClient(_localEndPoint);
        _udp.Client.SendTimeout = _timeoutMs;
        _udp.Send(frame, frame.Length, _host, _port);
    }

    private void SendTcp(byte[] frame)
    {
        EnsureTcpConnected();
        if (_tcp is null || _tcpStream is null)
            return;
        _tcpStream.Write(frame, 0, frame.Length);
        _tcpStream.Flush();
    }

    /// <summary>确保 TCP 已连接；未连接时按重试次数尝试（参照 ECLightControl.ReconnectTCP）。</summary>
    private void EnsureTcpConnected()
    {
        if (_tcp is { Connected: true } && _tcpStream is not null)
            return;

        for (var attempt = 0; attempt <= _reconnectAttempts; attempt++)
        {
            try
            {
                _tcpStream?.Dispose();
                _tcp?.Dispose();

                var client = new TcpClient
                {
                    NoDelay = true,
                };
                var connect = client.ConnectAsync(_host, _port);
                if (!connect.Wait(_timeoutMs))
                {
                    client.Dispose();
                    continue;
                }

                _tcp = client;
                _tcpStream = client.GetStream();
                _tcpStream.ReadTimeout = _timeoutMs;
                _tcpStream.WriteTimeout = _timeoutMs;

                // TCP 首次建连成功后启动心跳保活（参照 ECLightControl.StartHeartBeatTimer）
                StartHeartbeat();
                return;
            }
            catch
            {
                _tcp?.Dispose();
                _tcp = null;
                _tcpStream = null;
            }
        }
    }

    private void StartHeartbeat()
    {
        if (_heartbeatTimer is not null)
            return;

        var timer = new System.Timers.Timer(_heartbeatInterval.TotalMilliseconds);
        timer.Elapsed += (_, _) => SendFrame(BuildHeartbeatFrame());
        timer.Start();
        _heartbeatTimer = timer;
    }

    private void StopHeartbeat()
    {
        if (_heartbeatTimer is null)
            return;
        _heartbeatTimer.Stop();
        _heartbeatTimer.Dispose();
        _heartbeatTimer = null;
    }
}
