using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Lighting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 光源控制器工厂注册表测试：默认预置 None、注册/覆盖、未注册查询、
/// 类型列表排序、按配置创建。与相机注册表同构。
/// </summary>
public class LightControllerTypeRegistryTests
{
    /// <summary>测试用假工厂：返回记录型控制器。</summary>
    private sealed class FakeFactory : ILightControllerFactory
    {
        public FakeFactory(string typeName) => TypeName = typeName;

        public string TypeName { get; }

        public ILightController Create(LightControllerConfig config, ILogger? logger = null) =>
            new FakeLight(config.Id, TypeName);
    }

    /// <summary>测试用记录型控制器。</summary>
    private sealed class FakeLight(string id, string type) : ILightController
    {
        public string Id { get; } = id;

        public LightControllerKind Kind => LightControllerKind.Virtual;

        public string CreatedBy { get; } = type;

        public void Apply(LightingConfig lighting)
        {
        }

        public void TurnOff()
        {
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void Default_ContainsNoneFactory()
    {
        var registry = LightControllerTypeRegistry.Default;

        Assert.True(registry.IsKnown("None"));
        Assert.True(registry.TryGet("none", out var factory));
        Assert.IsType<NoneLightControllerFactory>(factory);
    }

    [Fact]
    public void Register_AddsFactory_AndTypeNamesAreSorted()
    {
        var registry = new LightControllerTypeRegistry();
        registry.Register(new NoneLightControllerFactory());
        registry.Register(new FakeFactory("Optart"));
        registry.Register(new FakeFactory("Serial"));

        Assert.True(registry.IsKnown("Optart"));
        Assert.Equal(["None", "Optart", "Serial"], registry.TypeNames);
    }

    [Fact]
    public void Register_OverridesExistingFactory_ByCaseInsensitiveTypeName()
    {
        var registry = new LightControllerTypeRegistry();
        registry.Register(new NoneLightControllerFactory());
        registry.Register(new FakeFactory("None"));

        Assert.True(registry.TryGet("NONE", out var factory));
        Assert.IsType<FakeFactory>(factory);
    }

    [Fact]
    public void TryGet_UnknownType_ReturnsFalse()
    {
        var registry = new LightControllerTypeRegistry();
        Assert.False(registry.TryGet("Modbus", out _));
        Assert.False(registry.IsKnown(""));
        Assert.False(registry.IsKnown(null!));
    }

    [Fact]
    public void Register_NullOrEmptyTypeName_Throws()
    {
        var registry = new LightControllerTypeRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
        Assert.Throws<ArgumentException>(() => registry.Register(new FakeFactory("")));
    }

    [Fact]
    public void Create_KnownType_ReturnsInstance_WithConfigId()
    {
        var registry = LightControllerTypeRegistry.Default;
        var light = registry.Create(new LightControllerConfig { Id = "light_ring", Type = "None" },
            NullLogger.Instance);

        var noop = Assert.IsType<NoopLightController>(light);
        Assert.Equal("light_ring", noop.Id);
    }

    [Fact]
    public void Create_UnknownType_ReturnsNull()
    {
        var registry = LightControllerTypeRegistry.Default;
        Assert.Null(registry.Create(new LightControllerConfig { Id = "l1", Type = "Modbus" }));
    }

    [Fact]
    public void Create_RegistryDelegates_ToRegisteredFactory()
    {
        var registry = new LightControllerTypeRegistry();
        registry.Register(new FakeFactory("Optart"));

        var light = registry.Create(new LightControllerConfig { Id = "light_a", Type = "Optart" });

        var fake = Assert.IsType<FakeLight>(light);
        Assert.Equal("light_a", fake.Id);
        Assert.Equal("Optart", fake.CreatedBy);
    }

    [Fact]
    public void Create_NoneFactory_RejectsEmptyId()
    {
        var registry = LightControllerTypeRegistry.Default;
        Assert.Throws<ArgumentException>(() =>
            registry.Create(new LightControllerConfig { Id = "  ", Type = "None" }));
    }
}
