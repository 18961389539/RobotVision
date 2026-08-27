using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using Xunit;

namespace RobotVision.IntegrationTests;

/// <summary>
/// 管线冒烟测试（补齐原集成测试只覆盖错误路径的空缺）：
/// - 成功路径：TRIGGER → 双BLOB检出 → 外参映射 → OK + 机器人坐标（冒烟的灵魂：能出坐标）；
/// - TCP 应答格式：OK,x,y,rz,配方,数量,耗时；
/// - 灯光链路：None 控制器 + 稳定延时配置全链路走通；
/// - 压力：同一回放配方连续 15 次触发全部成功（无崩溃/无僵尸）；
/// - 失败现场图落盘：未检出目标时 failures 目录生成图片；
/// - 配置漂移：外参标定分辨率与内参不一致被拒绝（防静默错位）。
/// 全部用合成图 + DualBlobCenterLine（无模型），CI/开发机可直接运行。
/// 共享一个 TestServer（fixture），避免每个用例重复冷启动 DI 容器。
/// </summary>
public class SmokePipelineIntegrationTests : IClassFixture<SmokePipelineIntegrationTests.SmokeServerFixture>
{
    private const int ImageSize = 512;

    private readonly SmokeServerFixture _fixture;

    public SmokePipelineIntegrationTests(SmokeServerFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 512×512 黑底 + 主圆(150,200,r40) + 次圆(250,200,r20)：
    /// 主 BLOB 包围盒外扩 100% 后窗口 (30,80,240,240) 覆盖次圆（250,200 在窗口内），
    /// 主→次水平连线角度 0°，距离 100px ∈ [5,800]。DualBlob 必检出。
    /// </summary>
    private static void WriteDualBlobImage(string dir)
    {
        Directory.CreateDirectory(dir);
        using var img = new Mat(ImageSize, ImageSize, MatType.CV_8UC3, Scalar.All(0));
        Cv2.Circle(img, new Point(150, 200), 40, Scalar.All(255), thickness: -1);
        Cv2.Circle(img, new Point(250, 200), 20, Scalar.All(255), thickness: -1);
        Cv2.ImWrite(Path.Combine(dir, "dualblob.png"), img);
    }

    /// <summary>纯黑图：无任何目标（走"未检出目标"分支）。</summary>
    private static void WritePlainImage(string dir)
    {
        Directory.CreateDirectory(dir);
        using var img = new Mat(ImageSize, ImageSize, MatType.CV_8UC3, Scalar.All(0));
        Cv2.ImWrite(Path.Combine(dir, "plain.png"), img);
    }

    /// <summary>单次触发的取消预算：管线挂起时 30 秒后返回 1008，测试不无限等待。</summary>
    private static async Task<VisionResult> RunWithBudget(TestServer server, string recipe)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await server.Vision.RunAsync(recipe, cts.Token);
    }

    [Fact]
    public async Task Trigger_DualBlob_Success_ReturnsRobotCoordinates()
    {
        _fixture.Server.WriteRecipe("BLOB", """
            {"cameraId":"cam_file","stationId":"st1","angleMode":"DualBlobCenterLine"}
            """);

        var result = await RunWithBudget(_fixture.Server, "BLOB");

        result.Ok.Should().BeTrue($"成功路径应输出坐标，实际: {result.ErrorCode} {result.Message}");
        var pose = result.Poses.Should().ContainSingle().Subject;
        pose.X.Should().BeApproximately(15, 0.5);   // 主质心 150px × 0.1 = 15mm
        pose.Y.Should().BeApproximately(20, 0.5);   // 200px × 0.1 = 20mm
        // 主(150,200)→次(250,200) 水平连线：角度 0°（有方向，主次有序消 180° 歧义）
        pose.AngleDeg.Should().BeApproximately(0, 0.5);
    }

    [Fact]
    public async Task Trigger_DualBlob_TcpReply_OkWithCoordinates()
    {
        _fixture.Server.WriteRecipe("BLOB_TCP", """
            {"cameraId":"cam_file","stationId":"st1","angleMode":"DualBlobCenterLine"}
            """);

        var reply = await _fixture.Server.SendAsync("BLOB_TCP");

        // 协议：OK,x,y,rz,配方名,数量,耗时
        reply.Should().StartWith("OK,15.000,20.000,0.000");
        reply.Should().Contain(",BLOB_TCP,1,");
    }

    [Fact]
    public async Task Trigger_WithLighting_StabilizeDelayApplied()
    {
        _fixture.Server.WriteRecipe("LIGHT", """
            {"cameraId":"cam_file","stationId":"st1","angleMode":"DualBlobCenterLine",
             "lightControllerId":"light1",
             "lighting":{"channels":[{"channel":1,"brightness":200}],
                         "stabilizeDelayMs":30,"turnOffAfterGrab":true}}
            """);

        var watch = Stopwatch.StartNew();
        var result = await RunWithBudget(_fixture.Server, "LIGHT");
        watch.Stop();

        result.Ok.Should().BeTrue();
        // 稳定延时 30ms 计入单次触发预算（点亮→稳定→取图→熄灯 链路全走通）
        watch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(25);
    }

