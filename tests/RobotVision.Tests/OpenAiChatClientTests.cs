using System.Net;
using System.Text;
using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class OpenAiChatClientTests
{
    [Fact]
    public async Task Probe_HealthOk_ReturnsTrue()
    {
        using var client = Create(req =>
        {
            Assert.EndsWith("/health", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        Assert.True(await client.ProbeAsync());
    }

    [Fact]
    public async Task CompleteStream_YieldsSseChunks()
    {
        var sse = """
            data: {"choices":[{"delta":{"content":"你"}}]}

            data: {"choices":[{"delta":{"content":"好"}}]}

            data: [DONE]

            """;
        using var client = Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
        });
        var parts = new List<string>();
        await foreach (var chunk in client.CompleteStreamAsync([new ChatTurn("user", "hi")]))
            parts.Add(chunk);
        Assert.Equal(["你", "好"], parts);
    }

    [Fact]
    public async Task CompleteParts_AccumulatesToolCalls()
    {
        var sse = """
            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","function":{"name":"station_overview","arguments":""}}]}}]}

            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{}"}}]}}]}

            data: [DONE]

            """;
        using var client = Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
        });
        IReadOnlyList<ChatToolCall>? calls = null;
        await foreach (var part in client.CompletePartsAsync([new ChatApiMessage("user", "hi")], tools: null))
        {
            if (part.ToolCalls is not null)
                calls = part.ToolCalls;
        }
        Assert.NotNull(calls);
        Assert.Equal("station_overview", calls![0].Name);
        Assert.Equal("{}", calls[0].Arguments);
    }

    [Fact]
    public void NormalizeEndpoint_TrimsSlash()
    {
        Assert.Equal("http://127.0.0.1:8080", OpenAiChatClient.NormalizeEndpoint("http://127.0.0.1:8080/"));
        Assert.Equal("http://127.0.0.1:8080", OpenAiChatClient.NormalizeEndpoint(" "));
    }

    private static OpenAiChatClient Create(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new DelegateHandler(handler)) { Timeout = TimeSpan.FromSeconds(5) };
        return new OpenAiChatClient(new ChatConfig(), http, ownsHttp: true);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> inner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(inner(request));
    }
}
