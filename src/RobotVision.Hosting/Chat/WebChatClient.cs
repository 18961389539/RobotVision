using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RobotVision.Hosting.Chat;

/// <summary>
/// 工艺助手联网：检索公开网页。禁止本机/内网（SSRF），超时与体积封顶。
/// </summary>
public sealed class WebChatClient : IDisposable
{
    public const int MaxSearchResults = 6;
    public const int MaxFetchChars = 8000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Regex ResultLink = new(
        """class="result__a"[^>]*href="(?<href>[^"]+)"[^>]*>(?<title>.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ResultSnippet = new(
        """class="result__snippet"[^>]*>(?<s>.*?)</(?:a|div|span)>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public WebChatClient()
        : this(CreateDefault(), ownsHttp: true)
    {
    }

    internal WebChatClient(HttpClient http, bool ownsHttp)
    {
        _http = http;
        _ownsHttp = ownsHttp;
    }

    private static readonly string[] QuerySchemaRequired = ["query"];
    private static readonly string[] UrlSchemaRequired = ["url"];

    public IReadOnlyList<IChatTool> Tools =>
    [
        new DelegateChatTool(
            "web_search",
            "检索公开互联网（DuckDuckGo）。query 为搜索词。查标准/报错/第三方文档用；站内产量、相机、配方仍用站内工具。",
            Schema(new { type = "object", properties = new { query = new { type = "string" } }, required = QuerySchemaRequired }),
            Search),
        new DelegateChatTool(
            "web_fetch",
            "读取公开 https/http 网页正文。url 必须是公网地址，禁止本机与内网。",
            Schema(new { type = "object", properties = new { url = new { type = "string" } }, required = UrlSchemaRequired }),
            Fetch),
    ];

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }

    internal static bool TryValidatePublicHttpUrl(string? raw, out Uri uri, out string error)
    {
        uri = null!;
        error = "";
        if (string.IsNullOrWhiteSpace(raw) ||
            !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var parsed))
        {
            error = "URL 无效";
            return false;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            error = "只允许 http/https";
            return false;
        }

        var host = parsed.Host;
        if (string.IsNullOrWhiteSpace(host) ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("metadata", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            error = "禁止访问本机或内网";
            return false;
        }

        if (IPAddress.TryParse(host, out var ip) && !IsPublicAddress(ip))
        {
            error = "禁止访问本机或内网";
            return false;
        }

        uri = parsed;
        return true;
    }

