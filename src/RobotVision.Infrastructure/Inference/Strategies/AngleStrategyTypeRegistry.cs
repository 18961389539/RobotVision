using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 角度策略工厂注册表：AngleMode → 工厂的映射。
/// 静态 <see cref="Default"/> 预注册内置三种策略（MaskMinAreaRect/DualCenterLine/KeyPointLine），
/// 新增角度模式时在启动早期调用 <c>AngleStrategyTypeRegistry.Default.Register(new XxxStrategyFactory())</c>
/// 一行接入——服务注册、UI 角度模式下拉、配方引用校验均从本表查询，
/// 加新模式不再改任何分支代码。与 CameraTypeRegistry 同构。
/// </summary>
public sealed class AngleStrategyTypeRegistry
{
    /// <summary>进程级默认注册表：内置工厂已注册，第三方可追加。供 DI 与 UI 共用同一实例。</summary>
    public static AngleStrategyTypeRegistry Default { get; } = CreateDefault();

    private readonly Dictionary<AngleMode, IAngleStrategyFactory> _factories = [];
    private AngleMode[]? _cachedModes;

    /// <summary>预置内置策略工厂（三种角度模式）。</summary>
    public static AngleStrategyTypeRegistry CreateDefault()
    {
        var registry = new AngleStrategyTypeRegistry();
        registry.Register(new MaskMinAreaRectStrategyFactory());
        registry.Register(new DualCenterLineStrategyFactory());
        registry.Register(new KeyPointLineStrategyFactory());
        return registry;
    }

    /// <summary>注册/覆盖工厂（同 Mode 后者生效，便于宿主定制内置策略行为）。</summary>
    public void Register(IAngleStrategyFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories[factory.Mode] = factory;
        _cachedModes = null;
    }

    /// <summary>按模式查工厂。</summary>
    public bool TryGet(AngleMode mode, out IAngleStrategyFactory factory) =>
        _factories.TryGetValue(mode, out factory!);

    /// <summary>模式是否已注册工厂（配方校验/UI 下拉据此判断是否可用）。</summary>
    public bool IsKnown(AngleMode mode) => _factories.ContainsKey(mode);

    /// <summary>已注册模式列表（UI 角度模式下拉数据源；按枚举序排序保证稳定）。</summary>
    public IReadOnlyList<AngleMode> Modes =>
        Array.AsReadOnly(_cachedModes ??= _factories.Keys.OrderBy(m => m).ToArray());

    /// <summary>已注册工厂列表（UI 显示名来自工厂 Label，而非硬编码枚举翻译）。</summary>
    public IReadOnlyList<IAngleStrategyFactory> Factories =>
        _factories.Values.OrderBy(f => f.Mode).ToArray();

    /// <summary>按模式创建策略；模式未注册工厂时返回 null（不抛异常，调用方决定告警/兜底）。</summary>
    public IAngleStrategy? Create(AngleMode mode, ModelManager models, ILogger? logger = null)
    {
        if (!_factories.TryGetValue(mode, out var factory))
            return null;
        return factory.Create(models, logger);
    }
}
