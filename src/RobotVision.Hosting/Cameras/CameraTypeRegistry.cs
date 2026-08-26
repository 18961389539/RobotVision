using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;

namespace RobotVision.Hosting.Cameras;

/// <summary>
/// 相机工厂注册表：Type 字符串 → 工厂的映射。
/// 静态 <see cref="Default"/> 预注册内置类型（File/Basler/GigEVision/Virtual），
/// 第三方品牌接入时在启动早期调用 <c>CameraTypeRegistry.Default.Register(new HikCameraFactory())</c>
/// 一行接入——服务注册循环、UI 类型下拉、测试取图、运行时注册均从本表查询，
/// 加新相机品牌不再改任何分支代码。
/// </summary>
public sealed class CameraTypeRegistry
{
    /// <summary>进程级默认注册表：内置工厂已注册，第三方可追加。供 DI 与 UI 共用同一实例。</summary>
    public static CameraTypeRegistry Default { get; } = CreateDefault();

    private readonly Dictionary<string, ICameraFactory> _factories = new(StringComparer.OrdinalIgnoreCase);
    private string[]? _cachedTypeNames;

    /// <summary>预置内置类型工厂（File / Basler / GigEVision / Virtual）。</summary>
    public static CameraTypeRegistry CreateDefault()
    {
        var registry = new CameraTypeRegistry();
        registry.Register(new FileCameraFactory());
        registry.Register(new BaslerCameraFactory());
        registry.Register(new GigEVisionCameraFactory());
        registry.Register(new VirtualCameraFactory());
        return registry;
    }

    /// <summary>注册/覆盖工厂（同 TypeName 后者生效，便于宿主定制内置类型行为）。</summary>
    public void Register(ICameraFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (string.IsNullOrWhiteSpace(factory.TypeName))
            throw new ArgumentException("相机工厂 TypeName 不能为空", nameof(factory));
        _factories[factory.TypeName] = factory;
        _cachedTypeNames = null;
    }

    /// <summary>按类型查工厂。</summary>
    public bool TryGet(string type, out ICameraFactory factory) =>
        _factories.TryGetValue(type, out factory!);

    /// <summary>类型是否已注册工厂（UI 据此决定展示专用编辑面板还是通用提示）。</summary>
    public bool IsKnown(string type) =>
        !string.IsNullOrWhiteSpace(type) && _factories.ContainsKey(type);

    /// <summary>已注册类型名列表（UI 类型下拉数据源；按名称排序保证稳定）。</summary>
    public IReadOnlyList<string> TypeNames =>
        Array.AsReadOnly(_cachedTypeNames ??= _factories.Keys
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    /// <summary>按配置创建相机；类型未注册工厂时返回 null（不抛异常，调用方决定告警/跳过）。</summary>
    public ICamera? Create(CameraConfig config, ILogger? logger = null)
    {
        if (config is null || !_factories.TryGetValue(config.Type, out var factory))
            return null;
        return factory.Create(config, logger);
    }

    /// <summary>查询类型是否支持设备枚举（UI 设备下拉可用性）。</summary>
    public bool CanEnumerateDevices(string type) =>
        _factories.TryGetValue(type, out var factory) && factory is IDeviceEnumerableFactory;

    /// <summary>枚举类型可连接设备；不支持时返回空列表。</summary>
    public IReadOnlyList<string> EnumerateDevices(string type)
    {
        if (_factories.TryGetValue(type, out var factory) && factory is IDeviceEnumerableFactory enumerable)
            return enumerable.EnumerateDevices();
        return [];
    }
}