    [Fact]
    public async Task Trigger_Repeated15Times_AllSucceed()
    {
        _fixture.Server.WriteRecipe("BLOB_STRESS", """
            {"cameraId":"cam_file","stationId":"st1","angleMode":"DualBlobCenterLine"}
            """);

        for (var i = 0; i < 15; i++)
        {
            var result = await RunWithBudget(_fixture.Server, "BLOB_STRESS");
            result.Ok.Should().BeTrue($"第 {i + 1} 次触发应成功: {result.ErrorCode} {result.Message}");
            result.Poses.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task Trigger_NoTarget_FailureImageSaved()
    {
        _fixture.Server.WriteRecipe("PLAIN", """
            {"cameraId":"cam_plain","stationId":"st_plain","angleMode":"DualBlobCenterLine"}
            """);

        var result = await RunWithBudget(_fixture.Server, "PLAIN");

        result.ErrorCode.Should().Be(VisionErrorCode.NoTargetFound);
        var failDir = _fixture.Server.Cfg.FailureImage.Folder;
        Directory.Exists(failDir).Should().BeTrue("失败现场目录应创建");
        // 落盘为 fire-and-forget（PNG 编码在后台线程）：轮询等待文件出现，最多 5 秒
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Directory.Exists(failDir) && Directory.GetFiles(failDir).Length == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        Directory.GetFiles(failDir).Should().NotBeEmpty("未检出目标时失败现场图应落盘");
    }

    [Fact]
    public async Task ExtrinsicResolutionMismatch_Rejected()
    {
        // st_drift 外参分辨率 600×600 ≠ cam_file 内参 512×512：换分辨率后旧外参失效，必须拒绝
        _fixture.Server.WriteRecipe("DRIFT", """
            {"cameraId":"cam_file","stationId":"st_drift","angleMode":"DualBlobCenterLine"}
            """);

        var result = await RunWithBudget(_fixture.Server, "DRIFT");

        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be(VisionErrorCode.NotCalibrated);
        result.Message.Should().Contain("分辨率");
    }

    [Fact]
    public async Task Trigger_DualBlob_ResultLogWritten()
    {
        // 成功触发后:data/results/ 应有 JSON Lines 记录(坐标/配方/耗时)
        _fixture.Server.WriteRecipe("BLOB_LOG", """
            {"cameraId":"cam_file","stationId":"st1","angleMode":"DualBlobCenterLine"}
            """);

        var result = await RunWithBudget(_fixture.Server, "BLOB_LOG");
        result.Ok.Should().BeTrue();

        var logDir = _fixture.Server.Cfg.ResultLog.Folder;
        var file = WaitForFile(logDir, "results-*.jsonl");
        file.Should().NotBeEmpty("成功触发后结果日志应落盘");
        var line = File.ReadAllLines(file).First();
        line.Should().Contain("BLOB_LOG");
        line.Should().Contain("\"Code\":0"); // 成功码
        line.Should().Contain("\"X\":15");   // 主质心 150px × 0.1 = 15mm

        var db = _fixture.Server.Provider.GetRequiredService<SqliteResultStore>();
        var blobQuery = new ResultDbQuery { Recipe = "BLOB_LOG" };
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && db.Count(blobQuery) < 1)
            Thread.Sleep(50);
        db.Count(blobQuery).Should().BeGreaterThan(0, "成功触发后 SQLite 应有该配方一行");
        var row = db.Query(blobQuery).Should().ContainSingle().Subject;
        row.Code.Should().Be(0);
        row.X.Should().NotBeNull();
        row.X!.Value.Should().BeApproximately(15, 0.5);
    }

    [Fact]
    public async Task Trigger_DualBlob_CaptureSuccess_StoresImage_WhenEnabled()
    {
        // CaptureSuccess 开关开启时:成功检测也存图(data/captures/yyyy-MM-dd/*.png + json)
        await using var server = await TestServer.StartAsync((cfg, root) =>
        {
            var replay = Path.Combine(root, "replay_dual");
            WriteDualBlobImage(replay);
            cfg.Cameras[1].Folder = replay;
            cfg.CaptureSuccess = new CaptureSuccessConfig
            {
                Enabled = true,
                Folder = Path.Combine(root, "captures"),
                MaxWidth = 0,
            };
        });
        LoadCalibrationFor(server);
        server.WriteRecipe("CAP", """
            {"cameraId":"cam_file","stationId":"st1","angleMode":"DualBlobCenterLine"}
            """);

        var result = await RunWithBudget(server, "CAP");
        result.Ok.Should().BeTrue();

        var captureDir = server.Cfg.CaptureSuccess.Folder;
        var png = WaitForFile(Path.Combine(captureDir, DateTime.Now.ToString("yyyy-MM-dd")), "*_OK.png");
        png.Should().NotBeEmpty("开启成功存图后应有 OK 现场图");
        // 元数据 JSON 与图同名;同后台线程 PNG 先写、JSON 后写,轮询等待
        var metaPath = Path.ChangeExtension(png, ".json");
        var metaDeadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(metaPath) && DateTime.UtcNow < metaDeadline)
            Thread.Sleep(50);
        File.Exists(metaPath).Should().BeTrue("应有同名元数据 JSON");
    }

