using RobotVision.Infrastructure.Communication;

namespace RobotVision.Hosting;

/// <summary>TCP 客户端连接快照（宿主/UI 稳定 DTO）。</summary>
public sealed record TcpClientView(
    long Id, string Remote, DateTime ConnectedAt,
    long Requests, long BytesReceived, long BytesSent,
    string? LastRequest, DateTime? LastRequestAt);

/// <summary>TCP 请求历史行（宿主/UI 稳定 DTO）。</summary>
public sealed record TcpRequestView(
    DateTime Time, long ClientId, string Client,
    string Request, string Reply, bool Ok, double ElapsedMs,
    long BytesIn, long BytesOut);

internal static class TcpViewMapper
{
    public static TcpClientView Map(TcpClientSnapshot snapshot) => new(
        snapshot.Id, snapshot.Remote, snapshot.ConnectedAt,
        snapshot.Requests, snapshot.BytesReceived, snapshot.BytesSent,
        snapshot.LastRequest, snapshot.LastRequestAt);

    public static TcpRequestView Map(TcpRequestRecord record) => new(
        record.Time, record.ClientId, record.Client,
        record.Request, record.Reply, record.Ok, record.ElapsedMs,
        record.BytesIn, record.BytesOut);
}