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

    event Action<TcpClientView>? ClientConnected;
    event Action<TcpClientView>? ClientDisconnected;
    event Action<TcpRequestView>? RequestStarted;
    event Action<TcpRequestView>? RequestProcessed;

    IReadOnlyList<TcpClientView> GetClients();
    IReadOnlyList<TcpRequestView> GetRecentRequests();
    void Start();
    void Stop();
    bool Restart(string ipAddress, int port);
    void DisconnectClient(long clientId);
}

internal sealed class TcpRuntime(TcpServerManager inner) : ITcpRuntime
{
    private readonly Dictionary<Action<TcpClientView>, Action<TcpClientSnapshot>> _connected = new();
    private readonly Dictionary<Action<TcpClientView>, Action<TcpClientSnapshot>> _disconnected = new();
    private readonly Dictionary<Action<TcpRequestView>, Action<TcpRequestRecord>> _started = new();
    private readonly Dictionary<Action<TcpRequestView>, Action<TcpRequestRecord>> _processed = new();

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

    public event Action<TcpClientView>? ClientConnected
    {
        add
        {
            if (value is null)
                return;
            Action<TcpClientSnapshot> wrapper = s => value(TcpViewMapper.Map(s));
            lock (_connected)
                _connected[value] = wrapper;
            inner.ClientConnected += wrapper;
        }
        remove
        {
            if (value is null)
                return;
            lock (_connected)
            {
                if (_connected.Remove(value, out var wrapper))
                    inner.ClientConnected -= wrapper;
            }
        }
    }

    public event Action<TcpClientView>? ClientDisconnected
    {
        add
        {
            if (value is null)
                return;
            Action<TcpClientSnapshot> wrapper = s => value(TcpViewMapper.Map(s));
            lock (_disconnected)
                _disconnected[value] = wrapper;
            inner.ClientDisconnected += wrapper;
        }
        remove
        {
            if (value is null)
                return;
            lock (_disconnected)
            {
                if (_disconnected.Remove(value, out var wrapper))
                    inner.ClientDisconnected -= wrapper;
            }
        }
    }

    public event Action<TcpRequestView>? RequestStarted
    {
        add
        {
            if (value is null)
                return;
            Action<TcpRequestRecord> wrapper = r => value(TcpViewMapper.Map(r));
            lock (_started)
                _started[value] = wrapper;
            inner.RequestStarted += wrapper;
        }
        remove
        {
            if (value is null)
                return;
            lock (_started)
            {
                if (_started.Remove(value, out var wrapper))
                    inner.RequestStarted -= wrapper;
            }
        }
    }

    public event Action<TcpRequestView>? RequestProcessed
    {
        add
        {
            if (value is null)
                return;
            Action<TcpRequestRecord> wrapper = r => value(TcpViewMapper.Map(r));
            lock (_processed)
                _processed[value] = wrapper;
            inner.RequestProcessed += wrapper;
        }
        remove
        {
            if (value is null)
                return;
            lock (_processed)
            {
                if (_processed.Remove(value, out var wrapper))
                    inner.RequestProcessed -= wrapper;
            }
        }
    }

    public IReadOnlyList<TcpClientView> GetClients() =>
        inner.GetClients().Select(TcpViewMapper.Map).ToList();

    public IReadOnlyList<TcpRequestView> GetRecentRequests() =>
        inner.GetRecentRequests().Select(TcpViewMapper.Map).ToList();

    public void Start() => inner.Start();
    public void Stop() => inner.Stop();
    public bool Restart(string ipAddress, int port) => inner.Restart(ipAddress, port);
    public void DisconnectClient(long clientId) => inner.DisconnectClient(clientId);
}
