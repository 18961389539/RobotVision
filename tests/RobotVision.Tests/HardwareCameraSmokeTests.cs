using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace RobotVision.Tests;

/// <summary>
/// 真实相机硬件冒烟测试：枚举 → 连接 → 单帧采集 → 存图验证。
/// 依赖现场真实相机与网络环境，默认不执行：设置环境变量 RV_HARDWARE_TEST=1 后运行
/// （dotnet test --filter "FullyQualifiedName~HardwareCameraSmokeTests"）。
/// 采集图片保存到 %TEMP%/RobotVision-camera-test/ 供人工检查（亮度/花屏/错位）。
/// </summary>
[Trait("Category", "Hardware")]
public class HardwareCameraSmokeTests(ITestOutputHelper output)
{
    private static string OutDir =>
        Path.Combine(Path.GetTempPath(), "RobotVision-camera-test");

    [Fact]
    public void GigE_Discover_And_Grab_EachCamera()
    {
        TestPreconditions.RequireHardware();

        var devices = GigEVisionCamera.EnumerateDevices();
        foreach (var d in devices)
            output.WriteLine($"发现设备: {d}");
        Assert.True(devices.Count > 0,
            "GigE 发现列表为空：检查网线/网卡 IP 网段/UDP 3956 防火墙。");

        Directory.CreateDirectory(OutDir);
        foreach (var device in devices)
        {
            var serial = device.Split('|')[0].Trim();
            using var cam = new GigEVisionCamera($"hwtest_{serial}", serial, grabTimeoutMs: 5000,
                log: new OutputLogger(output));

            Assert.True(cam.TryConnectOnce(), $"相机 {serial} 连接失败（详见日志）");
            output.WriteLine($"已连接: SN={cam.SerialNumber} Name={cam.FriendlyName}");

            using var frame = cam.Grab();
            Assert.True(frame.Image.Width > 0 && frame.Image.Height > 0,
                $"相机 {serial} 返回空图像");

            using var mat = VisionImageCv.AsMat(frame.Image);
            Cv2.MeanStdDev(mat, out var mean, out var stddev);
            output.WriteLine($"相机 {serial}: {frame.Image.Width}x{frame.Image.Height} " +
                             $"mean=({mean.Val0:F1},{mean.Val1:F1},{mean.Val2:F1}) " +
                             $"stddev=({stddev.Val0:F1},{stddev.Val1:F1},{stddev.Val2:F1})");

            var path = Path.Combine(OutDir, $"gige_{serial}.png");
            Cv2.ImWrite(path, mat);
            Assert.True(File.Exists(path) && new FileInfo(path).Length > 0,
                $"相机 {serial} 存图失败: {path}");
            output.WriteLine($"已保存: {path}");
        }
    }

    [Fact]
    public void Pylon_Discover_And_Grab_EachCamera()
    {
        TestPreconditions.RequireHardware();

        IReadOnlyList<string> devices;
        try
        {
            devices = BaslerCamera.EnumerateDevices();
        }
        catch (Exception ex)
        {
            // 未装 pylon 运行库：pylon 路径不可用但不算失败（GigE 路径独立验证）
            output.WriteLine($"pylon 枚举异常（运行库未安装？）: {ex.Message}");
            return;
        }
        foreach (var d in devices)
            output.WriteLine($"pylon 发现设备: {d}");
        if (devices.Count == 0)
        {
            output.WriteLine("pylon 未发现设备");
            return;
        }

        Directory.CreateDirectory(OutDir);
        foreach (var device in devices)
        {
            var serial = device.Split('|')[0].Trim();
            using var cam = new BaslerCamera($"hwtest_pylon_{serial}", serial, grabTimeoutMs: 5000);
            if (!cam.TryConnectOnce())
            {
                output.WriteLine($"pylon 相机 {serial} 连接失败");
                continue;
            }
            using var frame = cam.Grab();
            output.WriteLine($"pylon 相机 {serial}: {frame.Image.Width}x{frame.Image.Height}");
            var path = Path.Combine(OutDir, $"pylon_{serial}.png");
            using var mat = VisionImageCv.AsMat(frame.Image);
            Cv2.ImWrite(path, mat);
            output.WriteLine($"已保存: {path}");
        }
    }

