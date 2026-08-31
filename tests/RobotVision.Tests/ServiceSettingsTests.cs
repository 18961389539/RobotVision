using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// TCP 接入控制测试：IP 白名单（精确/通配/空名单）与并发连接上限。
/// 拒绝的连接不进入会话表、立即关闭；拒绝计数累计。
/// </summary>
public class TcpAccessControlTests : IDisposable
{
    private readonly TcpServerManager _tcp;

    public TcpAccessControlTests()
    {
        _tcp = NewServer();
    }

    public void Dispose() => _tcp.Dispose();

    private static TcpServerManager NewServer()
    {
        var server = new TcpServerManager(
            "127.0.0.1", GetFreePort(), 2000,
            (recipe, _, ct) => Task.FromResult(VisionResult.Success(recipe, [], 1)),
            NullLogger<TcpServerManager>.Instance);
        server.Start();
        return server;
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task<bool> CanExchangePingAsync(int port)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("PING\n"));
        var buffer = new byte[256];
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            var read = await stream.ReadAsync(buffer, timeoutCts.Token);
            return read > 0 && Encoding.ASCII.GetString(buffer, 0, read).Trim() == "PONG";
        }
        catch (IOException)
        {
            return false;
        }
    }

    [Fact]
    public async Task EmptyWhitelist_AllowsLoopback()
    {
        _tcp.IpWhitelist = [];
        Assert.True(await CanExchangePingAsync(_tcp.Port));
        Assert.Equal(1, _tcp.TotalConnections);
        Assert.Equal(0, _tcp.RejectedConnections);
    }

    [Fact]
    public async Task WhitelistNotMatching_RejectsImmediately()
    {
        _tcp.IpWhitelist = ["10.99.99.99"];
        Assert.False(await CanExchangePingAsync(_tcp.Port));
        Assert.Equal(0, _tcp.TotalConnections);
        Assert.Equal(1, _tcp.RejectedConnections);
        Assert.Equal(0, _tcp.ConnectedClients);
    }

    [Fact]
    public async Task WhitelistExactLoopback_Allows()
    {
        _tcp.IpWhitelist = ["127.0.0.1"];
        Assert.True(await CanExchangePingAsync(_tcp.Port));
    }

    [Fact]
    public async Task WhitelistWildcardPrefix_Allows()
    {
        _tcp.IpWhitelist = ["127.0*"];
        Assert.True(await CanExchangePingAsync(_tcp.Port));
    }

    [Fact]
    public async Task MaxConnections_SecondClientRejected()
    {
        _tcp.MaxConnections = 1;

        using var first = new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        using var firstStream = first.GetStream();
        await firstStream.WriteAsync(Encoding.ASCII.GetBytes("PING\n"));
        var buffer = new byte[256];
        var read = await firstStream.ReadAsync(buffer);
        Assert.Equal("PONG", Encoding.ASCII.GetString(buffer, 0, read).Trim());

        Assert.False(await CanExchangePingAsync(_tcp.Port));
        Assert.Equal(1, _tcp.TotalConnections);
        Assert.Equal(1, _tcp.RejectedConnections);
        Assert.Equal(1, _tcp.ConnectedClients);
    }

    [Fact]
    public async Task MaxConnections_HotChangeTakesEffect()
    {
        _tcp.MaxConnections = 0;
        using var first = new TcpClient();
        await first.ConnectAsync(IPAddress.Loopback, _tcp.Port);
        using var firstStream = first.GetStream();
        // PONG 往返确保服务端已完成该连接的注册，再收紧上限
        await firstStream.WriteAsync(Encoding.ASCII.GetBytes("PING\n"));
        var buffer = new byte[256];
        await firstStream.ReadAsync(buffer);

        _tcp.MaxConnections = 1;
        Assert.False(await CanExchangePingAsync(_tcp.Port));
    }

    // ---- 白名单条目校验（与匹配器语义一致）----

    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("192.168.*")]
    [InlineData("192.168.1.*")]
    [InlineData("10.0.0.1")]
    [InlineData("::1")]
    public void TryParseWhitelistEntry_AcceptsValid(string entry)
        => Assert.True(TcpServerManager.TryParseWhitelistEntry(entry));

    [Theory]
    [InlineData("192.168.*.10")]
    [InlineData("999.1.1.1")]
    [InlineData("abc")]
    [InlineData("192.168.1.5*")]
    [InlineData("")]
    [InlineData("  ")]
    public void TryParseWhitelistEntry_RejectsInvalid(string entry)
        => Assert.False(TcpServerManager.TryParseWhitelistEntry(entry));

    // ---- 服务内热重启端点 ----

    [Fact]
    public async Task Restart_ToNewPort_ServesPing()
    {
        var newPort = GetFreePort();

        Assert.True(_tcp.Restart("127.0.0.1", newPort));

        Assert.True(_tcp.IsRunning);
        Assert.Equal(newPort, _tcp.Port);
        Assert.True(await CanExchangePingAsync(newPort));
    }

    [Fact]
    public async Task Restart_PortOccupied_RollsBackToOldEndpoint()
    {
        var oldPort = _tcp.Port;

        // 占用一个新端口，让热重启必然失败
        var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        var busyPort = ((IPEndPoint)blocker.LocalEndpoint).Port;
        try
        {
            Assert.False(_tcp.Restart("127.0.0.1", busyPort));

            // 回滚后旧端点仍在监听
            Assert.True(_tcp.IsRunning);
            Assert.Equal(oldPort, _tcp.Port);
            Assert.True(await CanExchangePingAsync(oldPort));
        }
        finally
        {
            blocker.Stop();
        }
    }
}

