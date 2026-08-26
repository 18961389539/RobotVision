using System.Text.Json;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using OpenCvSharp;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 相机配置管理测试：CameraConfigStore 写回 appsettings.json
/// （保留其他节点、同步内存配置、文件不存在时新建），CameraManager 注册/覆盖/移除。
/// </summary>
public class CameraConfigStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "rv_camcfg_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try { File.Delete(_file); }
        catch (IOException) { }
    }

    [Fact]
    public void Save_PreservesOtherNodesAndWritesCameras()
    {
        File.WriteAllText(_file, """
            {
              "TcpPort": 8888,
              "Cameras": [
                { "Id": "cam_file", "Type": "File", "Folder": "data/replay" }
              ],
              "FailureImage": { "Enabled": true }
            }
            """);
        var cfg = new AppConfig { TcpPort = 8888 };
        var store = new CameraConfigStore(cfg, _file);

        store.Save(
        [
            new CameraConfig { Id = "cam_a", Type = "File", Folder = @"D:\imgs" },
            new CameraConfig { Id = "cam_b", Type = "File", Folder = @"D:\imgs2" },
        ]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        var root = doc.RootElement;
        Assert.Equal(8888, root.GetProperty("TcpPort").GetInt32());
        Assert.True(root.GetProperty("FailureImage").GetProperty("Enabled").GetBoolean());
        var cameras = root.GetProperty("Cameras").EnumerateArray().ToArray();
        Assert.Equal(2, cameras.Length);
        Assert.Equal("cam_a", cameras[0].GetProperty("Id").GetString());
        Assert.Equal(@"D:\imgs", cameras[0].GetProperty("Folder").GetString());
        Assert.Equal("cam_b", cameras[1].GetProperty("Id").GetString());
    }

    [Fact]
    public void Save_RoundTripsOptionalName()
    {
        var cfg = new AppConfig();
        var store = new CameraConfigStore(cfg, _file);

        store.Save([new CameraConfig { Id = "cam_a", Type = "File", Folder = "imgs", Name = "左工位" }]);

        Assert.Equal("左工位", cfg.Cameras[0].Name);
        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        Assert.Equal("左工位", doc.RootElement.GetProperty("Cameras")[0].GetProperty("Name").GetString());
    }

    [Fact]
    public void Save_SyncsInMemoryConfig()
    {
        var cfg = new AppConfig();
        cfg.Cameras.Add(new CameraConfig { Id = "old", Type = "File", Folder = "x" });
        var store = new CameraConfigStore(cfg, _file);

        store.Save([new CameraConfig { Id = "new", Type = "File", Folder = "y" }]);

        Assert.Single(cfg.Cameras);
        Assert.Equal("new", cfg.Cameras[0].Id);
        Assert.Equal("y", cfg.Cameras[0].Folder);
    }

    [Fact]
    public void Save_CreatesFileWhenMissing()
    {
        Assert.False(File.Exists(_file));
        var cfg = new AppConfig();
        var store = new CameraConfigStore(cfg, _file);

        store.Save([new CameraConfig { Id = "cam_x", Type = "File", Folder = "z" }]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        Assert.Single(doc.RootElement.GetProperty("Cameras").EnumerateArray());
    }

    [Fact]
    public void Save_EmptyList_ClearsNode()
    {
        File.WriteAllText(_file, """
            { "Cameras": [ { "Id": "a", "Type": "File", "Folder": "f" } ] }
            """);
        var cfg = new AppConfig();
        var store = new CameraConfigStore(cfg, _file);

        store.Save([]);

        using var doc = JsonDocument.Parse(File.ReadAllText(_file));
        Assert.Empty(doc.RootElement.GetProperty("Cameras").EnumerateArray());
        Assert.Empty(cfg.Cameras);
    }

    [Fact]
    public void Save_RootNotObject_Throws()
    {
        File.WriteAllText(_file, "[1, 2]");
        var store = new CameraConfigStore(new AppConfig(), _file);

        Assert.Throws<InvalidOperationException>(() => store.Save([]));
    }
}

/// <summary>可观察 Dispose 的相机桩。</summary>
file sealed class StubCamera(string id) : ICamera
{
    public string Id { get; } = id;
    public CameraKind Kind => CameraKind.File;
    public bool Disposed { get; private set; }
    public CameraFrame Grab(CancellationToken ct = default) =>
        new(VisionImageCv.FromMat(new Mat(4, 4, MatType.CV_8UC3, Scalar.All(0)), ownsMat: true), DateTime.UtcNow);
    public void Dispose() => Disposed = true;
}

public class CameraManagerLifecycleTests
{
    [Fact]
    public void Register_OverwritesAndDisposesOldInstance()
    {
        var manager = new CameraManager();
        var first = new StubCamera("cam");
        var second = new StubCamera("cam");
        manager.Register(first);
        manager.Register(second);

        Assert.True(manager.IsRegistered("cam"));
        Assert.True(first.Disposed);
        Assert.False(second.Disposed);
    }

    [Fact]
    public void Register_SameInstance_DoesNotSelfDispose()
    {
        var manager = new CameraManager();
        var cam = new StubCamera("cam");
        manager.Register(cam);
        manager.Register(cam);

        Assert.True(manager.IsRegistered("cam"));
        Assert.False(cam.Disposed, "重复注册同一实例不应自我释放");
    }

    [Fact]
    public void Unregister_RemovesAndDisposes()
    {
        var manager = new CameraManager();
        var cam = new StubCamera("cam");
        manager.Register(cam);

        Assert.True(manager.Unregister("cam"));
        Assert.False(manager.IsRegistered("cam"));
        Assert.True(cam.Disposed);
        Assert.False(manager.Unregister("cam"));
    }

    [Fact]
    public void TryGet_ReturnsRegisteredCamera()
    {
        var manager = new CameraManager();
        var cam = new StubCamera("cam");
        manager.Register(cam);

        Assert.True(manager.TryGet("cam", out var got));
        Assert.Same(cam, got);
        Assert.False(manager.TryGet("missing", out var miss));
        Assert.Null(miss);
    }

    [Fact]
    public async Task GrabAsync_SameId_SerializesConcurrentCalls()
    {
        using var manager = new CameraManager();
        var tracker = new GrabTracker();
        var cam = new CountingCamera("cam", TimeSpan.FromMilliseconds(80), tracker);
        manager.Register(cam);

        var t1 = manager.GrabAsync("cam");
        var t2 = manager.GrabAsync("cam");
        var frames = await Task.WhenAll(t1, t2);
        foreach (var frame in frames)
            frame.Dispose();

        Assert.Equal(1, tracker.MaxInFlight);
        Assert.Equal(2, tracker.GrabCount);
    }

    [Fact]
    public async Task GrabAsync_TempCamera_SharesGateWithRegisteredId()
    {
        using var manager = new CameraManager();
        var tracker = new GrabTracker();
        var registered = new CountingCamera("cam", TimeSpan.FromMilliseconds(80), tracker);
        var temp = new CountingCamera("cam", TimeSpan.FromMilliseconds(80), tracker);
        manager.Register(registered);

        var t1 = manager.GrabAsync("cam");
        var t2 = manager.GrabAsync(temp);
        var frames = await Task.WhenAll(t1, t2);
        foreach (var frame in frames)
            frame.Dispose();

        Assert.Equal(1, tracker.MaxInFlight);
        Assert.Equal(2, tracker.GrabCount);
    }
}

/// <summary>跨实例共享的取图并发计数。</summary>
file sealed class GrabTracker
{
    private int _inFlight;
    private int _grabCount;

    public int MaxInFlight { get; private set; }

    public int GrabCount => _grabCount;

    public void Enter()
    {
        var n = Interlocked.Increment(ref _inFlight);
        lock (this)
            MaxInFlight = Math.Max(MaxInFlight, n);
        Interlocked.Increment(ref _grabCount);
    }

    public void Leave() => Interlocked.Decrement(ref _inFlight);
}

/// <summary>慢取图桩：经共享 tracker 记录并发 Grab 峰值。</summary>
file sealed class CountingCamera(string id, TimeSpan delay, GrabTracker tracker) : ICamera
{
    public string Id { get; } = id;
    public CameraKind Kind => CameraKind.File;

    public CameraFrame Grab(CancellationToken ct = default)
    {
        tracker.Enter();
        try
        {
            Thread.Sleep(delay);
            return new CameraFrame(
                VisionImageCv.FromMat(new Mat(4, 4, MatType.CV_8UC3, Scalar.All(0)), ownsMat: true),
                DateTime.UtcNow);
        }
        finally
        {
            tracker.Leave();
        }
    }

    public void Dispose() { }
}
