using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Communication;

/// <summary>
/// TCP 服务管理类：监听、连接注册表、行协议解析、应答与超时。
/// 协议（ASCII；请求可带或不带换行，应答仍以 \n 结尾）：
///   请求: 配方名 或 序列号（#3 / 3）              应答: OK,x,y,角度[,x2,y2,角度2...],配方名,目标数,耗时ms
///   请求: 配方键,X,Y,RZ（键=名称或序列号）         应答: 同上（OnArm 工位校验拍照位姿，不一致 1012）
///   请求: PING               应答: PONG
///   请求: STATUS             应答: OK,ready|busy,队列深度,队列上限,最近耗时ms,连续失败,联锁0|1
///   请求: CLEARINHIBIT 或 CLEARINHIBIT,键（键=配方名或序列号）  应答: OK,CLEARED
///   出错: ERR,错误码,消息
/// 错误消息契约：业务错误保留 Sanitize 后的可读消息；InternalError 固定为
/// INTERNAL_ERROR（详情只进日志）；未知命令/缺参数/参数格式错误用固定 ASCII 模板。
/// 坐标三元组紧跟 OK，PLC 可顺序读取；配方名与目标数在耗时前，便于从尾部校验。
/// 也为将来"0 目标返回空 OK（count=0）"预留了非破坏性扩展（当前 0 目标仍返回 ERR 1007）。
/// 分帧：遇到 \n / \r\n / \r 立即切帧；无结束符时静默 TcpLineFramer.QuietMs 后提交。
/// </summary>
public sealed class TcpServerManager : IDisposable
{
    /// <summary>STATUS 命令状态快照（由组装层从 VisionService 注入）。
    /// ConsecutiveFails / Inhibited 为附加字段（协议向后兼容：旧 PLC 只读前 5 段）。</summary>
    public sealed record TcpServerState(
        bool Ready, int QueueDepth, int MaxQueueDepth, double LastElapsedMs,
        int ConsecutiveFails = 0, int Inhibited = 0);

    private IPAddress _address;
    private int _port;
    private readonly Func<string, TcpClientPose?, CancellationToken, Task<VisionResult>> _handler;
    private readonly ILogger<TcpServerManager> _log;
    private readonly ConcurrentDictionary<long, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<long, TcpSession> _sessions = new();
    private long _totalConnections;
    private long _totalRequests;
    private long _rejectedConnections;
    private const int MaxRequestHistory = 200;
    private readonly ConcurrentQueue<TcpRequestRecord> _requestHistory = new();
    private int _requestHistoryCount;

    /// <summary>启停/热重启互斥锁：Start/Stop/Restart 串行化，防止并发启停把监听状态弄乱。</summary>
    private readonly object _lifecycleLock = new();

    /// <summary>30 天空闲超时（ms），设置页快捷填入。int 装不下，用 long。</summary>
    public const long IdleTimeoutThirtyDaysMs = 30L * 24 * 60 * 60 * 1000;

    private volatile TcpListener? _listener;
    private volatile CancellationTokenSource? _cts;
    private long _nextClientId;

    public TcpServerManager(
        string ipAddress,
        int port,
        int timeoutMs,
        Func<string, TcpClientPose?, CancellationToken, Task<VisionResult>> handler,
        ILogger<TcpServerManager> log)
    {
        _address = IPAddress.Parse(ipAddress);
        _port = port;
        TimeoutMs = timeoutMs;
        _handler = handler;
        _log = log;
    }

    public int ConnectedClients => _clients.Count;

    /// <summary>监听端口（诊断/状态栏显示用）。</summary>
    public int Port => _port;

    /// <summary>监听端点文本，如 0.0.0.0:9999。</summary>
    public string ListenEndPoint => $"{_address}:{_port}";

