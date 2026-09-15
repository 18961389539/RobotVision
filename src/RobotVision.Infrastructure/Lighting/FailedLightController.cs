using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 初始化失败的占位光源：保持 Id 已注册（配方引用校验通过），Apply/TurnOff 抛 1006。
/// 避免"构造失败 → 未注册 → TRIGGER 返回 1001"。
/// </summary>
public sealed class FailedLightController(string id, string message) : ILightController
{
    private readonly string _message = string.IsNullOrWhiteSpace(message)
        ? $"光源控制器 {id} 初始化失败"
        : message;

    public string Id { get; } = id;

    public LightControllerKind Kind => LightControllerKind.Virtual;

    public bool Apply(LightingConfig lighting)
    {
        throw new VisionException(VisionErrorCode.LightNotRegistered, _message);
    }

    /// <summary>永远失败——占位光源绝不能"静默成功"，否则配方会以为灯关了。</summary>
    public bool TurnOff() =>
        throw new VisionException(VisionErrorCode.LightNotRegistered, _message);

    /// <summary>永远失败——占位光源绝不能"静默成功"，否则调试框会谎报已发送。</summary>
    public bool SendRaw(string command) =>
        throw new VisionException(VisionErrorCode.LightNotRegistered, _message);

    public void Dispose()
    {
    }
}
