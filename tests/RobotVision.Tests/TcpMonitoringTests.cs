using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Communication;
using Xunit;

namespace RobotVision.Tests;

public class TcpMonitoringTests : IDisposable
{
    private readonly TcpServerManager _tcp;

    public TcpMonitoringTests()
    {
        _tcp = new TcpServerManager(
            "127.0.0.1", GetFreePort(), 2000,
            (recipe, _, ct) => Task.FromResult(VisionResult.Success(recipe,
                [new RobotPose(1.5, 2.5, 30)], 4)),
            NullLogger<TcpServerManager>.Instance);
    }

    public void Dispose() => _tcp.Dispose();

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task<string> RoundTripAsync(NetworkStream stream, string request)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(request + "\n"));
        var buffer = new byte[4096];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var read = await stream.ReadAsync(buffer, timeout.Token);
        return Encoding.UTF8.GetString(buffer, 0, read).Trim();
    }

    [Fact]
    public void BeforeStart_NoClientsAndNotRunning()
    {
        Assert.Equal(0, _tcp.ConnectedClients);
        Assert.Empty(_tcp.GetClients());
        Assert.False(_tcp.IsRunning);
        Assert.Equal(0, _tcp.TotalConnections);
        Assert.Equal(0, _tcp.TotalRequests);
    }

    [Fact]
    public void DisconnectClient_UnknownId_ReturnsFalse()
    {
        Assert.False(_tcp.DisconnectClient(42));
    }

    [Fact]
    public async Task ClientLifecycle_RaisesEventsAndTracksRequests()
    {
        var connected = new TaskCompletionSource<TcpClientSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnected = new TaskCompletionSource<TcpClientSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequest = new TaskCompletionSource<TcpRequestRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        _tcp.ClientConnected += s => connected.TrySetResult(s);
        _tcp.ClientDisconnected += s => disconnected.TrySetResult(s);
        _tcp.RequestProcessed += r => firstRequest.TrySetResult(r);
        _tcp.Start();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        var session = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, _tcp.ConnectedClients);
        Assert.Contains("127.0.0.1", session.Remote);
        var portPart = session.Remote[(session.Remote.LastIndexOf(':') + 1)..];
        Assert.True(int.TryParse(portPart, out var remotePort) && remotePort > 0, $"远端应含端口: {session.Remote}");

        var stream = client.GetStream();
        Assert.Equal("PONG", await RoundTripAsync(stream, "PING"));
        var ping = await firstRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("PING", ping.Request);
        Assert.Equal("PONG", ping.Reply);
        Assert.True(ping.Ok);
        Assert.Equal(session.Id, ping.ClientId);
        Assert.Equal(session.Remote, ping.Client);

        Assert.StartsWith("OK,A01,1,", await RoundTripAsync(stream, "TRIGGER,A01"));

        var stats = Assert.Single(_tcp.GetClients());
        Assert.Equal(2, stats.Requests);
        Assert.Equal(1, _tcp.TotalConnections);
        Assert.Equal(2, _tcp.TotalRequests);
        Assert.Equal("TRIGGER,A01", stats.LastRequest);
        Assert.NotNull(stats.LastRequestAt);
        Assert.True(stats.BytesReceived > 0);
        Assert.True(stats.BytesSent > stats.BytesReceived);

        client.Close();
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, _tcp.ConnectedClients);
        Assert.Empty(_tcp.GetClients());
    }

    [Fact]
    public async Task DisconnectClient_ClosesConnectionAndRaisesEvent()
    {
        var connected = new TaskCompletionSource<TcpClientSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnected = new TaskCompletionSource<TcpClientSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _tcp.ClientConnected += s => connected.TrySetResult(s);
        _tcp.ClientDisconnected += s => disconnected.TrySetResult(s);
        _tcp.Start();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        var session = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(_tcp.DisconnectClient(session.Id));
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(_tcp.DisconnectClient(session.Id));
        Assert.Equal(0, _tcp.ConnectedClients);

        var buffer = new byte[16];
        var read = await client.GetStream().ReadAsync(buffer);
        Assert.Equal(0, read);
    }

    [Fact]
    public async Task StopAndStart_ServiceCanBeRestarted()
    {
        _tcp.Start();
        Assert.True(_tcp.IsRunning);
        _tcp.Stop();
        Assert.False(_tcp.IsRunning);
        Assert.Equal(0, _tcp.ConnectedClients);

        _tcp.Start();
        Assert.True(_tcp.IsRunning);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        var reply = await RoundTripAsync(client.GetStream(), "PING");
        Assert.Equal("PONG", reply);
    }

    [Fact]
    public async Task IdleTimeoutZero_KeepsConnectionUntilClientCloses()
    {
        _tcp.IdleTimeoutMs = 0;
        _tcp.Start();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        var stream = client.GetStream();
        Assert.Equal("PONG", await RoundTripAsync(stream, "PING"));
        await Task.Delay(400);
        Assert.Equal(1, _tcp.ConnectedClients);
        Assert.Equal("PONG", await RoundTripAsync(stream, "PING"));
    }

    [Fact]
    public async Task IdleTimeoutPositive_DisconnectsAfterSilence()
    {
        _tcp.IdleTimeoutMs = 250;
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _tcp.ClientDisconnected += _ => disconnected.TrySetResult();
        _tcp.Start();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        Assert.Equal("PONG", await RoundTripAsync(client.GetStream(), "PING"));
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, _tcp.ConnectedClients);
    }
}