/// <summary>服务参数写回 appsettings.json 测试：节点合并、内存同步、重启项判定。</summary>
public class AppSettingsStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "rv_setcfg_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try { File.Delete(_file); }
        catch (IOException) { }
    }

    private AppConfig WriteBase(string json)
    {
        File.WriteAllText(_file, json);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    [Fact]
    public void Save_WritesAllNodesAndPreservesOthers()
    {
        var cfg = WriteBase("""
            {
              "IpAddress": "0.0.0.0",
              "TcpPort": 9999,
              "TimeoutMs": 5000,
              "MaxQueueDepth": 4,
              "Cameras": [ { "Id": "cam_file", "Type": "File", "Folder": "data/replay" } ],
              "FailureImage": { "Enabled": true, "Folder": "data/failures", "RetainedCount": 200 }
            }
            """);
        var store = new AppSettingsStore(cfg, _file);

        store.Save(new ServiceSettingsValues(
            TimeoutMs: 3000, MaxQueueDepth: 8, MaxConcurrent: 2, TcpBacklog: 16, MaxConnections: 4,
            FailureEnabled: false, FailureRetainedCount: 50,
            IpAddress: "192.168.1.50", TcpPort: 8888,
            IpWhitelist: ["192.168.1.10", "192.168.2.*"],
            FailureRetainedDays: 14,
            CaptureSuccessEnabled: true, CaptureSuccessRetainedDays: 7, CaptureSuccessMaxWidth: 1280,
            ResultLogEnabled: false, ResultLogJsonl: true, ResultLogSqlite: false, ResultLogRetainedDays: 60,
            InferenceProvider: "OpenVinoCpu", InferenceMaxSessions: 4,
            FileLoggingEnabled: false, FileLoggingRetainedDays: 14,
            ProcessHealthRetainedDays: 45));

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        var root = doc.RootElement;
        Assert.Equal(3000, root.GetProperty("TimeoutMs").GetInt32());
        Assert.Equal(8, root.GetProperty("MaxQueueDepth").GetInt32());
        Assert.Equal(2, root.GetProperty("MaxConcurrent").GetInt32());
        Assert.Equal(16, root.GetProperty("TcpBacklog").GetInt32());
        Assert.Equal(4, root.GetProperty("MaxConnections").GetInt32());
        Assert.Equal("192.168.1.50", root.GetProperty("IpAddress").GetString());
        Assert.Equal(8888, root.GetProperty("TcpPort").GetInt32());
        Assert.Equal(2, root.GetProperty("IpWhitelist").GetArrayLength());
        Assert.Equal("192.168.2.*", root.GetProperty("IpWhitelist")[1].GetString());
        Assert.False(root.GetProperty("FailureImage").GetProperty("Enabled").GetBoolean());
        Assert.Equal(50, root.GetProperty("FailureImage").GetProperty("RetainedCount").GetInt32());
        Assert.Equal(14, root.GetProperty("FailureImage").GetProperty("RetainedDays").GetInt32());
        Assert.True(root.GetProperty("CaptureSuccess").GetProperty("Enabled").GetBoolean());
        Assert.Equal(7, root.GetProperty("CaptureSuccess").GetProperty("RetainedDays").GetInt32());
        Assert.Equal(1280, root.GetProperty("CaptureSuccess").GetProperty("MaxWidth").GetInt32());
        Assert.False(root.GetProperty("ResultLog").GetProperty("Enabled").GetBoolean());
        Assert.Equal(60, root.GetProperty("ResultLog").GetProperty("RetainedDays").GetInt32());
        Assert.Equal("OpenVinoCpu", root.GetProperty("Inference").GetProperty("Provider").GetString());
        Assert.Equal(4, root.GetProperty("Inference").GetProperty("MaxSessions").GetInt32());
        Assert.False(root.GetProperty("FileLogging").GetProperty("Enabled").GetBoolean());
        Assert.Equal(14, root.GetProperty("FileLogging").GetProperty("RetainedDays").GetInt32());
        Assert.Equal(45, root.GetProperty("ProcessHealth").GetProperty("RetainedDays").GetInt32());
        Assert.Equal(0, root.GetProperty("IdleTimeoutMs").GetInt64());
        Assert.True(root.GetProperty("PoseCheck").GetProperty("Enabled").GetBoolean());
        // 其他节点保留
        Assert.Equal("cam_file", root.GetProperty("Cameras")[0].GetProperty("Id").GetString());

        // 内存同步
        Assert.Equal(3000, cfg.TimeoutMs);
        Assert.Equal(8, cfg.MaxQueueDepth);
        Assert.Equal("192.168.1.50", cfg.IpAddress);
        Assert.Equal(8888, cfg.TcpPort);
        Assert.False(cfg.FailureImage.Enabled);
        Assert.Equal(50, cfg.FailureImage.RetainedCount);
        Assert.Equal(14, cfg.FailureImage.RetainedDays);
        Assert.True(cfg.CaptureSuccess.Enabled);
        Assert.Equal(7, cfg.CaptureSuccess.RetainedDays);
        Assert.Equal(1280, cfg.CaptureSuccess.MaxWidth);
        Assert.False(cfg.ResultLog.Enabled);
        Assert.True(cfg.ResultLog.Jsonl);
        Assert.False(cfg.ResultLog.Sqlite);
        Assert.Equal(60, cfg.ResultLog.RetainedDays);
        Assert.Equal("OpenVinoCpu", cfg.Inference.Provider);
        Assert.Equal(4, cfg.Inference.MaxSessions);
        Assert.False(cfg.FileLogging.Enabled);
        Assert.Equal(14, cfg.FileLogging.RetainedDays);
        Assert.Equal(45, cfg.ProcessHealth.RetainedDays);
        Assert.Equal(2, cfg.IpWhitelist.Count);
    }

    [Fact]
    public void Save_ResultLogEnabledWithoutSink_Throws()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }""");
        var store = new AppSettingsStore(cfg, _file);
        var values = new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999, [],
            ResultLogEnabled: true, ResultLogJsonl: false, ResultLogSqlite: false);
        Assert.Throws<InvalidDataException>(() => store.Save(values));
    }

    [Fact]
    public void Save_EndpointChanged_ReturnsEmpty_RestartHandledByCaller()
    {
        // 新语义：端点热重启由调用方（设置页）执行，Store 只落盘 + 同步内存
        var cfg = WriteBase("""
            { "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }
            """);
        var store = new AppSettingsStore(cfg, _file);

        var restart = store.Save(new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 7777, []));
        Assert.Empty(restart);
        Assert.Equal(7777, cfg.TcpPort);

        restart = store.Save(new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "10.0.0.5", 7777, []));
        Assert.Empty(restart);
        Assert.Equal("10.0.0.5", cfg.IpAddress);
    }

    [Fact]
    public void Save_EndpointUnchanged_NoRestartItems()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }""");
        var store = new AppSettingsStore(cfg, _file);

        var restart = store.Save(new ServiceSettingsValues(
            1000, 6, 2, 16, 2, false, 100, "0.0.0.0", 9999, []));

        Assert.Empty(restart);
    }

    [Fact]
    public void Save_MissingFile_CreatesMinimalConfig()
    {
        Assert.False(File.Exists(_file));
        var cfg = new AppConfig();
        var store = new AppSettingsStore(cfg, _file);

        store.Save(new ServiceSettingsValues(5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999, []));

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        Assert.Equal(9999, doc.RootElement.GetProperty("TcpPort").GetInt32());
    }

    // ---- 校验下沉（Save 拒绝非法值，任何入口共用）----

    [Theory]
    [InlineData(499, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999)]   // 超时 <500
    [InlineData(5000, 0, 2, 16, 0, true, 200, "0.0.0.0", 9999)]  // 队列 <1
    [InlineData(5000, 4, 5, 16, 0, true, 200, "0.0.0.0", 9999)]  // 并发 > 队列深度
    [InlineData(5000, 4, 2, 0, 0, true, 200, "0.0.0.0", 9999)]   // backlog <1
    [InlineData(5000, 4, 2, 16, -1, true, 200, "0.0.0.0", 9999)] // 连接上限 <0
    [InlineData(5000, 4, 2, 16, 0, true, -1, "0.0.0.0", 9999)]   // 留存 <0
    [InlineData(5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 0)]     // 端口 0
    [InlineData(5000, 4, 2, 16, 0, true, 200, "not-an-ip", 9999)]
    public void Save_InvalidValues_Throws(
        int timeout, int queue, int concurrent, int backlog, int maxConn,
        bool failureEnabled, int retained, string ip, int port)
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999 }""");
        var store = new AppSettingsStore(cfg, _file);

        var values = new ServiceSettingsValues(
            timeout, queue, concurrent, backlog, maxConn,
            failureEnabled, retained, ip, port, []);
        Assert.Throws<InvalidDataException>(() => store.Save(values));
    }

    [Fact]
    public void Save_InvalidWhitelistEntry_Throws()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999 }""");
        var store = new AppSettingsStore(cfg, _file);

        var values = new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999,
            ["192.168.*.10"]); // 中间通配非法
        Assert.Throws<InvalidDataException>(() => store.Save(values));
    }

    [Fact]
    public void Save_IdleTimeoutNegative_Throws()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }""");
        var store = new AppSettingsStore(cfg, _file);
        var values = new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999, [], IdleTimeoutMs: -1);
        Assert.Throws<InvalidDataException>(() => store.Save(values));
    }

    [Fact]
    public void Save_IdleTimeoutBelowOneSecond_Throws()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }""");
        var store = new AppSettingsStore(cfg, _file);
        var values = new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999, [], IdleTimeoutMs: 999);
        Assert.Throws<InvalidDataException>(() => store.Save(values));
    }

    [Fact]
    public void Save_IdleTimeoutThirtyDays_Writes()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }""");
        var store = new AppSettingsStore(cfg, _file);
        store.Save(new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999, [],
            IdleTimeoutMs: TcpServerManager.IdleTimeoutThirtyDaysMs));

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        Assert.Equal(TcpServerManager.IdleTimeoutThirtyDaysMs,
            doc.RootElement.GetProperty("IdleTimeoutMs").GetInt64());
        Assert.Equal(TcpServerManager.IdleTimeoutThirtyDaysMs, cfg.IdleTimeoutMs);
    }

    [Fact]
    public void Save_InvalidInferenceProvider_Throws()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }""");
        var store = new AppSettingsStore(cfg, _file);
        var values = new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999, [],
            InferenceProvider: "OnnxRuntime");
        Assert.Throws<InvalidDataException>(() => store.Save(values));
    }

    [Fact]
    public void Save_PoseCheckToleranceNonPositive_Throws()
    {
        var cfg = WriteBase("""{ "IpAddress": "0.0.0.0", "TcpPort": 9999, "TimeoutMs": 5000 }""");
        var store = new AppSettingsStore(cfg, _file);
        var values = new ServiceSettingsValues(
            5000, 4, 2, 16, 0, true, 200, "0.0.0.0", 9999, [],
            PoseXyToleranceMm: 0);
        Assert.Throws<InvalidDataException>(() => store.Save(values));
    }

    [Fact]
    public void ValidateConfig_GigEGrabTimeoutNotLessThanTotal_Throws()
    {
        var cfg = new AppConfig
        {
            TimeoutMs = 90_000,
            Cameras =
            [
                new CameraConfig { Id = "cam_gige", Type = "GigEVision", GrabTimeoutMs = 90_000 },
            ],
        };
        var ex = Assert.Throws<InvalidDataException>(() => AppSettingsStore.ValidateConfig(cfg));
        Assert.Contains("cam_gige", ex.Message);
    }

    [Fact]
    public void NormalizeVisionTiming_RaisesTimeoutButKeepsValidGrabTimeout()
    {
        var cfg = new AppConfig
        {
            TimeoutMs = 5_000,
            Cameras =
            [
                new CameraConfig { Id = "cam_basler", Type = "Basler", GrabTimeoutMs = 2_000 },
            ],
        };
        cfg.NormalizeVisionTiming();
        Assert.Equal(AppConfig.DefaultRequestTimeoutMs, cfg.TimeoutMs);
        Assert.Equal(2_000, cfg.Cameras[0].GrabTimeoutMs);
    }
}

