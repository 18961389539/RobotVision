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
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request + "\n"));
        return await ReadReplyAsync(stream);
    }

    private static async Task<string> RoundTripRawAsync(NetworkStream stream, string request)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        return await ReadReplyAsync(stream);
    }

    private static async Task<string> ReadReplyAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var read = await stream.ReadAsync(buffer, timeout.Token);
        return Encoding.ASCII.GetString(buffer, 0, read).Trim();
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

        Assert.Equal("OK,1.500,2.500,30.000,A01,1,4", await RoundTripAsync(stream, "A01"));

        var stats = Assert.Single(_tcp.GetClients());
        Assert.Equal(2, stats.Requests);
        Assert.Equal(1, _tcp.TotalConnections);
        Assert.Equal(2, _tcp.TotalRequests);
        Assert.Equal("A01", stats.LastRequest);
        Assert.NotNull(stats.LastRequestAt);
        Assert.True(stats.BytesReceived > 0);
        Assert.True(stats.BytesSent > stats.BytesReceived);

        client.Close();
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, _tcp.ConnectedClients);
        Assert.Empty(_tcp.GetClients());
    }

    [Fact]
    public async Task RequestHistory_RetainedWithoutSubscriber()
    {
        _tcp.Start();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        Assert.Equal("PONG", await RoundTripAsync(client.GetStream(), "PING"));
        Assert.Equal("OK,1.500,2.500,30.000,A01,1,4", await RoundTripAsync(client.GetStream(), "A01"));

        var history = _tcp.GetRecentRequests();
        Assert.Equal(2, history.Count);
        Assert.Equal("A01", history[0].Request);
        Assert.Equal("PING", history[1].Request);
        Assert.Equal("PONG", history[1].Reply);
    }

    [Fact]
    public async Task RequestStarted_FiresBeforeHandlerCompletes()
    {
        using var allowComplete = new ManualResetEventSlim(false);
        using var tcp = new TcpServerManager(
            "127.0.0.1", GetFreePort(), 5000,
            (_, _, ct) =>
            {
                allowComplete.Wait(ct);
                return Task.FromResult(VisionResult.Success("A01", [new RobotPose(1, 2, 3)], 4));
            },
            NullLogger<TcpServerManager>.Instance);

        var started = new TaskCompletionSource<TcpRequestRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcp.RequestStarted += r => started.TrySetResult(r);
        tcp.Start();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, tcp.Port);
            await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("A01\n"));

            var record = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("A01", record.Request);
            Assert.Equal("", record.Reply);
            Assert.Empty(tcp.GetRecentRequests());

            allowComplete.Set();
            var buffer = new byte[4096];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var read = await client.GetStream().ReadAsync(buffer, timeout.Token);
            Assert.StartsWith("OK,", Encoding.ASCII.GetString(buffer, 0, read));
            Assert.Single(tcp.GetRecentRequests());
        }
        finally
        {
            allowComplete.Set();
            tcp.Dispose();
        }
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
    public async Task Ping_WithoutNewline_StillReplies()
    {
        _tcp.Start();
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        Assert.Equal("PONG", await RoundTripRawAsync(client.GetStream(), "PING"));
    }

    [Fact]
    public async Task Ping_WithCrLf_StillReplies()
    {
        _tcp.Start();
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        Assert.Equal("PONG", await RoundTripRawAsync(client.GetStream(), "PING\r\n"));
    }

    [Fact]
    public async Task TwoPings_WithoutNewline_SameConnection()
    {
        _tcp.Start();
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        var stream = client.GetStream();
        Assert.Equal("PONG", await RoundTripRawAsync(stream, "PING"));
        Assert.Equal("PONG", await RoundTripRawAsync(stream, "PING"));
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
    public async Task Handler_DoesNotRunOnCallerSynchronizationContext()
    {
        var ctx = new TrackingSyncContext();
        var handlerSawCallerContext = 0;
        using var tcp = new TcpServerManager(
            "127.0.0.1", GetFreePort(), 2000,
            (recipe, _, _) =>
            {
                if (SynchronizationContext.Current == ctx)
                    Interlocked.Increment(ref handlerSawCallerContext);
                return Task.FromResult(VisionResult.Success(recipe, [new RobotPose(1, 2, 3)], 1));
            },
            NullLogger<TcpServerManager>.Instance);

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(ctx);
        try
        {
            tcp.Start();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, tcp.Port);
            var reply = await RoundTripAsync(client.GetStream(), "A01");
            Assert.StartsWith("OK,", reply);
            Assert.Equal(0, handlerSawCallerContext);
        }
        finally
        {
            tcp.Stop();
        }
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

    private sealed class TrackingSyncContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            var thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                d(state);
            })
            {
                IsBackground = true,
            };
            thread.Start();
        }
    }
}
