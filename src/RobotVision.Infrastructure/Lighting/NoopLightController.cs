using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 无操作光源控制器：配方已配置照明但现场尚未接线时的调试兜底。
/// 与 FileCamera 的定位一致——上层流程不感知"没有灯"，只是什么都不发生。
/// </summary>
public sealed class NoopLightController(string id) : ILightController
{
    public string Id { get; } = id;

    public LightControllerKind Kind => LightControllerKind.None;

    public void Apply(LightingConfig lighting)
    {
        // 无操作：不点亮任何硬件
    }

    public void TurnOff()
    {
    }

    public void Dispose()
    {
    }
}