    internal static bool IsPublicAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            return false;
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.Broadcast))
            return false;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 10) return false;
            if (b[0] == 127) return false;
            if (b[0] == 0) return false;
            if (b[0] == 169 && b[1] == 254) return false;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;
            if (b[0] == 192 && b[1] == 168) return false;
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 0xFC || b[0] == 0xFD) return false;
        }
        return true;
    }

    internal static string HtmlToPlain(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";
        var text = Regex.Replace(html, "(?is)<(script|style)[^>]*>.*?</\\1>", " ");
        text = Regex.Replace(text, "(?is)<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= MaxFetchChars ? text : text[..MaxFetchChars] + "…";
    }

    internal static string UnwrapDuckLink(string href)
    {
        var raw = href.Trim();
        if (raw.StartsWith("//", StringComparison.Ordinal))
            raw = "https:" + raw;
        var marker = "uddg=";
        var i = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i >= 0)
        {
            var encoded = raw[(i + marker.Length)..];
            var amp = encoded.IndexOf('&', StringComparison.Ordinal);
            if (amp >= 0)
                encoded = encoded[..amp];
            return Uri.UnescapeDataString(encoded);
        }
        return raw;
    }

    internal static List<(string Title, string Url, string Snippet)> ParseSearchHtml(string html)
    {
        var hits = new List<(string, string, string)>();
        var snippets = ResultSnippet.Matches(html)
            .Select(m => HtmlToPlain(m.Groups["s"].Value))
            .ToList();
        var n = 0;
        foreach (Match match in ResultLink.Matches(html))
        {
            var title = HtmlToPlain(match.Groups["title"].Value);
            var url = UnwrapDuckLink(match.Groups["href"].Value);
            if (title.Length == 0 || url.Length == 0)
                continue;
            var snippet = n < snippets.Count ? snippets[n] : "";
            hits.Add((title, url, snippet));
            n++;
            if (hits.Count >= MaxSearchResults)
                break;
        }
        return hits;
    }

    private async Task<ChatToolResult> Search(string args, CancellationToken ct)
    {
        var query = ReadArg(args, "query");
        if (string.IsNullOrWhiteSpace(query))
            return Fail("请提供 query");
        query = query.Trim();
        if (query.Length > 200)
            query = query[..200];

        try
        {
            var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query) + "&kl=cn-zh";
            var html = await GetTextAsync(new Uri(url), ct).ConfigureAwait(false);
            var hits = ParseSearchHtml(html);
            return Ok(new
            {
                ok = true,
                query,
                count = hits.Count,
                results = hits.Select(h => new { title = h.Title, url = h.Url, snippet = h.Snippet }).ToList(),
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail("检索失败: " + ex.Message);
        }
    }

    private async Task<ChatToolResult> Fetch(string args, CancellationToken ct)
    {
        var raw = ReadArg(args, "url");
        if (!TryValidatePublicHttpUrl(raw, out var uri, out var error))
            return Fail(error);
        try
        {
            await EnsurePublicDnsAsync(uri, ct).ConfigureAwait(false);
            var body = await GetTextAsync(uri, ct, follow: 3).ConfigureAwait(false);
            return Ok(new
            {
                ok = true,
                url = uri.ToString(),
                text = HtmlToPlain(body),
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail("读取失败: " + ex.Message);
        }
    }

    private static async Task EnsurePublicDnsAsync(Uri uri, CancellationToken ct)
    {
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            if (!IsPublicAddress(literal))
                throw new InvalidOperationException("禁止访问本机或内网");
            return;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, ct).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("无法解析主机: " + ex.Message);
        }

        if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a)))
            throw new InvalidOperationException("禁止访问本机或内网");
    }

    private async Task<string> GetTextAsync(Uri uri, CancellationToken ct, int follow = 0)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/json,text/plain;q=0.9,*/*;q=0.1");
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if ((int)resp.StatusCode is 301 or 302 or 303 or 307 or 308)
        {
            var loc = resp.Headers.Location;
            if (follow <= 0 || loc is null)
                throw new InvalidOperationException("重定向过多或缺少 Location");
            var next = loc.IsAbsoluteUri ? loc : new Uri(uri, loc);
            if (!TryValidatePublicHttpUrl(next.ToString(), out var nextUri, out var err))
                throw new InvalidOperationException(err);
            await EnsurePublicDnsAsync(nextUri, ct).ConfigureAwait(false);
            return await GetTextAsync(nextUri, ct, follow - 1).ConfigureAwait(false);
        }

        resp.EnsureSuccessStatusCode();
        var media = resp.Content.Headers.ContentType?.MediaType ?? "";
        if (media.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            media.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            media.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
            media.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            media.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("不支持该内容类型: " + media);

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var buffer = new char[MaxFetchChars * 4];
        var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
        return new string(buffer, 0, read);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "SocketsHttpHandler ownership transfers to HttpClient (disposeHandler: true).")]
    private static HttpClient CreateDefault()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        var http = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(12) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RobotVision/1.0");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.5");
        return http;
    }

    private static string ReadArg(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty(name, out var p) ? p.ToString() : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static JsonElement Schema(object o) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(o))!;

    private static ChatToolResult Ok(object o) =>
        new(JsonSerializer.Serialize(o, Json));

    private static ChatToolResult Fail(string error) =>
        new(JsonSerializer.Serialize(new { ok = false, error }, Json));
}
