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

    /// <summary>分割+模板匹配：分割给粗角度与位置，模板匹配精修角度并消 180° 歧义。</summary>
    MaskTemplate,
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

/// <summary>
/// 关键点连线策略（KeyPointLine）专属参数。
/// 旧版平铺字段（keypointIndexA/keypointIndexB/keypointMinConfidence）在 RecipeConfig
/// 保留为 setter-only 兼容属性，读取旧配方 JSON 时自动迁移到本子对象，无需手工改文件。
/// </summary>
public sealed class KeypointOptions
{
    public int IndexA { get; set; }

    /// <summary>默认 1（与原平铺字段默认一致：0 与 1 不相等，避免校验误判）。</summary>
    public int IndexB { get; set; } = 1;

    /// <summary>关键点最低置信度，低于该值的关键点视为无效。</summary>
    public double MinConfidence { get; set; } = 0.3;

    public KeypointOptions Clone() => new()
    {
        IndexA = IndexA,
        IndexB = IndexB,
        MinConfidence = MinConfidence,
    };
}

/// <summary>
/// 双模型连线策略（DualCenterLine）专属参数。
/// 旧版平铺字段（pairingMaxDistancePx）在 RecipeConfig 保留为 setter-only 兼容属性。
/// </summary>
public sealed class DualModelOptions
{
    /// <summary>双模型配对时允许的最大中心距离（px），超过不配对。</summary>
    public double PairingMaxDistancePx { get; set; } = 800;

    public DualModelOptions Clone() => new()
    {
        PairingMaxDistancePx = PairingMaxDistancePx,
    };
}

/// <summary>
/// 分割策略（MaskMinAreaRect）专属参数。
/// 旧版平铺字段（pixelConfidence）在 RecipeConfig 保留为 setter-only 兼容属性。
/// </summary>
public sealed class SegmentationOptions
{
    /// <summary>分割掩码的像素置信度阈值。</summary>
    public double PixelConfidence { get; set; } = 0.65;

    public SegmentationOptions Clone() => new()
    {
        PixelConfidence = PixelConfidence,
    };
}

/// <summary>分割精修（MaskTemplate 模式）的精修方法。
/// Template = 原图模板匹配（吃纹理，可判头尾）；LineFit = 掩码长边鲁棒直线拟合
/// （吃轮廓几何，弱纹理矩形适用，无 180° 方向语义）。</summary>
public enum SegmentRefineMethod
{
    /// <summary>模板匹配：亚度精度，180° 头尾可判（需示教模板）。</summary>
    Template = 0,

    /// <summary>直线拟合：掩码两条长边各做鲁棒直线拟合取均值，抗离群点优于 minAreaRect；
    /// 弱纹理（光滑平面）矩形目标适用；角度无方向 [0,180)。</summary>
    LineFit = 1,

    /// <summary>质心-内标连线：掩码质心 → 最大内标（孔或槽）。圆孔走质心→孔心连线（指向孔）；
    /// 细长槽走长轴定角（精度高）+ 偏置侧别定头尾（±1 bit 判决，偏置弱也稳定）。有方向语义。</summary>
    CentroidHoleLine = 2,
}

/// <summary>
/// 分割+精修策略（MaskTemplate 模式）专属参数。
/// 模板图（转正后的目标 PNG，base64 内嵌配方文件）由配方页「示教模板」自动生成：
/// 取图 → 分割 → 最优目标转正裁剪。拷贝配方文件即携带模板，无路径依赖。
/// </summary>
public sealed class TemplateOptions
{
    /// <summary>精修方法（默认模板匹配；旧配方无此字段自动取默认，行为不变）。</summary>
    public SegmentRefineMethod RefineMethod { get; set; } = SegmentRefineMethod.Template;

    /// <summary>模板图 PNG 的 base64（转正目标裁剪）；空 = 未示教（LineFit 方法不使用）。</summary>
    public string TemplateImageBase64 { get; set; } = "";

