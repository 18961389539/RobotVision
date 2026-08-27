using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 相机管理（CameraManager）测试：
/// - 注册/注销/查询：Count、CameraIds（含缓存失效）、TryGet、IsRegistered；
/// - 同 Id 覆盖注册释放旧实例；Unregister 释放实例并返回存在性；
/// - Get 未注册抛 CameraNotRegistered；Dispose 释放全部相机且幂等；
/// - 取图按相机 Id 串行：并发 Grab 不得同时进入同一相机的 Grab（SDK 非线程安全）。
/// </summary>
public class CameraManagerTests
{
    /// <summary>可控行为假相机：记录释放次数，可注入 Grab 钩子。</summary>
    private sealed class FakeCamera(string id) : ICamera
    {
        public string Id { get; } = id;

        public CameraKind Kind => CameraKind.Virtual;

        public int DisposedCount;

        public Func<CancellationToken, CameraFrame>? OnGrab;

        public CameraFrame Grab(CancellationToken ct = default) =>
            OnGrab?.Invoke(ct) ?? throw new InvalidOperationException("未配置 OnGrab");

        public void Dispose() => DisposedCount++;
    }

    [Fact]
    public void Register_AddsCamera_AndListsId()
    {
        using var manager = new CameraManager();
        var camera = new FakeCamera("cam1");

        manager.Register(camera);

        Assert.Equal(1, manager.Count);
        Assert.Contains("cam1", manager.CameraIds);
        Assert.True(manager.IsRegistered("cam1"));
        Assert.Equal(0, camera.DisposedCount);
    }

    [Fact]
    public void Register_SameId_DisposesOldInstance()
    {
        using var manager = new CameraManager();
        var old = new FakeCamera("cam1");
        var fresh = new FakeCamera("cam1");
        manager.Register(old);

        manager.Register(fresh);

        Assert.Equal(1, manager.Count);
        Assert.Equal(1, old.DisposedCount);   // 旧实例被释放（运行时重配相机）
        Assert.Equal(0, fresh.DisposedCount);
        Assert.Same(fresh, manager.Get("cam1"));
    }

