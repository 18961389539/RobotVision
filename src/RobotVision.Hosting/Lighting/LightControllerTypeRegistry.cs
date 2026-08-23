using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;

namespace RobotVision.Hosting.Lighting;

/// <summary>
/// 光源控制器工厂注册表：Type 字符串 → 工厂的映射。
/// 静态 <see cref="Default"/> 预注册内置类型（None），第三方光源接入时在启动早期
/// 调用 <c>LightControllerTypeRegistry.Default.Register(new OptartLightFactory())</c>
/// 一行接入——服务注册、UI 类型下拉、运行时注册均从本表查询，
/// 加新光源品牌不再改任何分支代码。与 CameraTypeRegistry 同构。
/// </summary>
public sealed class LightControllerTypeRegistry
{
    /// <summary>进程级默认注册表：内置工厂已注册，第三方可追加。供 DI 与 UI 共用同一实例。</summary>
    public static LightControllerTypeRegistry Default { get; } = CreateDefault();

    private readonly Dictionary<string, ILightControllerFactory> _factories = new(StringComparer.OrdinalIgnoreCase);
    private string[]? _cachedTypeNames;

    /// <summary>预置内置类型工厂（None）。</summary>
    public static LightControllerTypeRegistry CreateDefault()
    {
        var registry = new LightControllerTypeRegistry();
        registry.Register(new NoneLightControllerFactory());
        return registry;
    }

    /// <summary>注册/覆盖工厂（同 TypeName 后者生效，便于宿主定制内置类型行为）。</summary>
    public void Register(ILightControllerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (string.IsNullOrWhiteSpace(factory.TypeName))
            throw new ArgumentException("光源控制器工厂 TypeName 不能为空", nameof(factory));
        _factories[factory.TypeName] = factory;
        _cachedTypeNames = null;
    }

    /// <summary>按类型查工厂。</summary>
    public bool TryGet(string type, out ILightControllerFactory factory) =>
        _factories.TryGetValue(type, out factory!);

    /// <summary>类型是否已注册工厂（UI 据此决定展示专用编辑面板还是通用提示）。</summary>
    public bool IsKnown(string type) =>
        !string.IsNullOrWhiteSpace(type) && _factories.ContainsKey(type);

    /// <summary>已注册类型名列表（UI 类型下拉数据源；按名称排序保证稳定）。</summary>
    public IReadOnlyList<string> TypeNames =>
        Array.AsReadOnly(_cachedTypeNames ??= _factories.Keys
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    /// <summary>按配置创建光源控制器；类型未注册工厂时返回 null（不抛异常，调用方决定告警/跳过）。</summary>
    public ILightController? Create(LightControllerConfig config, ILogger? logger = null)
    {
        if (config is null || !_factories.TryGetValue(config.Type, out var factory))
            return null;
        return factory.Create(config, logger);
    }
}
