using System.Text.Json;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 光源配置持久化测试：LightingConfigStore 写回 appsettings.json 的 LightControllers 节点
/// （保留其他节点、同步内存配置、文件不存在时新建）。
/// </summary>
public class LightingConfigStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "rv_lightcfg_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try { File.Delete(_file); }
        catch (IOException) { }
    }

    [Fact]
    public void Save_PreservesOtherNodesAndWritesLightControllers()
    {
        File.WriteAllText(_file, """
            {
              "TcpPort": 9999,
              "Cameras": [ { "Id": "cam_file", "Type": "File" } ],
              "LightControllers": [ { "Id": "light_old", "Type": "None" } ]
            }
            """);
        var cfg = new AppConfig { TcpPort = 9999 };
        var store = new LightingConfigStore(cfg, _file);

        store.Save([new LightControllerConfig { Id = "light_ring", Type = "None" }]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        var root = doc.RootElement;
        Assert.Equal(9999, root.GetProperty("TcpPort").GetInt32());
        Assert.True(root.GetProperty("Cameras").EnumerateArray().Any());
        var lights = root.GetProperty("LightControllers").EnumerateArray().ToArray();
        Assert.Single(lights);
        Assert.Equal("light_ring", lights[0].GetProperty("Id").GetString());
        Assert.Equal("None", lights[0].GetProperty("Type").GetString());

        // 内存配置同步
        Assert.Single(cfg.LightControllers);
        Assert.Equal("light_ring", cfg.LightControllers[0].Id);
    }

    [Fact]
    public void Save_CreatesFileWhenMissing()
    {
        Assert.False(File.Exists(_file));
        var store = new LightingConfigStore(new AppConfig(), _file);

        store.Save([new LightControllerConfig { Id = "light_a", Type = "None" }]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        Assert.Single(doc.RootElement.GetProperty("LightControllers").EnumerateArray());
    }
}
