using Microsoft.Extensions.Logging;
using OpenCvSharp;
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
    private static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("RV_HARDWARE_TEST"), "1",
            StringComparison.OrdinalIgnoreCase);

    private static string OutDir =>
        Path.Combine(Path.GetTempPath(), "RobotVision-camera-test");

    [Fact]
    public void GigE_Discover_And_Grab_EachCamera()
    {
        if (!Enabled)
            return;

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
        if (!Enabled)
            return;

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
