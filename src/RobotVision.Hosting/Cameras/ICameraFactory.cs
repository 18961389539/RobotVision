using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;

namespace RobotVision.Hosting.Cameras;

/// <summary>
/// 相机类型工厂：按 <see cref="CameraConfig.Type"/> 创建相机实例的扩展点。
/// 接入新品牌（海康/大华等）＝新建一个实现类 + 调用 <c>CameraTypeRegistry.Register</c> 一行，
/// 服务注册、UI 类型下拉、测试取图、运行时注册全部自动生效，不再改动核心与 UI 分支。
/// </summary>
public interface ICameraFactory
{
    /// <summary>相机类型标识（与 CameraConfig.Type / appsettings.json 中 Type 字段一致，不区分大小写）。</summary>
    string TypeName { get; }

    /// <summary>
    /// 按配置创建相机实例。logger 可为 null（UI 临时测试取图等无日志场景）。
    /// 创建不应抛异常来报告"暂不可用"——懒连接相机（如 Basler）构造即成功，
    /// 首次取图时再连接/失败；参数校验失败抛 VisionException。
    /// </summary>
    ICamera Create(CameraConfig config, ILogger? logger = null);
}

/// <summary>
/// 可选能力：工厂支持枚举本类型可连接的设备（如 Basler 的序列号列表），
/// 供 UI 设备下拉框使用。UI 按类型查询该能力，不依赖具体品牌类。
/// </summary>
public interface IDeviceEnumerableFactory
{
    /// <summary>枚举可连接设备（UI 展示字符串列表）；失败抛异常由调用方处理。</summary>
    IReadOnlyList<string> EnumerateDevices();
}
