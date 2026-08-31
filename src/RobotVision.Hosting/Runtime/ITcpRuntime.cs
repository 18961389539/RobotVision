using RobotVision.Infrastructure.Communication;

namespace RobotVision.Hosting;

/// <summary>TCP 通信运行时（WPF/宿主稳定入口）。</summary>
public interface ITcpRuntime
{
    const long IdleTimeoutThirtyDaysMs = TcpServerManager.IdleTimeoutThirtyDaysMs;

    bool IsRunning { get; }
    int ConnectedClients { get; }
    string ListenEndPoint { get; }
    long TotalConnections { get; }
    long TotalRequests { get; }
    long RejectedConnections { get; }
    int TimeoutMs { get; set; }
    long IdleTimeoutMs { get; set; }
    int Backlog { get; set; }
    int MaxConnections { get; set; }
    bool PlcAlwaysOkMode { get; set; }
    double PlcDebugDefaultX { get; set; }
    double PlcDebugDefaultY { get; set; }
    double PlcDebugDefaultRz { get; set; }

    IReadOnlyList<string> IpWhitelist { get; set; }

    event Action<TcpClientSnapshot>? ClientConnected;
    event Action<TcpClientSnapshot>? ClientDisconnected;
    event Action<TcpRequestRecord>? RequestStarted;
    event Action<TcpRequestRecord>? RequestProcessed;

    IReadOnlyList<TcpClientSnapshot> GetClients();
    IReadOnlyList<TcpRequestRecord> GetRecentRequests();
    void Start();
    void Stop();
    bool Restart(string ipAddress, int port);
    void DisconnectClient(long clientId);
}

internal sealed class TcpRuntime(TcpServerManager inner) : ITcpRuntime
{
    public bool IsRunning => inner.IsRunning;
    public int ConnectedClients => inner.ConnectedClients;
    public string ListenEndPoint => inner.ListenEndPoint;
    public long TotalConnections => inner.TotalConnections;
    public long TotalRequests => inner.TotalRequests;
    public long RejectedConnections => inner.RejectedConnections;

    public int TimeoutMs
    {
        get => inner.TimeoutMs;
        set => inner.TimeoutMs = value;
    }

    public long IdleTimeoutMs
    {
        get => inner.IdleTimeoutMs;
        set => inner.IdleTimeoutMs = value;
    }

    public int Backlog
    {
        get => inner.Backlog;
        set => inner.Backlog = value;
    }

    public int MaxConnections
    {
        get => inner.MaxConnections;
        set => inner.MaxConnections = value;
    }

    public IReadOnlyList<string> IpWhitelist
    {
        get => inner.IpWhitelist;
        set => inner.IpWhitelist = value;
    }

    public bool PlcAlwaysOkMode
    {
        get => inner.PlcAlwaysOkMode;
        set => inner.PlcAlwaysOkMode = value;
    }

    public double PlcDebugDefaultX
    {
        get => inner.PlcDebugDefaultX;
        set => inner.PlcDebugDefaultX = value;
    }

    public double PlcDebugDefaultY
    {
        get => inner.PlcDebugDefaultY;
        set => inner.PlcDebugDefaultY = value;
    }

    public double PlcDebugDefaultRz
    {
        get => inner.PlcDebugDefaultRz;
        set => inner.PlcDebugDefaultRz = value;
    }

    public event Action<TcpClientSnapshot>? ClientConnected
    {
        add => inner.ClientConnected += value;
        remove => inner.ClientConnected -= value;
    }

    public event Action<TcpClientSnapshot>? ClientDisconnected
    {
        add => inner.ClientDisconnected += value;
        remove => inner.ClientDisconnected -= value;
    }

    public event Action<TcpRequestRecord>? RequestStarted
    {
        add => inner.RequestStarted += value;
        remove => inner.RequestStarted -= value;
    }

    public event Action<TcpRequestRecord>? RequestProcessed
    {
        add => inner.RequestProcessed += value;
        remove => inner.RequestProcessed -= value;
    }

    public IReadOnlyList<TcpClientSnapshot> GetClients() => inner.GetClients();
    public IReadOnlyList<TcpRequestRecord> GetRecentRequests() => inner.GetRecentRequests();
    public void Start() => inner.Start();
    public void Stop() => inner.Stop();
    public bool Restart(string ipAddress, int port) => inner.Restart(ipAddress, port);
    public void DisconnectClient(long clientId) => inner.DisconnectClient(clientId);
}
