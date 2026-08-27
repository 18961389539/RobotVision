using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.IntegrationTests;

/// <summary>
/// 集成测试共享服务器：临时目录 + 虚拟相机 + DI 容器（AddRobotVision 完整组装）
/// + 真实 TCP 监听。每个测试独立实例，Dispose 时停止监听并清理目录。
/// </summary>
public sealed class TestServer : IAsyncDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _provider;

    public AppConfig Cfg { get; }
    public TcpServerManager Tcp { get; }
    public VisionService Vision { get; }
    public ServiceProvider Provider => _provider;
    public int Port { get; }
    public string RecipeFolder { get; }

    private TestServer(string root, ServiceProvider provider, AppConfig cfg, TcpServerManager tcp, VisionService vision, int port)
    {
        _root = root;
        _provider = provider;
        Cfg = cfg;
        Tcp = tcp;
        Vision = vision;
        Port = port;
        RecipeFolder = Path.Combine(root, "recipes");
    }

    public static async Task<TestServer> StartAsync(Action<AppConfig, string>? configure = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_it_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "recipes"));
        Directory.CreateDirectory(Path.Combine(root, "calibration"));
        Directory.CreateDirectory(Path.Combine(root, "replay"));

        // 仓库根（bin/Debug/net8.0-windows → ../../../../../ 共 5 级）下存在真实模型与回放图，
        // 集成测试直接复用：配方引用校验可通过，并可跑真实推理链路
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repoModels = Path.Combine(repoRoot, "models");
        var repoReplay = Path.Combine(repoRoot, "data", "replay");

        var cfg = new AppConfig
        {
            IpAddress = "127.0.0.1",
            TcpPort = FreeTcpPort(),
            TimeoutMs = 5000,
            RecipesFolder = Path.Combine(root, "recipes"),
            ModelsFolder = repoModels,
            CalibrationFolder = Path.Combine(root, "calibration"),
            FileLogging = new FileLoggingConfig { Enabled = false },
            FailureImage = new FailureImageConfig { Folder = Path.Combine(root, "failures") },
            ResultLog = new ResultLogConfig { Enabled = true, Folder = Path.Combine(root, "results") },
            Cameras =
            [
                new CameraConfig { Id = "cam_virtual", Type = "Virtual", Width = 128, Height = 96, Pattern = "Bars" },
                new CameraConfig
                {
                    Id = "cam_file", Type = "File",
                    Folder = Directory.Exists(repoReplay) ? repoReplay : Path.Combine(root, "replay"),
                },
            ],
        };
        configure?.Invoke(cfg, root);

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddRobotVision(cfg);
        var provider = services.BuildServiceProvider();

        var tcp = provider.GetRequiredService<TcpServerManager>();
        var vision = provider.GetRequiredService<VisionService>();

        // 为虚拟相机加载零畸变内参档案：取图后的去畸变步骤可正常执行
        // （否则管线在 Undistort 处返回 1004，无法覆盖取图/推理路径）
        var calibration = provider.GetRequiredService<RobotVision.Infrastructure.Calibration.CalibrationManager>();
        calibration.LoadIntrinsic(new RobotVision.Core.Models.IntrinsicProfile
        {
            CameraId = "cam_virtual",
            Width = 128,
            Height = 96,
            CameraMatrix = [100, 0, 64, 0, 100, 48, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });

        tcp.Start();

        return new TestServer(root, provider, cfg, tcp, vision, cfg.TcpPort);
    }

    public void WriteRecipe(string name, string json) =>
        File.WriteAllText(Path.Combine(RecipeFolder, name + ".json"), json);

    /// <summary>建立 TCP 连接发送一行并读取一行应答。</summary>
    public async Task<string> SendAsync(string line, int timeoutMs = 8000)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(timeoutMs);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        using var stream = client.GetStream();
        var payload = Encoding.ASCII.GetBytes(line + "\n");
        await stream.WriteAsync(payload, cts.Token);
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (stream.DataAvailable)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
                if (n == 0)
                    break;
                sb.Append(Encoding.ASCII.GetString(buffer, 0, n));
                if (sb.ToString().Contains('\n'))
                    break;
            }
            else
            {
                await Task.Delay(10, cts.Token);
            }
        }
        var reply = sb.ToString();
        return reply.TrimEnd('\r', '\n');
    }

    /// <summary>建立连接并返回流（用于白名单拒绝等场景的原始连接断言）。</summary>
    public async Task<TcpClient> ConnectAsync(int timeoutMs = 3000)
    {
        var client = new TcpClient();
        using var cts = new CancellationTokenSource(timeoutMs);
        await client.ConnectAsync("127.0.0.1", Port, cts.Token);
        return client;
    }

    public ValueTask DisposeAsync()
    {
        try { Tcp.Stop(); } catch { /* 尽力而为 */ }
        _provider.Dispose();
        try { Directory.Delete(_root, true); } catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return ValueTask.CompletedTask;
    }

    private static int FreeTcpPort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
