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
/// 协议（UTF-8 编码的 ASCII 子集，\n 结尾）：
///   请求: TRIGGER,配方名     应答: OK,配方名,目标数,x,y,角度[,x2,y2,角度2...],耗时ms
///   请求: PING               应答: PONG
///   请求: STATUS             应答: OK,ready|busy,队列深度,队列上限,最近耗时ms（状态查询）
///   出错: ERR,错误码,消息
/// 错误消息契约：业务错误保留 Sanitize 后的可读消息；InternalError 固定为
/// INTERNAL_ERROR（详情只进日志）；未知命令/缺参数用固定 ASCII 模板。
/// 目标数字段让 PLC 无需先数尾部字段即可解析位姿三元组，
/// 也为将来"0 目标返回空 OK（count=0）"预留了非破坏性扩展（当前 0 目标仍返回 ERR 1007）。
/// StreamReader 按行读取天然解决粘包/半包问题。
/// </summary>
public sealed class TcpServerManager : IDisposable
{
    /// <summary>STATUS 命令状态快照（由组装层从 VisionService 注入）。</summary>
    public sealed record TcpServerState(bool Ready, int QueueDepth, int MaxQueueDepth, double LastElapsedMs);

    private IPAddress _address;
    private int _port;
    private readonly Func<string, CancellationToken, Task<VisionResult>> _handler;
    private readonly ILogger<TcpServerManager> _log;
    private readonly ConcurrentDictionary<long, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<long, TcpSession> _sessions = new();
    private long _totalConnections;
    private long _totalRequests;
    private long _rejectedConnections;

    /// <summary>启停/热重启互斥锁：Start/Stop/Restart 串行化，防止并发启停把监听状态弄乱。</summary>
    private readonly object _lifecycleLock = new();

    /// <summary>读侧空闲超时下限（ms）：避免 UI 把 TimeoutMs 调得过小时误断正常的慢客户端。</summary>
    private const int MinIdleTimeoutMs = 5000;

    private volatile TcpListener? _listener;
    private volatile CancellationTokenSource? _cts;
    private long _nextClientId;

    public TcpServerManager(
        string ipAddress,
        int port,
        int timeoutMs,
        Func<string, CancellationToken, Task<VisionResult>> handler,
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
    /// volatile 保证套接字线程能即时读到 UI 改动；≤0 按 1ms clamp（CancelAfter(0) 会立即超时）。</summary>
    private volatile int _timeoutMs;

    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = Math.Max(1, value);
    }

    /// <summary>监听 backlog（内核排队待 accept 的连接数）。</summary>
    public int Backlog { get; set; } = 16;

    /// <summary>
    /// STATUS 命令状态提供者（组装层注入 VisionService 的实时状态）。
    /// 未注入时 STATUS 返回 OK,ready,0,0,0。
    /// </summary>
    public Func<TcpServerState>? StateProvider { get; set; }

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

    /// <summary>请求处理完成（套接字线程回调）。</summary>
    public event Action<TcpRequestRecord>? RequestProcessed;

