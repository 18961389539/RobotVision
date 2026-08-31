namespace RobotVision.Core.Models;

/// <summary>单路光源通道配置。</summary>
public sealed class LightingChannelConfig
{
    /// <summary>通道号（≥1，取决于控制器通道数）。</summary>
    public int Channel { get; set; } = 1;

    /// <summary>亮度 0~255。</summary>
    public int Brightness { get; set; } = 128;

    public LightingChannelConfig Clone() => new()
    {
        Channel = Channel,
        Brightness = Brightness,
    };
}

/// <summary>
/// 配方照明配置：取图前点亮、稳定后取图。
/// 缺省（不填 lighting / 不填 lightControllerId）＝ 不亮灯，行为与旧版完全一致。
/// </summary>
public sealed class LightingConfig
{
    /// <summary>要点亮的通道列表；为空等效于不亮灯。</summary>
    public List<LightingChannelConfig> Channels { get; set; } = [];

    /// <summary>
    /// 点亮到取图之间的稳定延时（ms）。LED 常亮一般 0~20ms；
    /// 频闪模式须与相机硬触发曝光同步，此延时应设为 0。
    /// 延时计入单次 TRIGGER 超时预算。
    /// </summary>
    public int StabilizeDelayMs { get; set; }

    /// <summary>
    /// 取图完成后是否熄灯（默认 true）。常亮场景（光源与产线节拍联动）可设 false，
    /// 减少每帧开关灯带来的抖动与寿命损耗。
    /// </summary>
    public bool TurnOffAfterGrab { get; set; } = true;

    public LightingConfig Clone() => new()
    {
        Channels = [.. Channels.Select(c => c.Clone())],
        StabilizeDelayMs = StabilizeDelayMs,
        TurnOffAfterGrab = TurnOffAfterGrab,
    };
}
