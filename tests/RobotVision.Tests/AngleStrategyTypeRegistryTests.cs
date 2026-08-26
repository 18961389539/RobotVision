using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 角度策略工厂注册表测试：默认预置五种模式、注册/覆盖、未注册查询、
/// 模式列表排序、按模式创建。与光源/相机注册表同构。
/// </summary>
public class AngleStrategyTypeRegistryTests
{
    /// <summary>测试用假工厂：返回记录型策略。</summary>
    private sealed class FakeFactory(AngleMode mode, string label) : IAngleStrategyFactory
    {
        public AngleMode Mode { get; } = mode;

        public string Label { get; } = label;

        public IAngleStrategy Create(ModelManager models, ILogger? logger = null) =>
            new FakeStrategy(Mode);
    }

    /// <summary>测试用记录型策略。</summary>
    private sealed class FakeStrategy(AngleMode mode) : IAngleStrategy
    {
        public AngleMode Mode { get; } = mode;

        public List<PixelPose> Compute(VisionImage undistorted, RecipeConfig recipe, CancellationToken ct = default) =>
            [];
    }

    private static ModelManager Models => new(Path.GetTempPath());

    [Fact]
    public void Default_ContainsAllBuiltInModes()
    {
        var registry = AngleStrategyTypeRegistry.Default;

        Assert.True(registry.IsKnown(AngleMode.MaskMinAreaRect));
        Assert.True(registry.IsKnown(AngleMode.DualCenterLine));
        Assert.True(registry.IsKnown(AngleMode.KeyPointLine));
        Assert.True(registry.IsKnown(AngleMode.MaskTemplate));
        Assert.True(registry.IsKnown(AngleMode.DualBlobCenterLine));
        Assert.Equal(5, registry.Modes.Count);
    }

    [Fact]
    public void Default_Factories_HaveLabels()
    {
        var factories = AngleStrategyTypeRegistry.Default.Factories;

        Assert.All(factories, f => Assert.False(string.IsNullOrWhiteSpace(f.Label)));
        // 三种内置模式的 Label 各不相同（UI 下拉可区分）
        Assert.Equal(factories.Select(f => f.Label).Distinct().Count(), factories.Count);
    }

    [Fact]
    public void Default_Create_ReturnsConcreteStrategyPerMode()
    {
        var registry = AngleStrategyTypeRegistry.Default;
        var models = Models;

        Assert.IsType<MaskMinAreaRectStrategy>(registry.Create(AngleMode.MaskMinAreaRect, models));
        Assert.IsType<DualCenterLineStrategy>(registry.Create(AngleMode.DualCenterLine, models));
        Assert.IsType<KeyPointLineStrategy>(registry.Create(AngleMode.KeyPointLine, models));
        Assert.IsType<MaskTemplateStrategy>(registry.Create(AngleMode.MaskTemplate, models));
        Assert.IsType<DualBlobCenterLineStrategy>(registry.Create(AngleMode.DualBlobCenterLine, models));
    }

    [Fact]
    public void Register_AddsFactory_AndModesReflects()
    {
        var registry = new AngleStrategyTypeRegistry();
        registry.Register(new FakeFactory(AngleMode.KeyPointLine, "假策略"));

        Assert.True(registry.IsKnown(AngleMode.KeyPointLine));
        Assert.Equal([AngleMode.KeyPointLine], registry.Modes);
        Assert.Single(registry.Factories);
    }

    [Fact]
    public void Register_OverridesExistingFactory_ByMode()
    {
        var registry = new AngleStrategyTypeRegistry();
        registry.Register(new MaskMinAreaRectStrategyFactory());
        registry.Register(new FakeFactory(AngleMode.MaskMinAreaRect, "覆盖版"));

        Assert.True(registry.TryGet(AngleMode.MaskMinAreaRect, out var factory));
        Assert.IsType<FakeFactory>(factory);
        Assert.Equal("覆盖版", factory.Label);
    }

    [Fact]
    public void TryGet_UnknownMode_ReturnsFalse()
    {
        var registry = new AngleStrategyTypeRegistry();
        Assert.False(registry.TryGet((AngleMode)99, out _));
        Assert.False(registry.IsKnown((AngleMode)99));
    }

    [Fact]
    public void Register_NullFactory_Throws()
    {
        var registry = new AngleStrategyTypeRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void Create_UnknownMode_ReturnsNull()
    {
        var registry = new AngleStrategyTypeRegistry();
        Assert.Null(registry.Create((AngleMode)99, Models));
    }

    [Fact]
    public void Create_Delegates_ToRegisteredFactory()
    {
        var registry = new AngleStrategyTypeRegistry();
        registry.Register(new FakeFactory(AngleMode.KeyPointLine, "假策略"));

        var strategy = registry.Create(AngleMode.KeyPointLine, Models, NullLogger.Instance);

        var fake = Assert.IsType<FakeStrategy>(strategy);
        Assert.Equal(AngleMode.KeyPointLine, fake.Mode);
    }

    [Fact]
    public void Modes_AreOrderedByEnumValue()
    {
        var registry = new AngleStrategyTypeRegistry();
        registry.Register(new KeyPointLineStrategyFactory()); // KeyPointLine = 2
        registry.Register(new MaskMinAreaRectStrategyFactory()); // MaskMinAreaRect = 0
        registry.Register(new DualCenterLineStrategyFactory()); // DualCenterLine = 1

        Assert.Equal(
            [AngleMode.MaskMinAreaRect, AngleMode.DualCenterLine, AngleMode.KeyPointLine],
            registry.Modes);
    }
}
