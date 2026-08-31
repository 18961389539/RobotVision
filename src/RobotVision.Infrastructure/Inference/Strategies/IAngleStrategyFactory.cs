using Microsoft.Extensions.Logging;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

/// <summary>
/// 角度策略工厂：一种 <see cref="AngleMode"/> 对应一个实现。
/// 新增模式时实现本接口并在
/// <c>AngleStrategyTypeRegistry.Register</c> 一行，服务注册、UI 角度模式下拉
/// 自动生效，不再改动 VisionService 与配方编辑分支——与 ICameraFactory 同构。
/// </summary>
public interface IAngleStrategyFactory
{
    /// <summary>策略模式标识（与 <see cref="RecipeConfig.AngleMode"/> 一致）。</summary>
    AngleMode Mode { get; }

    /// <summary>UI 下拉显示名（如 "关键点模型（两点连线角度）"）。</summary>
    string Label { get; }

    /// <summary>
    /// 按模型管理器创建策略实例。logger 可为 null（无日志场景）。
    /// 策略应保持无状态（实例可复用）；创建不应抛异常来报告"暂不可用"。
    /// </summary>
    IAngleStrategy Create(ModelManager models, ILogger? logger = null);
}
