using RobotVision.Core.Models;

namespace RobotVision.Core.Recipe;

/// <summary>角度计算模式。</summary>
public enum AngleMode
{
    /// <summary>单分割模型：最小外接矩形长边方向（[0,180)，存在 180° 歧义）。</summary>
    MaskMinAreaRect,

    /// <summary>双检测模型：两个目标中心的连线（方向由模型 A→B 唯一确定）。</summary>
    DualCenterLine,

    /// <summary>关键点模型：单模型两个关键点的连线。</summary>
    KeyPointLine,
}

/// <summary>旋转中心补偿模式。</summary>
public enum RotationCompensationMode
{
    /// <summary>不补偿：工具与旋转轴共轴，或机器人侧自行补偿。</summary>
    None = 0,

    /// <summary>
    /// 偏心工具补偿：输出位置绕旋转轴心反转零件角，角度不变。
    /// 机器人先移动到输出位置，再旋转第 4 轴到输出角度，工具尖端恰好落在零件上。
    /// 需工位已完成旋转中心标定（CalibTool rotation）。
    /// </summary>
    EccentricTool = 1,
}

/// <summary>配方：一次 TRIGGER 触发所需的全部参数。</summary>
public sealed class RecipeConfig
{
    /// <summary>配置结构版本（未来字段演进时用于迁移判断）。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>配方用途备注（管理台展示，不参与推理）。</summary>
    public string Description { get; set; } = "";

    /// <summary>启用开关：false 时配方不可触发（返回 1001），文件保留不删。</summary>
    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = "";

    /// <summary>相机 Id（对应 appsettings 中注册的相机）。</summary>
    public string CameraId { get; set; } = "";

    /// <summary>工位 Id（外参档案）。必需；台架调试需像素直通时设置 DebugPassthrough。</summary>
    public string? StationId { get; set; }

    /// <summary>
    /// 台架调试模式：显式开启后允许 stationId 为空，返回像素坐标。
    /// 关闭（默认）时缺 stationId 直接报错，防止像素坐标被误当成机器人坐标。
    /// </summary>
    public bool DebugPassthrough { get; set; }

    public AngleMode AngleMode { get; set; }

    /// <summary>模型文件名（相对 models 目录，也可写绝对路径）。双模型模式需要 2 个。</summary>
    public List<string> Models { get; set; } = [];

    public double Confidence { get; set; } = 0.5;

    public double Iou { get; set; } = 0.7;

    /// <summary>分割掩码的像素置信度阈值（仅 MaskMinAreaRect 模式使用）。</summary>
    public double PixelConfidence { get; set; } = 0.65;

    /// <summary>关键点索引（仅 KeyPointLine 模式）。</summary>
    public int KeypointIndexA { get; set; } = 0;

    public int KeypointIndexB { get; set; } = 1;

    /// <summary>关键点最低置信度，低于该值的关键点视为无效。</summary>
    public double KeypointMinConfidence { get; set; } = 0.3;

    /// <summary>双模型配对时允许的最大中心距离（px），超过不配对。</summary>
    public double PairingMaxDistancePx { get; set; } = 800;

    /// <summary>检测区域（相对比例 0~1，X/Y 左上角、W/H 宽高）；null = 全图推理。
    /// 大图上只检测局部区域可显著降低 CPU 推理耗时。</summary>
    public Roi? Roi { get; set; }

    /// <summary>旋转中心补偿（默认关闭）。开启前需 CalibTool rotation 完成轴心标定。</summary>
    public RotationCompensationMode RotationCompensation { get; set; } = RotationCompensationMode.None;

    /// <summary>光源控制器 Id（对应 appsettings 中注册的光源）；空 = 不亮灯。</summary>
    public string? LightControllerId { get; set; }

    /// <summary>照明配置（通道/亮度/稳定延时/取图后熄灯）；配了 LightControllerId 才生效。</summary>
    public LightingConfig? Lighting { get; set; }

    /// <summary>深拷贝（Models 列表独立），供编辑器副本与缓存隔离使用。</summary>
    public RecipeConfig Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        Description = Description,
        Enabled = Enabled,
        Name = Name,
        CameraId = CameraId,
        StationId = StationId,
        DebugPassthrough = DebugPassthrough,
        AngleMode = AngleMode,
        Models = [.. Models],
        Confidence = Confidence,
        Iou = Iou,
        PixelConfidence = PixelConfidence,
        KeypointIndexA = KeypointIndexA,
        KeypointIndexB = KeypointIndexB,
        KeypointMinConfidence = KeypointMinConfidence,
        PairingMaxDistancePx = PairingMaxDistancePx,
        Roi = Roi,
        RotationCompensation = RotationCompensation,
        LightControllerId = LightControllerId,
        Lighting = Lighting?.Clone(),
    };
}