    [Fact]
    public void Register_SameInstanceTwice_DoesNotDispose()
    {
        using var manager = new CameraManager();
        var camera = new FakeCamera("cam1");
        manager.Register(camera);

        manager.Register(camera); // 同一实例重复注册：ReferenceEquals 短路，不释放

        Assert.Equal(0, camera.DisposedCount);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Register_Null_Throws()
    {
        using var manager = new CameraManager();
        Assert.Throws<ArgumentNullException>(() => manager.Register(null!));
    }

    [Fact]
    public void Unregister_Existing_ReturnsTrue_AndDisposes()
    {
        using var manager = new CameraManager();
        var camera = new FakeCamera("cam1");
        manager.Register(camera);

        var removed = manager.Unregister("cam1");

        Assert.True(removed);
        Assert.Equal(0, manager.Count);
        Assert.False(manager.IsRegistered("cam1"));
        Assert.Equal(1, camera.DisposedCount);
    }

    [Fact]
    public void Unregister_Missing_ReturnsFalse()
    {
        using var manager = new CameraManager();
        Assert.False(manager.Unregister("ghost"));
    }

    [Fact]
    public void Get_Registered_ReturnsCamera()
    {
        using var manager = new CameraManager();
        var camera = new FakeCamera("cam1");
        manager.Register(camera);

        Assert.Same(camera, manager.Get("cam1"));
    }

    [Fact]
    public void Get_Missing_ThrowsCameraNotRegistered()
    {
        using var manager = new CameraManager();
        var ex = Assert.Throws<VisionException>(() => manager.Get("ghost"));
        Assert.Equal(VisionErrorCode.CameraNotRegistered, ex.ErrorCode);
    }

    [Fact]
    public void TryGet_Registered_ReturnsTrue_AndCamera()
    {
        using var manager = new CameraManager();
        var camera = new FakeCamera("cam1");
        manager.Register(camera);

        Assert.True(manager.TryGet("cam1", out var found));
        Assert.Same(camera, found);
    }

    [Fact]
    public void TryGet_Missing_ReturnsFalse()
    {
        using var manager = new CameraManager();
        Assert.False(manager.TryGet("ghost", out var found));
        Assert.Null(found);
    }

    [Fact]
    public void CameraIds_AreCaseInsensitive()
    {
        using var manager = new CameraManager();
        manager.Register(new FakeCamera("CAM-A"));

        Assert.True(manager.IsRegistered("cam-a"));
        Assert.NotNull(manager.Get("Cam-A"));
    }

    [Fact]
    public void CameraIds_CacheInvalidated_AfterRegisterAndUnregister()
    {
        using var manager = new CameraManager();
        var camera = new FakeCamera("cam1");
        manager.Register(camera);
        Assert.Contains("cam1", manager.CameraIds);

        manager.Register(new FakeCamera("cam2"));
        Assert.Contains("cam2", manager.CameraIds); // 缓存失效后重新枚举

        manager.Unregister("cam1");
        Assert.DoesNotContain("cam1", manager.CameraIds);
    }

    [Fact]
    public void Grab_SerializedByCameraId_NoConcurrentEntry()
    {
        using var manager = new CameraManager();
        var active = 0;
        var violations = 0;
        var camera = new FakeCamera("cam1")
        {
            OnGrab = _ =>
            {
                // 同一相机同时进入 Grab 即违反按 Id 串行约束（SDK 非线程安全）
                if (Interlocked.Increment(ref active) > 1)
                    Interlocked.Increment(ref violations);
                Thread.Sleep(50);
                Interlocked.Decrement(ref active);
                return new CameraFrame(VisionImage.AllocateZero(4, 4, 3), DateTime.UtcNow);
            },
        };
        manager.Register(camera);

        Parallel.For(0, 4, _ => manager.Grab("cam1").Dispose());

        Assert.Equal(0, violations);
        Assert.Equal(1, manager.Count);              // 取图不影响注册状态
        Assert.Equal(0, camera.DisposedCount);       // Grab 不释放相机实例
    }

    [Fact]
    public void Grab_Unregistered_Throws()
    {
        using var manager = new CameraManager();
        Assert.Throws<VisionException>(() => manager.Grab("ghost"));
    }

    [Fact]
    public async Task GrabAsync_ReturnsFrame()
    {
        using var manager = new CameraManager();
        var camera = new FakeCamera("cam1")
        {
            OnGrab = _ => new CameraFrame(VisionImage.AllocateZero(8, 8, 3), DateTime.UtcNow),
        };
        manager.Register(camera);

        using var frame = await manager.GrabAsync("cam1");
        Assert.Equal(8, frame.Image.Width);
    }

    [Fact]
    public async Task GrabTracedAsync_SeparatesGateWaitFromGrab()
    {
        using var manager = new CameraManager();
        using var enteredGrab = new ManualResetEventSlim(false);
        using var releaseGrab = new ManualResetEventSlim(false);
        var camera = new FakeCamera("cam1")
        {
            OnGrab = _ =>
            {
                enteredGrab.Set();
                releaseGrab.Wait();
                return new CameraFrame(VisionImage.AllocateZero(4, 4, 3), DateTime.UtcNow);
            },
        };
        manager.Register(camera);

        var first = manager.GrabTracedAsync("cam1");
        Assert.True(enteredGrab.Wait(TimeSpan.FromSeconds(2)), "首帧应进入 Grab");
        var second = manager.GrabTracedAsync("cam1");
        await Task.Delay(80);
        releaseGrab.Set();

        var r1 = await first;
        var r2 = await second;
        r1.Frame.Dispose();
        r2.Frame.Dispose();

        Assert.True(r1.GateWaitMs < 40, $"首帧等锁应接近 0，实际 {r1.GateWaitMs:0}ms");
        Assert.True(r2.GateWaitMs > 30, $"次帧应等到首帧释放门闩，实际等锁 {r2.GateWaitMs:0}ms");
    }

    [Fact]
    public void Dispose_DisposesAllCameras_AndIsIdempotent()
    {
        var manager = new CameraManager();
        var a = new FakeCamera("a");
        var b = new FakeCamera("b");
        manager.Register(a);
        manager.Register(b);

        manager.Dispose();
        manager.Dispose(); // 幂等

        Assert.Equal(1, a.DisposedCount);
        Assert.Equal(1, b.DisposedCount);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Operations_AfterDispose_Throw()
    {
        var manager = new CameraManager();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() => manager.Register(new FakeCamera("x")));
        Assert.Throws<ObjectDisposedException>(() => manager.Unregister("x"));
    }
}
