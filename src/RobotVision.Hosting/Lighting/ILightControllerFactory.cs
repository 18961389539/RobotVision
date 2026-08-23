using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;

namespace RobotVision.Hosting.Lighting;

/// <summary>
/// 光源控制器类型工厂：按 <see cref="LightControllerConfig.Type"/> 创建控制器实例的扩展点。
/// 接入真实光源（奥普特/康耐视等，串口/Modbus/TCP）＝新建一个实现类 + 调用
/// <c>LightControllerTypeRegistry.Register</c> 一行，服务注册、UI 类型下拉、
/// 运行时注册全部自动生效，不再改动核心与 UI 分支——与 ICameraFactory 同构。
/// </summary>
public interface ILightControllerFactory
{
    /// <summary>光源类型标识（与 LightControllerConfig.Type / appsettings.json 中 Type 字段一致，不区分大小写）。</summary>
    string TypeName { get; }

    /// <summary>
    /// 按配置创建光源控制器实例。logger 可为 null（UI 临时测试等无日志场景）。
    /// 创建不应抛异常来报告"暂不可用"——未接硬件时用 NoopLightController 兜底；
    /// 参数校验失败抛 VisionException。
    /// </summary>
    ILightController Create(LightControllerConfig config, ILogger? logger = null);
}