    /// <summary>当前连接快照（按接入顺序排列）。</summary>
    public IReadOnlyList<TcpClientSnapshot> GetClients() =>
        _sessions.Values.Select(s => s.Snapshot()).OrderBy(s => s.Id).ToList();

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
        _ = AcceptLoopAsync(listener, _cts.Token);
        _log.LogInformation("TCP 服务已启动: {Address}:{Port}", _address, _port);
    }

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
                    client = await listener.AcceptTcpClientAsync(ct);
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
                _ = HandleClientAsync(id, session, client, ct);
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
            if (!ct.IsCancellationRequested && _listener == listener)
            {
                try
                {
                    Start();
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
                var keepAlive = new byte[12];
                BitConverter.GetBytes(1).CopyTo(keepAlive, 0);      // 开启
                BitConverter.GetBytes(5000).CopyTo(keepAlive, 4);   // 首次探测延迟（ms）
                BitConverter.GetBytes(2000).CopyTo(keepAlive, 8);   // 探测间隔（ms）
                client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
            }
            catch
            {
                // 平台不支持 IOControl 时仅保留 SocketOption 级别探测
            }

            var stream = client.GetStream();
            // 读取侧用 UTF8（容忍客户端 BOM）；写出侧必须无 BOM——行协议是纯 ASCII，
            // StreamWriter 配 Encoding.UTF8 会在首个应答前插入 3 字节 BOM，破坏严格解析器
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };

            while (!ct.IsCancellationRequested)
            {
                string? line;
                using (var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    // 读侧空闲超时：死连接（客户端挂起、不发送也不关闭）不再无限占用连接名额。
                    // 取 TimeoutMs 与固定下限的较大者，避免 UI 把超时调得过小时误断正常慢客户端
                    var idleMs = Math.Max(TimeoutMs, MinIdleTimeoutMs);
                    idleCts.CancelAfter(idleMs);
                    try
                    {
                        line = await reader.ReadLineAsync(idleCts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // 空闲超时（非服务停止的取消）：断开该客户端并记日志
                        _log.LogWarning("客户端 #{Id} 读空闲超时（{Timeout}ms 无数据），断开", id, idleMs);
                        break;
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }

                if (line is null)
                    break;

                line = line.Trim();
                if (line.Length == 0)
                    continue;

                var stopwatch = Stopwatch.StartNew();
                var reply = await ProcessRequestAsync(line, ct);
                stopwatch.Stop();

                // 统计与事件在写出应答前完成：客户端收到应答时记录必然已可见
                var bytesIn = Encoding.UTF8.GetByteCount(line) + 1;
                var bytesOut = Encoding.UTF8.GetByteCount(reply) + 1;
                session.RecordRequest(line, bytesIn, bytesOut);
                Interlocked.Increment(ref _totalRequests);
                RaiseRequestProcessed(new TcpRequestRecord(
                    DateTime.Now, session.Id, session.Remote, line, reply,
                    !reply.StartsWith("ERR", StringComparison.Ordinal),
                    stopwatch.Elapsed.TotalMilliseconds, bytesIn, bytesOut));

                // 写侧超时：客户端不读（TCP 窗口满）时不无限挂起，超时断开释放连接
                using (var writeCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    writeCts.CancelAfter(TimeoutMs);
                    await writer.WriteLineAsync(reply.AsMemory(), writeCts.Token);
                }
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

    private async Task<string> ProcessRequestAsync(string line, CancellationToken ct)
    {
        if (string.Equals(line, "PING", StringComparison.OrdinalIgnoreCase))
            return "PONG";

        if (string.Equals(line, "STATUS", StringComparison.OrdinalIgnoreCase))
            return FormatStatus(StateProvider?.Invoke());

        var comma = line.IndexOf(',');
        var command = comma < 0 ? line : line[..comma];
        var argument = comma < 0 ? "" : line[(comma + 1)..].Trim();

        if (!string.Equals(command, "TRIGGER", StringComparison.OrdinalIgnoreCase))
            return $"ERR,{(int)VisionErrorCode.UnknownCommand},UNKNOWN_COMMAND";

        if (argument.Length == 0)
            return $"ERR,{(int)VisionErrorCode.UnknownRecipe},MISSING_RECIPE";

        // 超时只保证尽快回复错误；正在执行的推理不会中断（串行管线由信号量保护）
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeoutMs);

        var stopwatch = Stopwatch.StartNew();
        Task<VisionResult>? work = null;
        try
        {
            work = _handler(argument, timeoutCts.Token);
            var result = await work.WaitAsync(timeoutCts.Token);

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
                return FormatReply(await work);
            return FormatReply(VisionResult.Fail(
                argument, VisionErrorCode.Timeout, $"处理超时 >{TimeoutMs}ms", stopwatch.Elapsed.TotalMilliseconds));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "处理请求异常: {Line}", line);
            return FormatReply(VisionResult.Fail(
                argument, VisionErrorCode.InternalError, ex.Message, stopwatch.Elapsed.TotalMilliseconds));
        }
    }

    /// <summary>STATUS 应答格式化（纯函数，可单测）：OK,ready|busy,队列深度,队列上限,最近耗时ms。</summary>
    public static string FormatStatus(TcpServerState? state)
    {
        if (state is null)
            return "OK,ready,0,0,0";
        return $"OK,{(state.Ready ? "ready" : "busy")},{state.QueueDepth},{state.MaxQueueDepth},{state.LastElapsedMs:0}";
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

        var builder = new StringBuilder("OK,").Append(result.RecipeName)
            .Append(CultureInfo.InvariantCulture, $",{result.Poses.Count}");
        foreach (var pose in result.Poses)
        {
            // NaN/Infinity 防御：推理异常产生的非有限值直接转 ERR，避免 PLC 浮点解析失败
            if (!double.IsFinite(pose.X) || !double.IsFinite(pose.Y) || !double.IsFinite(pose.AngleDeg))
                return $"ERR,{(int)VisionErrorCode.InternalError},INVALID_POSE";
            builder.Append(CultureInfo.InvariantCulture, $",{pose.X:0.000},{pose.Y:0.000},{pose.AngleDeg:0.000}");
        }
        builder.Append(CultureInfo.InvariantCulture, $",{result.ElapsedMs:0}");
        return builder.ToString();
    }

    /// <summary>错误消息中不允许出现逗号/换行，避免破坏 CSV 结构。</summary>
    private static string Sanitize(string message) =>
        message.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');

    public void Dispose() => Stop();
}