/// <summary>失败留存运行时开关测试：Enabled/RetainedCount 热改后立即生效。</summary>
public class FailureImageStoreRuntimeTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rv_failrt_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
    }

    [Fact]
    public void DisabledAtRuntime_SavesNothing()
    {
        var cfg = new FailureImageConfig { Enabled = true, Folder = _folder, RetainedCount = 200 };
        var store = new FailureImageStore(cfg, NullLogger<FailureImageStore>.Instance);

        store.Enabled = false;
        using var mat = new OpenCvSharp.Mat(8, 8, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(0));
        store.Save("R", mat, VisionResult.Fail("R", VisionErrorCode.NoTargetFound, "未检出", 1));

        Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder, "*.png").Length > 0);
    }

    [Fact]
    public void RetainedCountTightenedAtRuntime_TrimsToNewLimit()
    {
        var cfg = new FailureImageConfig { Enabled = true, Folder = _folder, RetainedCount = 10 };
        var store = new FailureImageStore(cfg, NullLogger<FailureImageStore>.Instance,
            () => new DateTime(2026, 8, 22, 10, 0, 0));

        using var mat = new OpenCvSharp.Mat(8, 8, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(0));
        // 用 1099（非 1007）验证数量滚动：1007 有 1 分钟限流窗口，会干扰本用例
        for (var i = 0; i < 5; i++)
            store.Save("R", mat, VisionResult.Fail("R", VisionErrorCode.InternalError, "内部错误", 1));
        WaitForPngCount(_folder, 5); // Save 已异步化，等待后台落盘完成
        Assert.Equal(5, Directory.GetFiles(_folder, "*.png").Length);

        store.RetainedCount = 2;
        store.Save("R", mat, VisionResult.Fail("R", VisionErrorCode.InternalError, "内部错误", 1));
        WaitForPngCount(_folder, 2); // 等待最后一次落盘（含滚动清理到新上限）
        Assert.Equal(2, Directory.GetFiles(_folder, "*.png").Length);
    }

    /// <summary>等待后台落盘完成（Save 已异步化，断言前轮询目录避免与后台线程竞态）。</summary>
    private static void WaitForPngCount(string folder, int expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (Directory.Exists(folder) && Directory.GetFiles(folder, "*.png").Length == expected)
                return;
            Thread.Sleep(25);
        }
        Assert.Fail($"等待失败现场落盘超时（期望 {expected} 张 PNG）");
    }
}
