using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting.Chat;

/// <summary>
/// 本机回环 HTTP：给 WebView2 里的 assistant-ui 提供静态页、健康检查与对话 SSE。
/// 不对外网开放；请求须带启动时生成的 token。llama-server 仍只由 C# 调用。
/// </summary>
public sealed class ChatUiHost : IHostedService, IDisposable
{
    public const string TokenHeader = "X-RobotVision-Token";
    public const string DefaultStaticFolderName = "chat-ui";

    private readonly AppConfig _app;
    private readonly ChatConfig _cfg;
    private readonly ChatAgent _agent;
    private readonly ILocalChatClient _client;
    private readonly ILogger<ChatUiHost>? _log;
    private readonly string _staticRoot;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _chatGate = new(1, 1);
    private CancellationTokenSource? _chatCts;
    private readonly object _startGate = new();
    private Task? _loop;

    public ChatUiHost(
        AppConfig app,
        ChatAgent agent,
        ILocalChatClient client,
        ILogger<ChatUiHost>? log = null,
        string? staticRoot = null)
    {
        _app = app;
        _cfg = app.Chat;
        _agent = agent;
        _client = client;
        _log = log;
        _staticRoot = staticRoot ?? Path.Combine(AppContext.BaseDirectory, DefaultStaticFolderName);
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    public string Token { get; }

    public int Port { get; private set; }

    public string Origin => Port > 0 ? $"http://127.0.0.1:{Port}" : "";

    public string StartUrl => Port > 0 ? $"{Origin}/?t={Token}" : "";

    public bool IsListening => _listener.IsListening;

    public string? LastError { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_startGate)
        {
            if (_listener.IsListening)
                return Task.CompletedTask;

            LastError = null;
            var preferred = _cfg.UiPort is > 0 and <= 65535 ? _cfg.UiPort : 18080;
            Exception? last = null;
            for (var i = 0; i < 24; i++)
            {
                var port = i == 0 ? preferred : (i < 20 ? preferred + i : FindFreePort());
                try
                {
                    _listener.Prefixes.Clear();
                    _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    _listener.Start();
                    Port = port;
                    _loop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
                    if (_log is { } log)
                        ChatUiHostLog.Started(log, Origin);
                    return Task.CompletedTask;
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                {
                    last = ex;
                    try { _listener.Prefixes.Clear(); } catch { /* retry */ }
                }
            }

            LastError = last?.Message ?? "无法绑定本机工艺助手端口。";
            if (_log is { } warn)
                ChatUiHostLog.Warning(warn, LastError);
            return Task.CompletedTask;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        try { _chatCts?.Cancel(); } catch { /* ignore */ }
        try { _listener.Stop(); } catch { /* ignore */ }
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false); }
            catch (Exception) { /* 退出阶段尽力而为 */ }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Close(); } catch { /* ignore */ }
        _cts.Dispose();
        _chatGate.Dispose();
        _chatCts?.Dispose();
    }

    internal bool IsAllowedImagePath(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception) { return false; }
        if (!File.Exists(full))
            return false;

        foreach (var root in ImageRoots())
        {
            if (IsUnder(root, full))
                return true;
        }

        return false;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken hostCt)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            response.Headers["Cache-Control"] = "no-store";
            var path = request.Url?.AbsolutePath ?? "/";
            if (path.Length == 0)
                path = "/";

            if (!IsAuthorized(request, path))
            {
                await WriteTextAsync(response, 401, "unauthorized").ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && (path == "/v1/health" || path == "/health"))
            {
                await WriteHealthAsync(response, hostCt).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/v1/image")
            {
                await WriteImageAsync(request, response).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/v1/chat")
            {
                await WriteChatSseAsync(request, response, hostCt).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET")
            {
                await WriteStaticAsync(response, path).ConfigureAwait(false);
                return;
            }

            await WriteTextAsync(response, 404, "not found").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_log is { } log)
                ChatUiHostLog.Warning(log, ex.Message);
            try { context.Response.Abort(); } catch { /* ignore */ }
        }
    }

    private bool IsAuthorized(HttpListenerRequest request, string path)
    {
        var header = request.Headers[TokenHeader];
        if (string.Equals(header, Token, StringComparison.Ordinal))
            return true;
        var query = request.QueryString["t"];
        if (string.Equals(query, Token, StringComparison.Ordinal))
            return true;
        // 静态资源允许无 token：页面已在回环且首次导航用 ?t=；API 必须带 token。
        return request.HttpMethod == "GET" && !path.StartsWith("/v1/", StringComparison.Ordinal);
    }

    private async Task WriteHealthAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        var ready = false;
        try { ready = await _client.ProbeAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception) { ready = false; }
        var status = ready
            ? "本机模型已就绪。"
            : (_client.LastError ?? LastError ?? "未检测到 llama-server。");
        var json = $"{{\"ready\":{(ready ? "true" : "false")},\"status\":{JsonString(status)},\"model\":{JsonString(_cfg.Model)}}}";
        await WriteBytesAsync(response, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json))
            .ConfigureAwait(false);
    }

    private async Task WriteImageAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var raw = request.QueryString["path"];
        if (string.IsNullOrWhiteSpace(raw) || !IsAllowedImagePath(raw))
        {
            await WriteTextAsync(response, 404, "not found").ConfigureAwait(false);
            return;
        }

        var full = Path.GetFullPath(raw);
        var ext = Path.GetExtension(full);
        var mime = ext.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
        var bytes = await File.ReadAllBytesAsync(full).ConfigureAwait(false);
        await WriteBytesAsync(response, 200, mime, bytes).ConfigureAwait(false);
    }

    private async Task WriteChatSseAsync(
        HttpListenerRequest request,
        HttpListenerResponse response,
        CancellationToken hostCt)
    {
        string body;
        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            body = await reader.ReadToEndAsync(hostCt).ConfigureAwait(false);
        if (body.Length > 1_000_000)
        {
            await WriteTextAsync(response, 413, "payload too large").ConfigureAwait(false);
            return;
        }

        IReadOnlyList<ChatTurn> turns;
        try { turns = ChatUiMessages.ParseTurns(body); }
        catch (JsonException)
        {
            await WriteTextAsync(response, 400, "invalid json").ConfigureAwait(false);
            return;
        }

        response.StatusCode = 200;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.SendChunked = true;
        response.Headers["Cache-Control"] = "no-cache";
        var output = response.OutputStream;

        await _chatGate.WaitAsync(hostCt).ConfigureAwait(false);
        CancellationTokenSource runCts;
        try
        {
            _chatCts?.Cancel();
            _chatCts?.Dispose();
            runCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
            _chatCts = runCts;
        }
        finally
        {
            _chatGate.Release();
        }

        var token = runCts.Token;
        try
        {
            await foreach (var ev in _agent.RunAsync(turns, token).ConfigureAwait(false))
            {
                var sse = ev switch
                {
                    ChatTextDelta delta => ChatUiMessages.ToSse("text", text: delta.Text),
                    ChatToolNotice notice => ChatUiMessages.ToSse("tool", name: notice.Name, detail: notice.Detail),
                    ChatImageEvent image => ChatUiMessages.ToSse(
                        "image",
                        url: "/v1/image?path=" + Uri.EscapeDataString(image.Path) + "&t=" + Token,
                        text: image.Path),
                    _ => null,
                };
                if (sse is null)
                    continue;
                await WriteSseAsync(output, sse, token).ConfigureAwait(false);
            }

            await WriteSseAsync(output, ChatUiMessages.ToSse("done"), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await WriteSseAsync(output, ChatUiMessages.ToSse("error", message: "已停止"), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteSseAsync(output, ChatUiMessages.ToSse("error", message: ex.Message), CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            try { response.Close(); } catch { /* ignore */ }
        }
    }

    private async Task WriteStaticAsync(HttpListenerResponse response, string path)
    {
        if (path == "/" || path.Length == 0)
            path = "/index.html";
        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (relative.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            await WriteTextAsync(response, 404, "not found").ConfigureAwait(false);
            return;
        }

        var full = Path.GetFullPath(Path.Combine(_staticRoot, relative));
        if (!IsUnder(_staticRoot, full) || !File.Exists(full))
        {
            if (string.Equals(Path.GetFileName(full), "index.html", StringComparison.OrdinalIgnoreCase))
            {
                await WriteBytesAsync(response, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(FallbackHtml))
                    .ConfigureAwait(false);
                return;
            }

            await WriteTextAsync(response, 404, "not found").ConfigureAwait(false);
            return;
        }

        var mime = Mime(full);
        var bytes = await File.ReadAllBytesAsync(full).ConfigureAwait(false);
        await WriteBytesAsync(response, 200, mime, bytes).ConfigureAwait(false);
    }

    private IEnumerable<string> ImageRoots()
    {
        if (!string.IsNullOrWhiteSpace(_app.DataRoot))
            yield return Path.GetFullPath(_app.DataRoot);
        yield return Path.GetFullPath(_app.ResolveChatCapturesFolder());
        var tmp = Path.GetTempPath();
        if (!string.IsNullOrEmpty(tmp))
            yield return Path.GetFullPath(tmp);
    }

    private static bool IsUnder(string root, string full)
    {
        try
        {
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task WriteSseAsync(Stream output, string sse, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(sse);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Task WriteTextAsync(HttpListenerResponse response, int status, string text) =>
        WriteBytesAsync(response, status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text));

    private static async Task WriteBytesAsync(HttpListenerResponse response, int status, string contentType, byte[] bytes)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static string JsonString(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".woff2" => "font/woff2",
        ".map" => "application/json",
        _ => "application/octet-stream",
    };

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    internal const string FallbackHtml =
        """
        <!doctype html>
        <html lang="zh-CN">
        <head>
          <meta charset="utf-8"/>
          <title>工艺助手</title>
          <style>
            body{margin:0;background:#1c1c1c;color:#f3f3f3;font:14px/1.5 sans-serif}
            #log{padding:16px;white-space:pre-wrap}
            form{display:flex;gap:8px;padding:12px;border-top:1px solid #333}
            input{flex:1;padding:8px;background:#2b2b2b;color:#fff;border:1px solid #444;border-radius:6px}
            button{padding:8px 14px;background:#2F81CB;color:#fff;border:0;border-radius:6px}
          </style>
        </head>
        <body>
          <div id="log">前端未打包。对话仍走本机 ChatAgent。</div>
          <form id="f"><input id="q" placeholder="例如：今日合格率"/><button>发</button></form>
          <script>
            const t = new URLSearchParams(location.search).get('t') || '';
            const headers = t ? { 'X-RobotVision-Token': t, 'Content-Type': 'application/json' } : { 'Content-Type': 'application/json' };
            document.getElementById('f').onsubmit = async (e) => {
              e.preventDefault();
              const text = document.getElementById('q').value.trim();
              if (!text) return;
              document.getElementById('q').value = '';
              const log = document.getElementById('log');
              log.textContent += '\n你: ' + text + '\n助手: ';
              const res = await fetch('/v1/chat', { method: 'POST', headers, body: JSON.stringify({ messages: [{ role: 'user', content: text }] }) });
              const reader = res.body.getReader();
              const dec = new TextDecoder();
              let buf = '';
              while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                buf += dec.decode(value, { stream: true });
                const parts = buf.split('\n\n');
                buf = parts.pop() || '';
                for (const p of parts) {
                  if (!p.startsWith('data: ')) continue;
                  const ev = JSON.parse(p.slice(6));
                  if (ev.type === 'text') log.textContent += ev.text;
                  if (ev.type === 'tool') log.textContent += '\n〔' + ev.name + '〕' + ev.detail;
                  if (ev.type === 'error') log.textContent += '\n' + ev.message;
                }
              }
            };
          </script>
        </body>
        </html>
        """;
}
