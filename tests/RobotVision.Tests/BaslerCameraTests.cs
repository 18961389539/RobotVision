using System.Text.Json;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// Basler 相机接入的纯托管测试：配置默认值、字段持久化往返、
/// 设备枚举在无 pylon 运行库时不抛异常（降级为空列表）。
/// 真实采集链路需在装有 pylon Camera Software Suite 的工控机上验证。
/// Grab 用 Start + 短超时 RetrieveResult 轮询，不得与连接阶段的连续采集混用（pylon 前置条件：采集已停止）。
/// </summary>
public class BaslerCameraTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "rv_basler_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try { File.Delete(_file); }
        catch (IOException) { }
    }

    [Fact]
    public void CameraConfig_DefaultTypeIsBasler()
    {
        Assert.Equal("Basler", new CameraConfig().Type);
    }

    [Fact]
    public void Save_RoundTripsBaslerFields()
    {
        var cfg = new AppConfig();
        var store = new CameraConfigStore(cfg, _file);

        store.Save([new CameraConfig
        {
            Id = "cam_basler",
            Type = "Basler",
            DeviceId = "40233271",
            ExposureTimeUs = 8000,
            Gain = 3.5,
            GrabTimeoutMs = 2500,
        }]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        var camera = doc.RootElement.GetProperty("Cameras")[0];
        Assert.Equal("Basler", camera.GetProperty("Type").GetString());
        Assert.Equal("40233271", camera.GetProperty("DeviceId").GetString());
        Assert.Equal(8000, camera.GetProperty("ExposureTimeUs").GetDouble());
        Assert.Equal(3.5, camera.GetProperty("Gain").GetDouble());
        Assert.Equal(2500, camera.GetProperty("GrabTimeoutMs").GetInt32());

        // 未设置的 File 目录为空串而非 null，正常落盘
        Assert.Equal("", camera.GetProperty("Folder").GetString());

        // 内存配置同步
        var restored = cfg.Cameras.Single();
        Assert.Equal("40233271", restored.DeviceId);
        Assert.Equal(8000, restored.ExposureTimeUs);
        Assert.Equal(3.5, restored.Gain);
        Assert.Equal(2500, restored.GrabTimeoutMs);
    }

    [Fact]
    public void Save_NullExposureAndGain_AreOmitted()
    {
        var cfg = new AppConfig();
        var store = new CameraConfigStore(cfg, _file);

        store.Save([new CameraConfig { Id = "cam_basler", Type = "Basler" }]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        var camera = doc.RootElement.GetProperty("Cameras")[0];
        Assert.False(camera.TryGetProperty("ExposureTimeUs", out _));
        Assert.False(camera.TryGetProperty("Gain", out _));
    }

    [Fact]
    public void EnumerateDevices_NeverThrows()
    {
        // 无 pylon 运行库的机器（如开发机）返回空列表而不是崩溃
        var devices = BaslerCamera.EnumerateDevices();
        Assert.NotNull(devices);
    }
}