    /// <summary>
    /// 连续取帧稳定性冒烟：连接后连续抓 30 帧，验证不丢帧/不花屏（每帧非空、尺寸一致、
    /// 内容有变化——运动场景下全黑/全白即异常）、帧间隔无异常尖峰。
    /// 单帧冒烟只证明"能取到图"，产线最常见的间歇花屏/丢帧需要连续帧才能暴露。
    /// </summary>
    [Fact]
    public void GigE_ContinuousGrab_30Frames_Stable()
    {
        TestPreconditions.RequireHardware();

        var devices = GigEVisionCamera.EnumerateDevices();
        if (devices.Count == 0)
        {
            output.WriteLine("未发现 GigE 设备（跳过）");
            return;
        }

        var serial = devices[0].Split('|')[0].Trim();
        using var cam = new GigEVisionCamera($"hwtest_{serial}", serial, grabTimeoutMs: 5000,
            log: new OutputLogger(output));
        Assert.True(cam.TryConnectOnce(), $"相机 {serial} 连接失败");

        var sizes = new HashSet<(int Width, int Height)>();
        Mat? previous = null;
        var blankFrames = 0;
        var intervalsMs = new List<double>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 30; i++)
        {
            using var frame = cam.Grab();
            Assert.False(frame.Image.IsEmpty, $"第 {i + 1} 帧为空（丢帧）");
            sizes.Add((frame.Image.Width, frame.Image.Height));

            using var mat = VisionImageCv.AsMat(frame.Image);
            Cv2.MeanStdDev(mat, out var mean, out var stddev);
            if (stddev.Val0 < 1.0)
                blankFrames++; // 整帧纯色 = 花屏/断流特征

            if (previous is not null)
            {
                using var diff = new Mat();
                Cv2.Absdiff(previous, mat, diff);
                if (Cv2.Mean(diff).Val0 < 0.5)
                    blankFrames++; // 相邻帧完全一致（30fps 场景不应逐帧冻结）
            }
            previous?.Dispose();
            previous = mat.Clone();

            intervalsMs.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
        }
        previous?.Dispose();

        Assert.Single(sizes); // 分辨率全程一致（分辨率跳变 = 链路异常）
        Assert.True(blankFrames <= 1,
            $"连续 30 帧出现 {blankFrames} 次纯色/冻结帧（疑似花屏或断流）");
        var avgInterval = intervalsMs.Average();
        output.WriteLine($"30 帧完成: 平均帧间隔 {avgInterval:0.0}ms");
        Assert.True(avgInterval < 2000, $"平均帧间隔 {avgInterval:0.0}ms 异常（>2s）");
    }

    /// <summary>
    /// 曝光/增益参数链路冒烟：相机支持 IExposureControl 时，把曝光从最小调到最大，
    /// 取帧均值应显著上升（参数写入 → 传感器生效 → 图像变化 的完整链路）。
    /// 参数没生效是现场常见故障（配置"改了"但图像不变），此用例直接暴露。
    /// </summary>
    [Fact]
    public void Exposure_ChangeAffectsBrightness()
    {
        TestPreconditions.RequireHardware();

        var devices = GigEVisionCamera.EnumerateDevices();
        TestSkip.When(devices.Count == 0, "No GigE devices discovered for exposure test.");

        var serial = devices[0].Split('|')[0].Trim();
        using var cam = new GigEVisionCamera($"hwtest_{serial}", serial, grabTimeoutMs: 5000,
            log: new OutputLogger(output));
        Assert.True(cam.TryConnectOnce(), $"相机 {serial} 连接失败");
        if (cam is not IExposureControl)
            TestSkip.Throw("Camera does not implement IExposureControl.");

        var range = cam.GetExposureRange();
        Assert.NotNull(range);
        Assert.True(range!.Value.Max > range.Value.Min, "曝光范围非法");

        // 最小曝光取帧
        Assert.True(cam.TrySetExposureTimeUs(range.Value.Min), "设置最小曝光失败");
        using var lowFrame = GrabSettled(cam);
        using var lowMat = VisionImageCv.AsMat(lowFrame.Image);
        var lowMean = Cv2.Mean(lowMat).Val0;

        // 最大曝光取帧
        Assert.True(cam.TrySetExposureTimeUs(range.Value.Max), "设置最大曝光失败");
        using var highFrame = GrabSettled(cam);
        using var highMat = VisionImageCv.AsMat(highFrame.Image);
        var highMean = Cv2.Mean(highMat).Val0;

        output.WriteLine($"曝光 {range.Value.Min}→{range.Value.Max}us: 均值 {lowMean:F1} → {highMean:F1}");
        Assert.True(highMean > lowMean + 5,
            $"高曝光均值应明显高于低曝光（参数未生效?）: {lowMean:F1} vs {highMean:F1}");

        // 恢复中值曝光，避免影响后续使用
        var mid = (range.Value.Min + range.Value.Max) / 2;
        cam.TrySetExposureTimeUs(mid);
    }

    private static CameraFrame GrabSettled(GigEVisionCamera cam)
    {
        using (cam.Grab()) { }
        using (cam.Grab()) { }
        return cam.Grab();
    }

    private sealed class OutputLogger(ITestOutputHelper output) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception is not null)
                output.WriteLine(exception.ToString());
        }
    }
}