    /// <summary>为独立 TestServer 加载 cam_file 内参 + st1 工位外参（与 fixture 初始化同配置）。</summary>
    private static void LoadCalibrationFor(TestServer server)
    {
        var cal = server.Provider.GetRequiredService<CalibrationManager>();
        cal.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam_file",
            Width = ImageSize,
            Height = ImageSize,
            CameraMatrix = [100, 0, ImageSize / 2.0, 0, 100, ImageSize / 2.0, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });
        cal.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam_file",
            Affine = [0.1, 0, 0, 0, 0.1, 0],
            Rms = 0.01,
            MaxResidual = 0.02,
            Width = ImageSize,
            Height = ImageSize,
        });
    }

    /// <summary>轮询等待目录中出现匹配文件(后台异步写盘)。</summary>
    private static string WaitForFile(string dir, string pattern, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var file = Directory.Exists(dir) ? Directory.GetFiles(dir, pattern).FirstOrDefault() : null;
            if (file is not null)
                return file;
            Thread.Sleep(50);
        }
        return "";
    }

    /// <summary>
    /// 共享 TestServer：一个 DI 容器 + 两组回放相机（dualblob / plain）+ None 灯光控制器
    /// + cam_file/cam_plain 内参 + st1/st_plain/st_drift 外参。全部用例复用，避免重复冷启动。
    /// </summary>
    public sealed class SmokeServerFixture : IAsyncLifetime
    {
        public TestServer Server { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Server = await TestServer.StartAsync((cfg, root) =>
            {
                // cam_file（索引 1）→ 双 BLOB 合成图（成功路径/灯光/压力/漂移）
                var replayDual = Path.Combine(root, "replay_dual");
                WriteDualBlobImage(replayDual);
                cfg.Cameras[1].Folder = replayDual;

                // 新增 cam_plain → 纯黑图（无目标分支）
                var replayPlain = Path.Combine(root, "replay_plain");
                WritePlainImage(replayPlain);
                cfg.Cameras.Add(new CameraConfig { Id = "cam_plain", Type = "File", Folder = replayPlain });

                // None 灯光控制器：协议链路全走通、零 IO
                cfg.LightControllers.Add(new LightControllerConfig { Id = "light1", Type = "None" });
            });

            var cal = Server.Provider.GetRequiredService<CalibrationManager>();
            cal.LoadIntrinsic(new IntrinsicProfile
            {
                CameraId = "cam_file",
                Width = ImageSize,
                Height = ImageSize,
                CameraMatrix = [100, 0, ImageSize / 2.0, 0, 100, ImageSize / 2.0, 0, 0, 1],
                DistCoeffs = [0, 0, 0, 0, 0],
            });
            cal.LoadIntrinsic(new IntrinsicProfile
            {
                CameraId = "cam_plain",
                Width = ImageSize,
                Height = ImageSize,
                CameraMatrix = [100, 0, ImageSize / 2.0, 0, 100, ImageSize / 2.0, 0, 0, 1],
                DistCoeffs = [0, 0, 0, 0, 0],
            });
            cal.LoadExtrinsic(new ExtrinsicProfile
            {
                StationId = "st1",
                CameraId = "cam_file",
                Affine = [0.1, 0, 0, 0, 0.1, 0], // px × 0.1 → mm
                Rms = 0.01,
                MaxResidual = 0.02,
                Width = ImageSize,
                Height = ImageSize,
            });
            cal.LoadExtrinsic(new ExtrinsicProfile
            {
                StationId = "st_plain",
                CameraId = "cam_plain",
                Affine = [0.1, 0, 0, 0, 0.1, 0],
                Rms = 0.01,
                MaxResidual = 0.02,
                Width = ImageSize,
                Height = ImageSize,
            });
            cal.LoadExtrinsic(new ExtrinsicProfile
            {
                StationId = "st_drift",
                CameraId = "cam_file",
                Affine = [0.1, 0, 0, 0, 0.1, 0],
                Rms = 0.01,
                MaxResidual = 0.02,
                Width = 600, // 故意与内参 512×512 不一致：漂移用例断言拒绝
                Height = 600,
            });
        }

        public async Task DisposeAsync()
        {
            await Server.DisposeAsync();
        }
    }
}
