using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;

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

    public static async Task<TestServer> StartAsync(
        Action<AppConfig, string>? configure = null,
        Func<TestInferenceEngine>? engineFactory = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_it_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "recipes"));
        Directory.CreateDirectory(Path.Combine(root, "calibration"));
        Directory.CreateDirectory(Path.Combine(root, "replay"));

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repoModels = Path.Combine(repoRoot, "models");
        var repoReplay = Path.Combine(repoRoot, "data", "replay");

        // 占位模型写到临时目录，绝不往仓库 models/ 写空文件。
        // Fake 引擎覆盖真实 ONNX；ModelManager.Load 仍要求文件存在且非空。
        var modelsDir = Path.Combine(root, "models");
        Directory.CreateDirectory(modelsDir);
        var dest = Path.Combine(modelsDir, "a01_kpt.onnx");
        var real = Directory.Exists(repoModels)
            ? Directory.EnumerateFiles(repoModels, "*.onnx").FirstOrDefault(p => new FileInfo(p).Length > 0)
            : null;
        if (real is not null)
            File.Copy(real, dest, overwrite: true);
        else
            File.WriteAllText(dest, "placeholder");

        // FileCamera 构造要求目录非空且图片可解码；CI 上 data/replay 不入库 → 生成占位图
        var replayDir = Path.Combine(root, "replay");
        if (!Directory.Exists(repoReplay))
        {
            using var placeholder = new Mat(64, 64, MatType.CV_8UC3, new Scalar(96, 96, 96));
            Cv2.ImWrite(Path.Combine(replayDir, "placeholder.png"), placeholder);
        }

        var cfg = new AppConfig
        {
            IpAddress = "127.0.0.1",
            TcpPort = FreeTcpPort(),
            TimeoutMs = 5000,
            RecipesFolder = Path.Combine(root, "recipes"),
            ModelsFolder = modelsDir,
            CalibrationFolder = Path.Combine(root, "calibration"),
            FileLogging = new FileLoggingConfig { Enabled = false },
            FailureImage = new FailureImageConfig { Folder = Path.Combine(root, "failures") },
            ResultLog = new ResultLogConfig { Enabled = true, Folder = Path.Combine(root, "results") },
            // 联锁/统计状态必须每实例独立:默认 data/metrics 按 exe 锚定,所有 TestServer 共享
            // 同一文件会导致计数跨测试泄漏(Total 虚高、残留联锁 1018)
            ProcessHealth = new ProcessHealthConfig { Folder = Path.Combine(root, "metrics") },
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
        services.AddLogging(b => b.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
        services.AddRobotVision(cfg);

        // 覆盖 AddRobotVision 内的真实 ONNX 引擎工厂(后注册优先)：
        // 集成测试不依赖真实模型(不入库)，Fake 引擎默认空结果 → 推理"未检出"走 1007 路径；
        // 免模型策略(DualBlob 等)不经推理引擎，成功路径不受影响。
        // 测试可传 engineFactory 定制引擎(如注入耗时模拟 busy 窗口)。
        services.AddSingleton<IInferenceEngineFactory>(
            new TestInferenceEngineFactory(engineFactory ?? (() => new TestInferenceEngine())));
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