    /// <summary>模板匹配置信阈值 [0,1]：低于该值放弃精修，回退粗角度（[0,180) 无方向）。</summary>
    public double MatchThreshold { get; set; } = 0.6;

    /// <summary>粗角度基础上的精修搜索范围（度，(0,45]）：minAreaRect 粗角误差通常 &lt;10°。</summary>
    public double RefineRangeDeg { get; set; } = 8;

    /// <summary>混合判决：边缘图定角度（更准，中纹理目标抖动 σ 1.6°→0.3°）+ 灰度图定头尾。
    /// 灰度直匹配在中等纹理上角度抖动较大；纯边缘匹配会丢头尾（Canny 抹掉不对称特征）。</summary>
    public bool UseEdgeMatch { get; set; }

    public TemplateOptions Clone() => new()
    {
        RefineMethod = RefineMethod,
        TemplateImageBase64 = TemplateImageBase64,
        MatchThreshold = MatchThreshold,
        RefineRangeDeg = RefineRangeDeg,
        UseEdgeMatch = UseEdgeMatch,
    };
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

    /// <summary>关键点连线策略专属参数（KeyPointLine 模式使用）。</summary>
    public KeypointOptions Keypoint { get; set; } = new();

    /// <summary>双模型连线策略专属参数（DualCenterLine 模式使用）。</summary>
    public DualModelOptions DualModel { get; set; } = new();

    /// <summary>分割策略专属参数（MaskMinAreaRect 模式使用）。</summary>
    public SegmentationOptions Segmentation { get; set; } = new();

    /// <summary>分割+模板匹配专属参数（MaskTemplate 模式使用）。</summary>
    public TemplateOptions Template { get; set; } = new();

    /// <summary>检测区域（相对比例 0~1，X/Y 左上角、W/H 宽高）；null = 全图推理。
    /// 大图上只检测局部区域可显著降低 CPU 推理耗时。</summary>
    public Roi? Roi { get; set; }

    /// <summary>旋转中心补偿（默认关闭）。开启前需 CalibTool rotation 完成轴心标定。</summary>
    public RotationCompensationMode RotationCompensation { get; set; } = RotationCompensationMode.None;

    /// <summary>光源控制器 Id（对应 appsettings 中注册的光源）；空 = 不亮灯。</summary>
    public string? LightControllerId { get; set; }

    /// <summary>照明配置（通道/亮度/稳定延时/取图后熄灯）；配了 LightControllerId 才生效。</summary>
    public LightingConfig? Lighting { get; set; }

    // ---- 旧版平铺字段兼容读取（setter-only：反序列化旧配方 JSON 时迁移到子对象，序列化自动忽略）----

    [Obsolete("旧格式字段，仅用于读取旧配方 JSON；新格式用 Keypoint 子对象")]
    public int KeypointIndexA { set => Keypoint.IndexA = value; }

    [Obsolete("旧格式字段，仅用于读取旧配方 JSON；新格式用 Keypoint 子对象")]
    public int KeypointIndexB { set => Keypoint.IndexB = value; }

    [Obsolete("旧格式字段，仅用于读取旧配方 JSON；新格式用 Keypoint 子对象")]
    public double KeypointMinConfidence { set => Keypoint.MinConfidence = value; }

    [Obsolete("旧格式字段，仅用于读取旧配方 JSON；新格式用 DualModel 子对象")]
    public double PairingMaxDistancePx { set => DualModel.PairingMaxDistancePx = value; }

    [Obsolete("旧格式字段，仅用于读取旧配方 JSON；新格式用 Segmentation 子对象")]
    public double PixelConfidence { set => Segmentation.PixelConfidence = value; }

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
        Keypoint = Keypoint.Clone(),
        DualModel = DualModel.Clone(),
        Segmentation = Segmentation.Clone(),
        Template = Template.Clone(),
        Roi = Roi,
        RotationCompensation = RotationCompensation,
        LightControllerId = LightControllerId,
        Lighting = Lighting?.Clone(),
    };
}
