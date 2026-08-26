using System.Text.Json;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// GigEVision 接入的纯托管测试：配置往返、工厂注册、枚举失败不抛。
/// 真实 GVSP 取图需在网口相机与 UDP 可达的机器上验证。
/// </summary>
public class GigEVisionCameraTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "rv_gige_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try { File.Delete(_file); }
        catch (IOException) { }
    }

    [Fact]
    public void Registry_IncludesGigEVision()
    {
        var registry = CameraTypeRegistry.CreateDefault();
        Assert.True(registry.IsKnown("GigEVision"));
        Assert.True(registry.CanEnumerateDevices("GigEVision"));
    }

    [Fact]
    public void Save_RoundTripsGigEVisionFields()
    {
        var cfg = new AppConfig();
        var store = new CameraConfigStore(cfg, _file);

        store.Save([new CameraConfig
        {
            Id = "cam_basler",
            Type = "GigEVision",
            DeviceId = "169.254.48.12",
            ExposureTimeUs = 8000,
            Gain = 3.5,
            GrabTimeoutMs = 2500,
        }]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        var camera = doc.RootElement.GetProperty("Cameras")[0];
        Assert.Equal("GigEVision", camera.GetProperty("Type").GetString());
        Assert.Equal("169.254.48.12", camera.GetProperty("DeviceId").GetString());
        Assert.Equal(8000, camera.GetProperty("ExposureTimeUs").GetDouble());
        Assert.Equal(3.5, camera.GetProperty("Gain").GetDouble());
        Assert.Equal(2500, camera.GetProperty("GrabTimeoutMs").GetInt32());
    }

    [Fact]
    public void EnumerateDevices_NeverThrows()
    {
        var devices = GigEVisionCamera.EnumerateDevices();
        Assert.NotNull(devices);
    }
}
