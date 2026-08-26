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

    /// <summary>
    /// 双BLOB连线（无需模型）：阈值分割+连通域分析，主 BLOB 质心定位 XY，
    /// 主 BLOB 外扩窗口内最近次 BLOB 质心定向（主→次连线，主次有序天然消 180° 歧义）。
    /// </summary>
    DualBlobCenterLine,
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

    /// <summary>
    /// 窗口配对（主从级联）：A 全图检测 → 每个 A 包围盒外扩窗口内单独跑 B，
    /// 窗口内最近中心配对——多目标不会跨目标配错，且 B 只推理小图。
    /// false = 旧行为（A/B 各跑整图，全局贪心最近邻配对）。
    /// </summary>
    public bool CropWindowPairing { get; set; }

    /// <summary>窗口外扩系数：A 包围盒四边各外扩（边长×该值），仅 CropWindowPairing 生效，(0,5]。</summary>
    public double CropExpandRatio { get; set; } = 1.0;

    public DualModelOptions Clone() => new()
    {
        PairingMaxDistancePx = PairingMaxDistancePx,
        CropWindowPairing = CropWindowPairing,
        CropExpandRatio = CropExpandRatio,
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

/// <summary>
/// 双BLOB连线策略（DualBlobCenterLine）专属参数：阈值分割 + 连通域分析。
/// 主 BLOB（面积在 [MinArea,MaxArea] 内）质心定位 XY；主包围盒按 CropExpandRatio 外扩
/// 圈定次 BLOB 搜索窗口，窗口内面积合格且距主质心 [Min,Max]PairDistancePx 的最近
/// 连通域为次 BLOB；角度 = 主质心→次质心连线。
/// </summary>
public sealed class BlobOptions
{
    /// <summary>true = 检测暗 BLOB（阈值取反）；false = 亮 BLOB。</summary>
    public bool DetectDark { get; set; }

    /// <summary>true = Otsu 自动阈值（忽略 Threshold）；false = 固定阈值。</summary>
    public bool UseOtsu { get; set; } = true;

    /// <summary>固定二值化阈值 [0,255]（UseOtsu=false 时生效）。</summary>
    public int Threshold { get; set; } = 128;

    /// <summary>主 BLOB 面积下限（px²）。</summary>
    public int MinArea { get; set; } = 200;

    /// <summary>主 BLOB 面积上限（px²）。</summary>
    public int MaxArea { get; set; } = 200000;

    /// <summary>次 BLOB 面积下限（px²）。</summary>
    public int SecondaryMinArea { get; set; } = 10;

    /// <summary>次 BLOB 面积上限（px²）。</summary>
    public int SecondaryMaxArea { get; set; } = 50000;

    /// <summary>次 BLOB 搜索窗口外扩系数：主包围盒四边各外扩（边长×该值），(0,5]。</summary>
    public double CropExpandRatio { get; set; } = 1.0;

    /// <summary>主次质心最小间距（px）：防止把主 BLOB 边缘碎块配成次 BLOB。</summary>
    public double MinPairDistancePx { get; set; } = 5;

    /// <summary>主次质心最大间距（px）：超过不配对。</summary>
    public double MaxPairDistancePx { get; set; } = 800;

    /// <summary>开运算核边长（px，≤1 = 关闭）：主次 BLOB 轻微粘连时先开运算分离再分析。</summary>
    public int OpenKernelSize { get; set; }

    public BlobOptions Clone() => new()
    {
        DetectDark = DetectDark,
        UseOtsu = UseOtsu,
        Threshold = Threshold,
        MinArea = MinArea,
        MaxArea = MaxArea,
        SecondaryMinArea = SecondaryMinArea,
        SecondaryMaxArea = SecondaryMaxArea,
        CropExpandRatio = CropExpandRatio,
        MinPairDistancePx = MinPairDistancePx,
        MaxPairDistancePx = MaxPairDistancePx,
        OpenKernelSize = OpenKernelSize,
    };
}

/// <summary>配方：一次 TRIGGER 触发所需的全部参数。</summary>
public sealed class RecipeConfig
{
    /// <summary>配置结构版本（未来字段演进时用于迁移判断）。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>配方用途备注（管理台展示，不参与推理）。</summary>
    public string Description { get; set; } = "";

    /// <summary>启用开关：false 时配方不可触发（返回 1015 RecipeDisabled），文件保留不删。</summary>
    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = "";

    /// <summary>
    /// PLC 触发的配方序列号（&gt;0 唯一；0 表示未分配，仅能用名称触发）。
    /// TCP 请求可发纯数字或 #数字，如 <c>3</c> / <c>#3</c> / <c>3,X,Y,RZ</c>。
    /// </summary>
    public int SerialNumber { get; set; }

    /// <summary>相机 Id（对应 appsettings 中注册的相机）。</summary>
    public string CameraId { get; set; } = "";

    /// <summary>工位 Id（外参/多项式/比例标定档案）。TRIGGER 须映射到机器人或图像平面毫米坐标。</summary>
    public string? StationId { get; set; }

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

    /// <summary>双BLOB连线专属参数（DualBlobCenterLine 模式使用；该模式不需要模型）。</summary>
    public BlobOptions Blob { get; set; } = new();

    /// <summary>检测区域（相对比例 0~1，X/Y 左上角、W/H 宽高）；null = 全图推理。
    /// 大图上只检测局部区域可显著降低 CPU 推理耗时。</summary>
    public Roi? Roi { get; set; }

    /// <summary>旋转中心补偿（默认关闭）。开启前需 CalibTool rotation 完成轴心标定。</summary>
    public RotationCompensationMode RotationCompensation { get; set; } = RotationCompensationMode.None;

    /// <summary>输出补偿（首件微调）：变换与偏心补偿之后叠加到每个机器人位姿。缺省全 0。</summary>
    public OutputOffsetOptions OutputOffset { get; set; } = new();

    /// <summary>
    /// 与 <see cref="Models"/> 按下标对应的 SHA-256（小写 hex）。空串或不填 = 该槽不校验。
    /// 钉死后替换同名 ONNX 会在 TRIGGER 时返回 1017，避免错模型静默出坐标。
    /// </summary>
    public List<string> ModelSha256 { get; set; } = [];

    /// <summary>
    /// 工位映射档案（多项式/外参/比例，以及开启偏心补偿时的旋转中心）内容指纹。
    /// 空 = 不校验。重标定后须在配方页重新钉扎。
    /// </summary>
    public string? StationSha256 { get; set; }

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
        SerialNumber = SerialNumber,
        CameraId = CameraId,
        StationId = StationId,
        AngleMode = AngleMode,
        Models = [.. Models],
        Confidence = Confidence,
        Iou = Iou,
        Keypoint = Keypoint.Clone(),
        DualModel = DualModel.Clone(),
        Segmentation = Segmentation.Clone(),
        Template = Template.Clone(),
        Blob = Blob.Clone(),
        Roi = Roi,
        RotationCompensation = RotationCompensation,
        OutputOffset = OutputOffset.Clone(),
        ModelSha256 = [.. ModelSha256],
        StationSha256 = StationSha256,
        LightControllerId = LightControllerId,
        Lighting = Lighting?.Clone(),
    };
}
