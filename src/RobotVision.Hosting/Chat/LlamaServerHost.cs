using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting.Chat;

/// <summary>按需启动本机 llama-server（CPU），退出时关掉我们拉起的进程。</summary>
public sealed class LlamaServerHost : IHostedService, IDisposable
{
    private readonly ChatConfig _cfg;
    private readonly OpenAiChatClient _client;
    private readonly ILogger<LlamaServerHost>? _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private StreamWriter? _logWriter;
    private bool _startedByUs;

    public LlamaServerHost(ChatConfig cfg, OpenAiChatClient client, ILogger<LlamaServerHost>? log = null)
    {
        _cfg = cfg;
        _client = client;
        _log = log;
    }

    public string? LastError { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopOwnedProcess();
        return Task.CompletedTask;
    }

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (await _client.ProbeAsync(cancellationToken).ConfigureAwait(false))
            return;

        if (!_cfg.AutoStart)
        {
            LastError = $"未检测到 {_client.Endpoint}，且 Chat.AutoStart=false。";
            throw new InvalidOperationException(LastError);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await _client.ProbeAsync(cancellationToken).ConfigureAwait(false))
                return;

            if (_process is { HasExited: false } || IsLocalPortOpen(EffectivePort(_cfg)))
            {
                await WaitUntilHealthyAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var exe = ResolveLlamaServer(_cfg)
                ?? throw Fail($"找不到 llama-server.exe（当前配置 {_cfg.LlamaServerPath}）。请把 CPU 版解压到该路径。");
            var gguf = ResolveGguf(_cfg)
                ?? throw Fail($"找不到 GGUF（当前配置 {_cfg.GgufPath}）。请先下载 Qwen3.5-4B Q4_K_M 到该路径。");

            StartProcess(exe, gguf);
            await WaitUntilHealthyAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static string BuildArguments(string ggufPath, ChatConfig cfg)
    {
        var port = cfg.Port is > 0 and <= 65535 ? cfg.Port : 8080;
        var threads = cfg.Threads > 0 ? cfg.Threads : 8;
        var ctx = cfg.ContextSize >= 512 ? cfg.ContextSize : 8192;
        return string.Format(
            CultureInfo.InvariantCulture,
            "-m \"{0}\" --host 127.0.0.1 --port {1} -c {2} -t {3} -ngl 0 --parallel 1 --jinja --no-webui",
            ggufPath, port, ctx, threads);
    }

    public static int EffectivePort(ChatConfig cfg) =>
        cfg.Port is > 0 and <= 65535 ? cfg.Port : 8080;

    public static string? ResolveLlamaServer(ChatConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.LlamaServerPath))
            return File.Exists(cfg.LlamaServerPath) ? Path.GetFullPath(cfg.LlamaServerPath) : null;
        foreach (var candidate in LlamaServerCandidates())
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static string? ResolveGguf(ChatConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.GgufPath))
            return File.Exists(cfg.GgufPath) ? Path.GetFullPath(cfg.GgufPath) : null;
        foreach (var candidate in GgufCandidates())
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public void Dispose()
    {
        StopOwnedProcess();
        _gate.Dispose();
    }

    private void StartProcess(string exe, string gguf)
    {
        StopOwnedProcess();
        var dir = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;
        var logDir = Path.GetDirectoryName(gguf) ?? dir;
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "llama-server.log");
        _logWriter = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = BuildArguments(gguf, _cfg),
            WorkingDirectory = dir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _logWriter?.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _logWriter?.WriteLine(e.Data);
        };
        if (!process.Start())
            throw Fail("llama-server 进程未能启动。");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;
        _startedByUs = true;
        if (_log is { } log)
            LlamaServerHostLog.Started(log, process.Id, psi.Arguments);
    }

    private async Task WaitUntilHealthyAsync(CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(_cfg.LoadTimeoutSeconds > 0 ? _cfg.LoadTimeoutSeconds : 180);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process is { HasExited: true })
                throw Fail($"llama-server 已退出（code={_process.ExitCode}）。详见 llama-server.log。");
            if (await _client.ProbeAsync(cancellationToken).ConfigureAwait(false))
                return;
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }

        throw Fail($"等待 llama-server 就绪超时（{timeout.TotalSeconds:0}s）。详见 llama-server.log。");
    }

    private InvalidOperationException Fail(string message)
    {
        LastError = message;
        if (_log is { } log)
            LlamaServerHostLog.Warning(log, message);
        return new InvalidOperationException(message);
    }

    private void StopOwnedProcess()
    {
        if (!_startedByUs)
            return;
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // 退出阶段尽力而为
        }

        try { _process?.Dispose(); } catch { /* ignore */ }
        _process = null;
        _startedByUs = false;
        try { _logWriter?.Dispose(); } catch { /* ignore */ }
        _logWriter = null;
    }

    private static IEnumerable<string> LlamaServerCandidates()
    {
        yield return Path.Combine(@"E:\光模块\llm\llama-cpp", "llama-server.exe");
        yield return Path.Combine(AppContext.BaseDirectory, "llama-server.exe");
        yield return Path.Combine(AppContext.BaseDirectory, "llama-cpp", "llama-server.exe");
    }

    internal static bool IsLocalPortOpen(int port)
    {
        try
        {
            using var tcp = new TcpClient();
            var task = tcp.ConnectAsync("127.0.0.1", port);
            return task.Wait(TimeSpan.FromMilliseconds(200)) && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> GgufCandidates()
    {
        yield return Path.Combine(@"E:\光模块\llm", "Qwen3.5-4B-Q4_K_M.gguf");
        yield return Path.Combine(AppContext.BaseDirectory, "Qwen3.5-4B-Q4_K_M.gguf");
    }
}
