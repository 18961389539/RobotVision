using System.Net;
using System.Text;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class WebChatClientTests
{
    [Fact]
    public void TryValidate_RejectsLoopbackPrivateAndNonHttp()
    {
        Assert.False(WebChatClient.TryValidatePublicHttpUrl("http://127.0.0.1/", out _, out _));
        Assert.False(WebChatClient.TryValidatePublicHttpUrl("http://localhost/x", out _, out _));
        Assert.False(WebChatClient.TryValidatePublicHttpUrl("http://192.168.1.8/a", out _, out _));
        Assert.False(WebChatClient.TryValidatePublicHttpUrl("http://10.0.0.1/", out _, out _));
        Assert.False(WebChatClient.TryValidatePublicHttpUrl("file:///c:/x", out _, out _));
        Assert.True(WebChatClient.TryValidatePublicHttpUrl("https://example.com/doc", out var uri, out _));
        Assert.Equal("example.com", uri.Host);
    }

    [Fact]
    public void HtmlToPlain_StripsTagsAndCaps()
    {
        var text = WebChatClient.HtmlToPlain("<html><script>bad()</script><p>合格率 &gt; 99%</p></html>");
        Assert.Contains("合格率 > 99%", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bad()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseSearchHtml_ReadsDuckDuckGoResults()
    {
        const string html = """
            <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Flearn.microsoft.com%2Foxyplot">OxyPlot 文档</a>
            <a class="result__snippet">WPF 图表库</a>
            """;
        var hits = WebChatClient.ParseSearchHtml(html);
        Assert.Single(hits);
        Assert.Equal("OxyPlot 文档", hits[0].Title);
        Assert.Equal("https://learn.microsoft.com/oxyplot", hits[0].Url);
        Assert.Contains("图表", hits[0].Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_UsesStubHtml()
    {
        const string html = """
            <a class="result__a" href="https://example.com/a">标题甲</a>
            <a class="result__snippet">摘要甲</a>
            """;
        using var client = Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        });
        var result = await client.Tools[0].InvokeAsync("""{"query":"oxyplot"}""", CancellationToken.None);
        Assert.Contains("标题甲", result.Text, StringComparison.Ordinal);
        Assert.Contains("example.com/a", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetch_RejectsPrivateUrlWithoutRequest()
    {
        using var client = Create(_ => throw new InvalidOperationException("should not fetch"));
        var result = await client.Tools[1].InvokeAsync("""{"url":"http://127.0.0.1/secret"}""", CancellationToken.None);
        Assert.Contains("内网", result.Text, StringComparison.Ordinal);
    }

    private static WebChatClient Create(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new DelegateHandler(handler)) { Timeout = TimeSpan.FromSeconds(5) };
        return new WebChatClient(http, ownsHttp: true);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> inner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(inner(request));
    }
}
