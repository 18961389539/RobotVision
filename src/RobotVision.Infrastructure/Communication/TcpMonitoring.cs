namespace RobotVision.Infrastructure.Communication;

/// <summary>
/// 单个客户端连接的实时统计。由套接字线程更新，UI 通过事件与快照读取。
/// 计数器用 Interlocked/Volatile 保证跨线程可见性。
/// </summary>
public sealed class TcpSession
{
    private long _requests;
    private long _bytesIn;
    private long _bytesOut;
    private string? _lastRequest;
    private long _lastRequestTicks;

    public long Id { get; init; }
    public string Remote { get; init; } = "";
    public DateTime ConnectedAt { get; init; }

    /// <summary>手动断开标记：随后的关闭异常按信息级记录，避免误报连接异常。</summary>
    public volatile bool ManualClose;

    public long Requests => Volatile.Read(ref _requests);
    public long BytesReceived => Volatile.Read(ref _bytesIn);
    public long BytesSent => Volatile.Read(ref _bytesOut);
    public string? LastRequest => Volatile.Read(ref _lastRequest);

    public DateTime? LastRequestAt
    {
        get
        {
            var ticks = Volatile.Read(ref _lastRequestTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Local);
        }
    }

    /// <summary>行已读到、处理尚未完成：先刷新「最近请求」，便于监控页在取图/推理期间也能看到。</summary>
    internal void NoteIncoming(string request)
    {
        Volatile.Write(ref _lastRequest, request);
        Interlocked.Exchange(ref _lastRequestTicks, DateTime.Now.Ticks);
    }

    internal void RecordRequest(string request, long bytesIn, long bytesOut)
    {
        Interlocked.Increment(ref _requests);
        Interlocked.Add(ref _bytesIn, bytesIn);
        Interlocked.Add(ref _bytesOut, bytesOut);
        Volatile.Write(ref _lastRequest, request);
        Interlocked.Exchange(ref _lastRequestTicks, DateTime.Now.Ticks);
    }

    public TcpClientSnapshot Snapshot() => new(
        Id, Remote, ConnectedAt, Requests, BytesReceived, BytesSent, LastRequest, LastRequestAt);
}

/// <summary>连接的不可变快照，供 UI 展示与事件回调用。</summary>
public sealed record TcpClientSnapshot(
    long Id, string Remote, DateTime ConnectedAt,
    long Requests, long BytesReceived, long BytesSent,
    string? LastRequest, DateTime? LastRequestAt);

/// <summary>一次请求的完整记录（请求历史行）。</summary>
public sealed record TcpRequestRecord(
    DateTime Time, long ClientId, string Client,
    string Request, string Reply, bool Ok, double ElapsedMs,
    long BytesIn, long BytesOut);
