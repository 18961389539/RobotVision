using System.Net;
using System.Net.Sockets;
using System.Text;
using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class ChatUiHostTests
{
    [Fact]
    public async Task ChatSse_RequiresToken_AndStreamsText()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv-chatui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var app = new AppConfig { DataRoot = root };
        app.Chat.UiPort = FreePort();
        app.Chat.AuditEnabled = false;
        app.Chat.AuditFolder = Path.Combine(root, "audit");
        app.Chat.CaptureFolder = Path.Combine(root, "captures");
        var client = new StubChatClient { ProbeResult = true, Chunks = ["Hello", " world"] };
        var agent = new ChatAgent(
            client,
            new ChatToolRegistry([], new ChatToolAuditStore(app), app.Chat),
            app.Chat);
        await using var host = new HostDisposable(new ChatUiHost(app, agent, client, staticRoot: root));
        await host.Inner.StartAsync(CancellationToken.None);
        Assert.True(host.Inner.IsListening);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var denied = await http.PostAsync(
            host.Inner.Origin + "/v1/chat",
            new StringContent("""{"messages":[{"role":"user","content":"hi"}]}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var req = new HttpRequestMessage(HttpMethod.Post, host.Inner.Origin + "/v1/chat")
        {
            Content = new StringContent("""{"messages":[{"role":"user","content":"hi"}]}""", Encoding.UTF8, "application/json"),
        };
        req.Headers.Add(ChatUiHost.TokenHeader, host.Inner.Token);
        using var ok = await http.SendAsync(req);
        ok.EnsureSuccessStatusCode();
        var body = await ok.Content.ReadAsStringAsync();
        Assert.Contains("Hello", body, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"done\"", body, StringComparison.Ordinal);

        using var healthReq = new HttpRequestMessage(HttpMethod.Get, host.Inner.Origin + "/v1/health");
        healthReq.Headers.Add(ChatUiHost.TokenHeader, host.Inner.Token);
        using var health = await http.SendAsync(healthReq);
        var healthJson = await health.Content.ReadAsStringAsync();
        Assert.Contains("\"ready\":true", healthJson, StringComparison.Ordinal);

        await host.Inner.StopAsync(CancellationToken.None);
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void IsAllowedImagePath_RejectsOutsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv-chatui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var app = new AppConfig { DataRoot = root };
        app.Chat.AuditEnabled = false;
        app.Chat.AuditFolder = Path.Combine(root, "audit");
        app.Chat.CaptureFolder = Path.Combine(root, "captures");
        Directory.CreateDirectory(app.Chat.CaptureFolder);
        var inside = Path.Combine(app.Chat.CaptureFolder, "a.png");
        File.WriteAllBytes(inside, [1, 2, 3]);
        var client = new StubChatClient();
        var agent = new ChatAgent(
            client,
            new ChatToolRegistry([], new ChatToolAuditStore(app), app.Chat),
            app.Chat);
        using var host = new ChatUiHost(app, agent, client, staticRoot: root);
        Assert.True(host.IsAllowedImagePath(inside));
        Assert.False(host.IsAllowedImagePath(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid() + ".png")));
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class HostDisposable(ChatUiHost inner) : IAsyncDisposable
    {
        public ChatUiHost Inner { get; } = inner;

        public async ValueTask DisposeAsync()
        {
            try { await Inner.StopAsync(CancellationToken.None); } catch (Exception) { }
            Inner.Dispose();
        }
    }

    private sealed class StubChatClient : ILocalChatClient
    {
        public bool ProbeResult { get; set; }
        public string? LastError { get; set; }
        public IReadOnlyList<string> Chunks { get; set; } = [];

        public Task<bool> ProbeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ProbeResult);

        public async IAsyncEnumerable<string> CompleteStreamAsync(
            IReadOnlyList<ChatTurn> turns,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = turns;
            foreach (var chunk in Chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<ChatStreamPart> CompletePartsAsync(
            IReadOnlyList<ChatApiMessage> messages,
            IReadOnlyList<ChatToolSpec>? tools,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = messages;
            _ = tools;
            foreach (var chunk in Chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatStreamPart(chunk, null, false);
                await Task.Yield();
            }
        }
    }
}