    /// <summary>单请求处理超时（ms）。热属性：管理界面可直接修改。
    /// volatile 保证套接字线程能即时读到 UI 改动；≤0 按 1ms clamp（CancelAfter(0) 会立即超时）。
    /// 只约束触发行处理，不用于空闲断线。</summary>
    private volatile int _timeoutMs;

    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = Math.Max(1, value);
    }

    /// <summary>读侧空闲超时（ms）。0 = 永不因空闲断开（默认，PLC 节拍间隙不断线）。
    /// 半开连接仍由 TCP KeepAlive 探测。热属性。</summary>
    private long _idleTimeoutMs;

    public long IdleTimeoutMs
    {
        get => Interlocked.Read(ref _idleTimeoutMs);
        set => Interlocked.Exchange(ref _idleTimeoutMs, Math.Max(0, value));
    }

    /// <summary>监听 backlog（内核排队待 accept 的连接数）。</summary>
    public int Backlog { get; set; } = 16;

    /// <summary>
    /// STATUS 命令状态提供者（组装层注入 VisionService 的实时状态）。
    /// 未注入时 STATUS 返回 OK,ready,0,0,0。
    /// </summary>
    public Func<TcpServerState>? StateProvider { get; set; }

    /// <summary>
    /// CLEARINHIBIT 命令：解除连续失败联锁。参数为配方名、序列号或空（全部）。
    /// 未注入时该命令按未知命令处理。
    /// </summary>
    public Func<string?, string>? ClearInhibitHandler { get; set; }

    /// <summary>并发连接数上限，0 表示不限。热属性：接入时检查。</summary>
    public int MaxConnections { get; set; }

    /// <summary>
    /// IP 白名单（热属性）。空 = 允许所有来源；条目支持精确 IP（192.168.1.10）
    /// 或前缀通配（192.168.*）。不在名单内的接入立即断开。
    /// </summary>
    public IReadOnlyList<string> IpWhitelist { get; set; } = [];

    /// <summary>监听是否处于运行状态（UI 启停控制用）。</summary>
    public bool IsRunning => _listener is not null;

    /// <summary>累计接入的连接数（进程生命周期内）。</summary>
    public long TotalConnections => Interlocked.Read(ref _totalConnections);

    /// <summary>累计拒绝的连接数（白名单/连接上限）。</summary>
    public long RejectedConnections => Interlocked.Read(ref _rejectedConnections);

    /// <summary>累计处理的请求数（含 PING 与错误应答）。</summary>
    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    /// <summary>客户端接入（套接字线程回调，订阅方需自行封送 UI 线程；订阅方异常不影响通信）。</summary>
    public event Action<TcpClientSnapshot>? ClientConnected;

    /// <summary>客户端断开（套接字线程回调）。</summary>
    public event Action<TcpClientSnapshot>? ClientDisconnected;

    /// <summary>读到完整行、尚未处理（套接字线程回调）。用于监控页在取图/推理期间显示「处理中」。</summary>
    public event Action<TcpRequestRecord>? RequestStarted;

    /// <summary>请求处理完成（套接字线程回调）。</summary>
    public event Action<TcpRequestRecord>? RequestProcessed;

    /// <summary>当前连接快照（按接入顺序排列）。</summary>
    public IReadOnlyList<TcpClientSnapshot> GetClients() =>
        _sessions.Values.Select(s => s.Snapshot()).OrderBy(s => s.Id).ToList();

    /// <summary>最近完成的请求（新→旧，最多 200 条）。监控页晚打开也能看到历史。</summary>
    public IReadOnlyList<TcpRequestRecord> GetRecentRequests() =>
        _requestHistory.Reverse().ToList();

    /// <summary>主动断开指定客户端；清理与断开事件走正常断开路径。</summary>
    public bool DisconnectClient(long id)
    {
        if (!_clients.TryGetValue(id, out var client))
            return false;

        if (_sessions.TryGetValue(id, out var session))
            session.ManualClose = true;
        try { client.Close(); }
        catch { /* 关闭阶段忽略 */ }
        return true;
    }

    public void Start()
    {
        lock (_lifecycleLock)
            StartCore();
    }

    public void Stop()
    {
        lock (_lifecycleLock)
            StopCore();
    }

    /// <summary>
    /// 服务内热重启监听端点（改 IP/端口无需重启程序）。
    /// 会断开现有客户端连接；新端点启动失败时回滚到旧端点并保持原监听。
    /// 启停全程持 _lifecycleLock，与 Start/Stop 互斥。
    /// </summary>
    /// <returns>true = 新端点已生效；false = 启动失败已回滚（旧端点仍在监听，除非回滚也失败）。</returns>
    public bool Restart(string ipAddress, int port)
    {
        if (!IPAddress.TryParse(ipAddress, out var newAddress))
            throw new ArgumentException($"IP 地址无效: {ipAddress}");
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "端口必须在 1~65535");

        lock (_lifecycleLock)
        {
            var oldAddress = _address;
            var oldPort = _port;
            var oldEnd = $"{oldAddress}:{oldPort}";

            StopCore();

            _address = newAddress;
            _port = port;
            try
            {
                StartCore();
                _log.LogInformation("TCP 监听已热重启: {Old} → {New}", oldEnd, $"{newAddress}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "监听热重启失败，回滚到 {Old}", oldEnd);
                StopCore();
                _address = oldAddress;
                _port = oldPort;
                try
                {
                    StartCore();
                }
                catch (Exception rollbackEx)
                {
                    _log.LogError(rollbackEx, "回滚监听也失败，服务保持停止状态");
                    return false;
                }
                return false;
            }
        }
    }

    private void StartCore()
    {
        if (_listener is not null)
            return;

        _cts = new CancellationTokenSource();
        var listener = new TcpListener(_address, _port);
        listener.Start(backlog: Math.Max(1, Backlog));
        _listener = listener;
        // WPF 在 Dispatcher 上 Host.Start()，此处若直接 AcceptLoopAsync，await 会回到 UI 线程，
        // 收包后的取图/推理会卡死界面。必须丢到线程池且不再捕获 SynchronizationContext。
        StartUncaptured(() => AcceptLoopAsync(listener, _cts.Token));
        _log.LogInformation("TCP 服务已启动: {Address}:{Port}", _address, _port);
    }

    private static void StartUncaptured(Func<Task> work) => _ = Task.Run(work);

    private void StopCore()
    {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();

        var listener = _listener;
        _listener = null;
        if (listener is not null)
        {
            try { listener.Stop(); }
            catch { /* 关闭阶段忽略 */ }
        }

        foreach (var client in _clients.Values)
        {
            try { client.Close(); }
            catch { /* 关闭阶段忽略 */ }
        }
        _clients.Clear();

        var leftoverSessions = _sessions.Values.ToArray();
        _sessions.Clear();
        foreach (var session in leftoverSessions)
            RaiseClientEvent(ClientDisconnected, session);

        cts?.Dispose();
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var id = Interlocked.Increment(ref _nextClientId);

                // 接入控制：白名单与连接上限均为热属性，逐连接检查
                var rawIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
                // IPv4-mapped IPv6（::ffff:192.168.1.10）归一化为 IPv4，与白名单里的
                // 192.168.1.10 视为同一地址，否则 IPv6 双栈监听下白名单形同虚设
                var remoteIp = rawIp?.IsIPv4MappedToIPv6 == true ? rawIp.MapToIPv4() : rawIp;
                if (!IsAddressAllowed(remoteIp))
                {
                    Interlocked.Increment(ref _rejectedConnections);
                    _log.LogWarning("连接被拒绝（IP 不在白名单）: {Remote}", RemoteText(client));
                    try { client.Close(); } catch { /* 关闭阶段忽略 */ }
                    continue;
                }

                if (MaxConnections > 0 && _clients.Count >= MaxConnections)
                {
                    Interlocked.Increment(ref _rejectedConnections);
                    _log.LogWarning("连接被拒绝（达到连接上限 {Limit}）: {Remote}", MaxConnections, RemoteText(client));
                    try { client.Close(); } catch { /* 关闭阶段忽略 */ }
                    continue;
                }

                var session = new TcpSession
                {
                    Id = id,
                    Remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown",
                    ConnectedAt = DateTime.Now,
                };
                _clients[id] = client;
                _sessions[id] = session;
                Interlocked.Increment(ref _totalConnections);
                _log.LogInformation("客户端 #{Id} 接入: {Remote}，当前连接数 {Count}",
                    id, session.Remote, _clients.Count);
                RaiseClientEvent(ClientConnected, session);
                StartUncaptured(() => HandleClientAsync(id, session, client, ct));
            }
        }
        catch (OperationCanceledException)
        {
            // 服务停止：正常退出
        }
        catch (ObjectDisposedException)
        {
            // 监听器被 Stop 释放：正常退出
        }
        catch (Exception ex)
        {
            // 意外异常兜底：AcceptLoop 静默死亡会让服务"看似运行实则无监听"，
            // 必须记录关键日志并尝试重启监听
            _log.LogCritical(ex, "TCP 监听循环异常退出，尝试重启监听");
            try { listener.Stop(); }
            catch { /* 关闭阶段忽略 */ }
            if (ct.IsCancellationRequested)
                return;

            // 必须清空 _listener 再 StartCore：否则 StartCore 见非空直接返回，重启是空操作。
            // 持 _lifecycleLock 与 Stop/Restart 互斥，避免把已停止的服务又拉起来。
            lock (_lifecycleLock)
            {
                if (_listener != listener)
                    return;
                _listener = null;
                var oldCts = _cts;
                _cts = null;
                try { oldCts?.Dispose(); }
                catch { /* 尽力而为 */ }
                try
                {
                    StartCore();
                }
                catch (Exception restartEx)
                {
                    _log.LogCritical(restartEx, "TCP 监听重启失败");
                }
            }
        }
    }

    private static string RemoteText(TcpClient client) =>
        client.Client.RemoteEndPoint?.ToString() ?? "unknown";

    /// <summary>白名单为空允许所有；条目支持 192.168.* 前缀通配。IPv4/IPv6 均按文本匹配。</summary>
    private bool IsAddressAllowed(IPAddress? address)
    {
        if (IpWhitelist.Count == 0)
            return true;
        if (address is null)
            return false;

        var ip = address.ToString();
        foreach (var entry in IpWhitelist)
        {
            var rule = entry.Trim();
            if (rule.Length == 0)
                continue;
            if (rule.EndsWith('*'))
            {
                if (ip.StartsWith(rule[..^1], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (string.Equals(ip, rule, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 白名单条目校验（与 <see cref="IsAddressAllowed"/> 的匹配语义严格一致）：
    /// 精确 IP（IPv4/IPv6）或以合法 IP 段结尾的 * 通配（如 192.168.* / 192.168.1.*）。
    /// 拒绝：中间通配（192.168.*.10）、非法 IP（999.1.1.1 / abc）、
    /// 四段前缀（192.168.1.5*，会按前缀误放行整个网段）。
    /// </summary>
    public static bool TryParseWhitelistEntry(string entry)
    {
        var e = entry.Trim();
        if (e.Length == 0)
            return false;

        if (e.EndsWith('*'))
        {
            var prefix = e[..^1].TrimEnd('.');
            var parts = prefix.Split('.');
            if (parts.Length is < 1 or > 3)
                return false;
            return parts.All(p => byte.TryParse(p, out _));
        }

        return IPAddress.TryParse(e, out _);
    }

    private async Task HandleClientAsync(long id, TcpSession session, TcpClient client, CancellationToken ct)
    {
        try
        {
            // 半开连接检测：客户端断电（无 FIN）时 TCP KeepAlive 在探测周期后自动断连，
            // 避免半开连接长期占用连接名额。探测参数为 Windows 特性，失败静默忽略。
            try
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                // IOControl 的 KeepAliveValues 是 Windows 特性,非 Windows 仅保留 SocketOption 级探测(CA1416 守卫)
                if (OperatingSystem.IsWindows())
                {
                    var keepAlive = new byte[12];
                    BitConverter.GetBytes(1).CopyTo(keepAlive, 0);      // 开启
                    BitConverter.GetBytes(5000).CopyTo(keepAlive, 4);   // 首次探测延迟（ms）
                    BitConverter.GetBytes(2000).CopyTo(keepAlive, 8);   // 探测间隔（ms）
                    client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
                }
            }
            catch
            {
                // 平台不支持 IOControl 时仅保留 SocketOption 级别探测
            }

            var stream = client.GetStream();
            try { client.NoDelay = true; } catch { /* 部分套接字实现不支持 */ }

            // 行协议固定 ASCII：PLC 侧按单字节字符集组包，避免 UTF-8 多字节歧义。
            // 应答仍 WriteLine（\n）；请求有无换行均可（见 TcpLineFramer）。
            using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };

            var pending = new StringBuilder();
            var complete = new List<string>();
            var chunk = new byte[1024];

            while (!ct.IsCancellationRequested)
            {
                complete.Clear();
                var peerClosed = false;
                var idleMs = IdleTimeoutMs;
                try
                {
                    if (pending.Length == 0)
                    {
                        int n;
                        if (idleMs > 0)
                        {
                            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            idleCts.CancelAfter(TimeSpan.FromMilliseconds(idleMs));
                            n = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), idleCts.Token)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            // 0 = 永久：不设读超时。停服/踢连接靠 StopCore 关闭套接字（Read 抛 IOException）。
                            n = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)
                                .ConfigureAwait(false);
                        }

                        if (n == 0)
                            break;

                        TcpLineFramer.Append(pending, chunk.AsSpan(0, n), complete);
                    }
                    else
                    {
                        // 已有未结束的命令：再等 QuietMs，无后续字节则提交；有则继续拼。
                        // 必须异步等待：同步 Poll 若跑在 UI 线程会把界面卡死 QuietMs。
                        using var quietCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        quietCts.CancelAfter(TcpLineFramer.QuietMs);
                        try
                        {
                            var n = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), quietCts.Token)
                                .ConfigureAwait(false);
                            if (n == 0)
                            {
                                complete.Add(pending.ToString());
                                pending.Clear();
                                peerClosed = true;
                            }
                            else
                            {
                                TcpLineFramer.Append(pending, chunk.AsSpan(0, n), complete);
                            }
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            complete.Add(pending.ToString());
                            pending.Clear();
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _log.LogWarning("客户端 #{Id} 读空闲超时（{Timeout}ms 无数据），断开", id, idleMs);
                    break;
                }
                catch (IOException)
                {
                    break;
                }

                foreach (var raw in complete)
                {
                    var line = raw.Trim();
                    if (line.Length == 0)
                        continue;

                    var startedAt = DateTime.Now;
                    var bytesIn = Encoding.ASCII.GetByteCount(line) + 1;
                    session.NoteIncoming(line);
                    _log.LogInformation("客户端 #{Id} 收到: {Line}", id, line);
                    RaiseRequestStarted(new TcpRequestRecord(
                        startedAt, session.Id, session.Remote, line, "",
                        false, 0, bytesIn, 0));

                    var stopwatch = Stopwatch.StartNew();
                    var reply = await ProcessRequestAsync(line, ct).ConfigureAwait(false);
                    stopwatch.Stop();

                    // 统计与事件在写出应答前完成：客户端收到应答时记录必然已可见
                    var bytesOut = Encoding.ASCII.GetByteCount(reply) + 1;
                    session.RecordRequest(line, bytesIn, bytesOut);
                    Interlocked.Increment(ref _totalRequests);
                    var record = new TcpRequestRecord(
                        startedAt, session.Id, session.Remote, line, reply,
                        !reply.StartsWith("ERR", StringComparison.Ordinal),
                        stopwatch.Elapsed.TotalMilliseconds, bytesIn, bytesOut);
                    RememberRequest(record);
                    RaiseRequestProcessed(record);

                    // 写侧超时：客户端不读（TCP 窗口满）时不无限挂起，超时断开释放连接
                    using (var writeCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        writeCts.CancelAfter(TimeoutMs);
                        await writer.WriteLineAsync(reply.AsMemory(), writeCts.Token).ConfigureAwait(false);
                    }
                }

                if (peerClosed)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // 写应答超时（客户端不读、TCP 窗口满）或服务停止：断开，避免挂起占用连接
            _log.LogInformation("客户端 #{Id} 写应答超时/取消，断开", id);
        }
        catch (Exception ex)
        {
            if (session.ManualClose)
                _log.LogInformation("客户端 #{Id} 被手动断开", id);
            else
                _log.LogWarning(ex, "客户端 #{Id} 连接异常", id);
        }
        finally
        {
            _clients.TryRemove(id, out _);
            _sessions.TryRemove(id, out var removed);
            client.Dispose();
            _log.LogInformation("客户端 #{Id} 断开，当前连接数 {Count}", id, _clients.Count);
            if (removed is not null)
                RaiseClientEvent(ClientDisconnected, removed);
        }
    }

    /// <summary>逐个回调并吞掉订阅方异常，保证通信线程不受 UI 订阅者影响。</summary>
    private void RaiseClientEvent(Action<TcpClientSnapshot>? handlers, TcpSession session)
    {
        if (handlers is null)
            return;
        var snapshot = session.Snapshot();
        foreach (var handler in handlers.GetInvocationList().Cast<Action<TcpClientSnapshot>>())
        {
            try { handler(snapshot); }
            catch { /* 订阅方异常不得影响通信线程 */ }
        }
    }

    private void RaiseRequestStarted(TcpRequestRecord record)
    {
        var handlers = RequestStarted;
        if (handlers is null)
            return;
        foreach (var handler in handlers.GetInvocationList().Cast<Action<TcpRequestRecord>>())
        {
            try { handler(record); }
            catch { /* 订阅方异常不得影响通信线程 */ }
        }
    }

    private void RaiseRequestProcessed(TcpRequestRecord record)
    {
        var handlers = RequestProcessed;
        if (handlers is null)
            return;
        foreach (var handler in handlers.GetInvocationList().Cast<Action<TcpRequestRecord>>())
        {
            try { handler(record); }
            catch { /* 订阅方异常不得影响通信线程 */ }
        }
    }

    private void RememberRequest(TcpRequestRecord record)
    {
        _requestHistory.Enqueue(record);
        if (Interlocked.Increment(ref _requestHistoryCount) <= MaxRequestHistory)
            return;
        if (_requestHistory.TryDequeue(out _))
            Interlocked.Decrement(ref _requestHistoryCount);
    }

    private async Task<string> ProcessRequestAsync(string line, CancellationToken ct)
    {
        if (string.Equals(line, "PING", StringComparison.OrdinalIgnoreCase))
            return "PONG";

        if (string.Equals(line, "STATUS", StringComparison.OrdinalIgnoreCase))
            return FormatStatus(StateProvider?.Invoke());

        if (IsClearInhibitCommand(line))
        {
            if (ClearInhibitHandler is null)
                return FormatReply(VisionResult.Fail("", VisionErrorCode.UnknownCommand, "UNKNOWN_COMMAND", 0));
            var recipe = ParseClearInhibitRecipe(line);
            return ClearInhibitHandler(recipe);
        }

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return FormatReply(VisionResult.Fail("", VisionErrorCode.UnknownRecipe, "MISSING_RECIPE", 0));

        var (recipeKey, pose, formatError) = ParseTriggerLine(trimmed);
        if (formatError is not null)
            return $"ERR,{(int)VisionErrorCode.InvalidTriggerArgument},{formatError}";

        // 超时只保证尽快回复错误；正在执行的推理不会中断（串行管线由信号量保护）
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeoutMs);

        var stopwatch = Stopwatch.StartNew();
        Task<VisionResult>? work = null;
        try
        {
            work = _handler(recipeKey, pose, timeoutCts.Token);
            var result = await work.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            // 内部错误详情（异常原文/路径等）只进日志，不上协议线
            if (!result.Ok && result.ErrorCode == VisionErrorCode.InternalError)
                _log.LogWarning("内部错误应答（详情仅日志）: 配方 {Recipe} · {Message}",
                    result.RecipeName, result.Message);

            return FormatReply(result);
        }
        catch (OperationCanceledException)
        {
            // 竞态窗口：VisionService 可能已把排队超时(1010)写入结果——此时优先返回该结果，
            // 避免排队超时被本层统一覆盖成处理超时(1008)
            if (work is { IsCompletedSuccessfully: true })
                return FormatReply(await work.ConfigureAwait(false));
            return FormatReply(VisionResult.Fail(
                recipeKey, VisionErrorCode.Timeout, $"处理超时 >{TimeoutMs}ms", stopwatch.Elapsed.TotalMilliseconds));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "处理请求异常: {Line}", line);
            return FormatReply(VisionResult.Fail(
                recipeKey, VisionErrorCode.InternalError, ex.Message, stopwatch.Elapsed.TotalMilliseconds));
        }
    }

    /// <summary>
    /// 触发行解析（纯函数，可单测）：
    /// - 1 段（配方名或序列号）→ pose = null；
    /// - 4 段（键,X,Y,RZ）→ 带拍照位姿（OnArm 工位一致性校验用）；
    /// - 其他段数 / 数值非有限 → formatError（ERR,1013 模板）。
    /// 序列号允许 <c>#3</c> 前缀；数值按 InvariantCulture 解析。
    /// </summary>
    public static (string RecipeKey, TcpClientPose? Pose, string? FormatError) ParseTriggerLine(string line)
    {
        line = line.Trim();
        if (line.Length == 0)
            return ("", null, "MISSING_RECIPE");

        var parts = line.Split(',');
        if (parts.Length == 1)
            return (NormalizeTriggerKey(parts[0]), null, null);

        if (parts.Length != 4)
            return ("", null, "TRIGGER_ARGUMENT_COUNT");

        var key = NormalizeTriggerKey(parts[0]);
        if (key.Length == 0)
            return ("", null, "MISSING_RECIPE");

        if (!TryParseFinite(parts[1], out var x) ||
            !TryParseFinite(parts[2], out var y) ||
            !TryParseFinite(parts[3], out var rz))
            return ("", null, "INVALID_POSE_NUMBER");

        return (key, new TcpClientPose(x, y, rz), null);
    }

    /// <summary>兼容旧测试名；解析触发行首段之后的参数段（已弃用，请用 <see cref="ParseTriggerLine"/>）。</summary>
    [Obsolete("Use ParseTriggerLine")]
    public static (string RecipeName, TcpClientPose? Pose, string? FormatError) ParseTriggerArgument(string argument)
    {
        if (argument.Trim().Length == 0)
            return ("", null, "MISSING_RECIPE");
        return ParseTriggerLine(argument);
    }

    private static string NormalizeTriggerKey(string key)
    {
        key = key.Trim();
        if (key.StartsWith('#') && key.Length > 1)
            key = key[1..].Trim();
        return key;
    }

    /// <summary>有限数字解析（拒绝 NaN/Infinity：会污染位姿比对）。</summary>
    private static bool TryParseFinite(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value))
            return true;
        value = 0;
        return false;
    }

    /// <summary>STATUS 应答：OK,ready|busy,队列深度,队列上限,最近耗时ms,连续失败,联锁(0/1)。
    /// 前 5 段与历史版本一致；后 2 段为过程能力扩展，旧 PLC 可忽略。</summary>
    public static string FormatStatus(TcpServerState? state)
    {
        if (state is null)
            return "OK,ready,0,0,0";
        var inhibited = state.Inhibited != 0 ? 1 : 0;
        return $"OK,{(state.Ready ? "ready" : "busy")},{state.QueueDepth},{state.MaxQueueDepth},{state.LastElapsedMs:0},{state.ConsecutiveFails},{inhibited}";
    }

    /// <summary>
    /// CLEARINHIBIT 或 CLEARINHIBIT,键（键=配方名或序列号）。
    /// 前缀粘连（CLEARINHIBITED）不当作本命令，以免误解除全部联锁。
    /// </summary>
    public static bool IsClearInhibitCommand(string line)
    {
        var trimmed = line.Trim();
        if (string.Equals(trimmed, "CLEARINHIBIT", StringComparison.OrdinalIgnoreCase))
            return true;
        return trimmed.StartsWith("CLEARINHIBIT,", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>CLEARINHIBIT 或 CLEARINHIBIT,配方名 → 配方名或 null（全部）。</summary>
    public static string? ParseClearInhibitRecipe(string line)
    {
        var trimmed = line.Trim();
        var comma = trimmed.IndexOf(',');
        if (comma < 0)
            return null;
        var recipe = trimmed[(comma + 1)..].Trim();
        var next = recipe.IndexOf(',');
        if (next >= 0)
            recipe = recipe[..next].Trim();
        return recipe.Length == 0 ? null : recipe;
    }

    public static string FormatReply(VisionResult result)
    {
        if (!result.Ok)
        {
            // 内部错误不暴露异常原文（可能含路径/堆栈/中文），固定 ASCII 令牌；详情进日志
            var message = result.ErrorCode == VisionErrorCode.InternalError
                ? "INTERNAL_ERROR"
                : Sanitize(result.Message);
            return $"ERR,{(int)result.ErrorCode},{message}";
        }

        var builder = new StringBuilder("OK");
        foreach (var pose in result.Poses)
        {
            // NaN/Infinity 防御：推理异常产生的非有限值直接转 ERR，避免 PLC 浮点解析失败
            if (!double.IsFinite(pose.X) || !double.IsFinite(pose.Y) || !double.IsFinite(pose.AngleDeg))
                return $"ERR,{(int)VisionErrorCode.InternalError},INVALID_POSE";
            builder.Append(CultureInfo.InvariantCulture, $",{pose.X:0.000},{pose.Y:0.000},{pose.AngleDeg:0.000}");
        }
        builder.Append(',').Append(result.RecipeName)
            .Append(CultureInfo.InvariantCulture, $",{result.Poses.Count},{result.ElapsedMs:0}");
        return builder.ToString();
    }

    /// <summary>错误消息中不允许出现逗号/换行或非 ASCII，避免破坏行协议。</summary>
    private static string Sanitize(string message)
    {
        var cleaned = message.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');
        return new string(cleaned.Where(static c => c < 128).ToArray());
    }

    public void Dispose() => Stop();
}
