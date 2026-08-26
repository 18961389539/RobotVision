using FluentAssertions;
using ImageViewer.Models;
using ImageViewer.Plugins;

namespace ImageViewerControl.Tests;

/// <summary>
/// ROI 插件注册表测试：内置插件注册（类型/类型键查找）、重复注册拦截、
/// 反注册、绘制工具排序、类型键大小写不敏感。
/// </summary>
public class RoiPluginRegistryTests
{
    private static RoiPluginRegistry CreateRegistry() =>
        // 内置插件经 CreateBuiltIn 注册（非模块发现）；Default 为共享单例，
        // 只读测试直接使用；变更加载的用例（Unregister/Register）自行恢复状态
        RoiPluginRegistry.Default;

    [Fact]
    public void BuiltInPlugins_RegisterCoreRoiTypes()
    {
        var registry = CreateRegistry();

        registry.FindByType(typeof(CircleRoi)).Should().NotBeNull();
        registry.FindByType(typeof(RingRoi)).Should().NotBeNull();
        registry.FindByType(typeof(PolygonRoi)).Should().NotBeNull();
        registry.FindByType(typeof(TextAnnotationRoi)).Should().NotBeNull();
        registry.FindByType(typeof(LineMeasureRoi)).Should().NotBeNull();
        registry.FindByType(typeof(RotatedRect)).Should().NotBeNull();
    }

    [Fact]
    public void FindByTypeKey_IsCaseInsensitive()
    {
        var registry = CreateRegistry();

        var plugin = registry.FindByType(typeof(CircleRoi));

        plugin.Should().NotBeNull();
        var byUpper = registry.FindByTypeKey(plugin!.TypeKey.ToUpperInvariant());
        var byLower = registry.FindByTypeKey(plugin.TypeKey.ToLowerInvariant());
        byUpper.Should().NotBeNull();
        byLower.Should().NotBeNull();
        byUpper.Should().BeSameAs(plugin);
    }

    [Fact]
    public void FindByRoi_ResolvesConcreteInstance()
    {
        var registry = CreateRegistry();
        var circle = new CircleRoi();

        registry.FindByRoi(circle).Should().NotBeNull();
    }

    [Fact]
    public void FindByTypeKey_Unknown_ReturnsNull() =>
        CreateRegistry().FindByTypeKey("no_such_type").Should().BeNull();

    [Fact]
    public void Register_DuplicateType_Throws()
    {
        var registry = CreateRegistry();
        var existing = registry.FindByType(typeof(CircleRoi));
        existing.Should().NotBeNull();

        var act = () => registry.Register(existing!);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Fact]
    public void Unregister_RemovesPlugin()
    {
        var registry = CreateRegistry();
        var plugin = registry.FindByType(typeof(CircleRoi));
        plugin.Should().NotBeNull();

        try
        {
            registry.Unregister(plugin!.TypeKey).Should().BeTrue();
            registry.FindByType(typeof(CircleRoi)).Should().BeNull();
            registry.Unregister(plugin.TypeKey).Should().BeFalse(); // 已移除
        }
        finally
        {
            registry.Register(plugin); // 恢复共享单例，避免污染其他测试
        }
    }

    [Fact]
    public void GetDrawingTools_ReturnsVisibleTools()
    {
        var registry = CreateRegistry();

        var tools = registry.GetDrawingTools();

        tools.Should().NotBeEmpty();
        // 工具按可见性/顺序整理，且不包含隐藏工具
        tools.Should().OnlyContain(t => t.IsVisible);
    }

    [Fact]
    public void GetPluginsInHitTestOrder_Descending()
    {
        var registry = CreateRegistry();

        var plugins = registry.GetPluginsInHitTestOrder().ToList();

        for (var i = 1; i < plugins.Count; i++)
            plugins[i - 1].HitTestOrder.Should().BeGreaterThanOrEqualTo(plugins[i].HitTestOrder);
    }

    [Fact]
    public void Register_Null_Throws() =>
        ((Action)(() => CreateRegistry().Register(null!))).Should().Throw<ArgumentNullException>();

    [Fact]
    public void FindByTypeKey_Whitespace_Throws() =>
        ((Action)(() => CreateRegistry().FindByTypeKey(" "))).Should().Throw<ArgumentException>();
}
