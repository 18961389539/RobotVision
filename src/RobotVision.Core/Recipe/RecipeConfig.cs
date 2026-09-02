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

/// <summary>壳体边缘在剖面里的极性（卡尺从外侧向中心抓第一条边）。</summary>
public enum HousingEdgePolarity
{
    /// <summary>未锁定：先按亮场亮→暗试，失败再试暗场。</summary>
    Auto = 0,

    /// <summary>亮场：背景亮、壳体暗，从外侧走进壳体为亮→暗（旧配方默认行为）。</summary>
    BrightToDark = 1,

    /// <summary>暗场：背景暗、壳体亮，从外侧走进壳体为暗→亮。</summary>
    DarkToBright = 2,
}

/// <summary>
/// 凸起相对壳体短轴的侧别（示教记录）。运行时每帧实测，不按此字段拒识或回退：
/// 无向长边在 [0,180) 换手时 ±短轴会对调，同号门会把转过后的正确结果判失败。
/// </summary>
public enum TabPolarityLock
{
    /// <summary>每帧测量；抓不到凸起则精修失败。</summary>
    Auto = 0,

    /// <summary>示教时凸起在 +短轴（θ≈0° 时朝下）。运行时仍每帧实测。</summary>
    PlusShortAxis = 1,

    /// <summary>示教时凸起在 −短轴（θ≈0° 时朝上）。运行时仍每帧实测。</summary>
    MinusShortAxis = 2,
}

/// <summary>分割精修（MaskTemplate 模式）的精修方法。
/// Template = 原图模板匹配（吃纹理，可判头尾）；LineFit = 掩码长边鲁棒直线拟合
/// （吃轮廓几何，弱纹理矩形适用，无 180° 方向语义）；CaliperTab = 原图卡尺长边 + 凸起极性；
/// Sift = 示教图 SIFT 特征匹配（相似变换，可判头尾）；
/// ShapeMatch = 示教 Canny Chamfer（分割窗内几何匹配，可判头尾）。</summary>
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

    /// <summary>卡尺长边 + 凸起极性：原图 1D 剖面抓两条长边（亚像素），短轴中心取两线中线；
    /// 头尾看壳体边缘外侧哪一侧更暗。免示教模板；分割只需粗框套住壳体。</summary>
    CaliperTab = 3,

    /// <summary>SIFT 特征匹配：示教模板与当前分割框内原图做特征点匹配，RANSAC 相似变换出 XY/有向角。
    /// 需示教模板；壳体弱纹理时可能匹配不够，失败默认 1019。</summary>
    Sift = 4,

    /// <summary>形状匹配：示教图 Canny 轮廓与分割框内转正图做 Chamfer（距离场）匹配，几何定位 XY/有向角。
    /// 需示教模板；只在分割目标窗内搜索，不整图搜。</summary>
    ShapeMatch = 5,
}

/// <summary>
/// LineFit 示教基准线：有向线段（起点 P1 = 尾、终点 P2 = 头），比例坐标 ∈ [0,1]（推理图像帧）。
/// <see cref="HeadMinusTailGray"/> 是示教时从两端邻域采到的「头 − 尾」平均灰度（0~255），作为运行时
/// 消 180° 的头尾签名：运行时在长轴两端各放一个探针窗，谁的明暗次序与该符号一致，谁就是头。
/// 端点仅用于画面回显/复编辑，不参与运行时几何映射（探针由当前实例壳体几何摆放）。
/// </summary>
public sealed record RefineLine(
    double X1,
    double Y1,
    double X2,
    double Y2,
    double HeadMinusTailGray)
{
    /// <summary>头尾签名/实测的最低明暗差（灰度）：任一侧小于该值判为无稳定不对称，180° 不可定，保持无向。</summary>
    public const double MinFlipContrastGray = 4.0;

    /// <summary>示教端邻域探针窗边长（px，用于示教时采 HeadMinusTailGray）。</summary>
    public const int TeachProbePx = 12;

    /// <summary>头尾符号（供运行时比较）：&gt;0 头端更亮，&lt;0 头端更暗，0 视为不可用。</summary>
    public double HeadSign => Math.Sign(HeadMinusTailGray);

    /// <summary>签名是否足够可靠（明暗差过门）。</summary>
    public bool HasReliableSignature => Math.Abs(HeadMinusTailGray) >= MinFlipContrastGray;
}

/// <summary>
/// 分割+精修策略（MaskTemplate 模式）专属参数。
/// 模板图（转正后的目标 PNG，base64 内嵌配方文件）由配方页「示教模板」自动生成：
/// 取图 → 分割 → 转正裁剪（可选特征 ROI 只裁局部纹理）。拷贝配方文件即携带模板，无路径依赖。
/// </summary>
public sealed class TemplateOptions
{
    /// <summary>精修方法（默认模板匹配；旧配方无此字段自动取默认，行为不变）。</summary>
    public SegmentRefineMethod RefineMethod { get; set; } = SegmentRefineMethod.Template;

    /// <summary>模板图 PNG 的 base64（转正目标裁剪）；空 = 未示教（LineFit / CaliperTab / CentroidHoleLine 不使用）。</summary>
    public string TemplateImageBase64 { get; set; } = "";

    /// <summary>模板匹配、SIFT 与形状匹配都要示教图；直线/卡尺/孔槽不用。</summary>
    public static bool NeedsTaughtImage(SegmentRefineMethod method) =>
        method is SegmentRefineMethod.Template or SegmentRefineMethod.Sift or SegmentRefineMethod.ShapeMatch;

    /// <summary>示教时可框选局部训练区域：灰度 NCC 与形状匹配。SIFT 必须整颗，不要裁特征框。</summary>
    public static bool UsesFeatureTeachRoi(SegmentRefineMethod method) =>
        method is SegmentRefineMethod.Template or SegmentRefineMethod.ShapeMatch;

    /// <summary>可选示教基准线（消 180°）：仅直线拟合使用。画了才输出有向角，没画保持无向旧行为。</summary>
    public static bool UsesTaughtRefineLine(SegmentRefineMethod method) =>
        method is SegmentRefineMethod.LineFit;

    /// <summary>宽高比达到该值（或倒数）视为过扁：模板/形状匹配十字会落在特征中心，齿列件可能跳齿。</summary>
    public const double FlatFeatureRoiAspect = 3;

    /// <summary>特征框过扁（宽÷高 ≥ <see cref="FlatFeatureRoiAspect"/> 或 ≤ 其倒数）。</summary>
    public static bool IsFlatFeatureRoi(Roi? roi)
    {
        if (roi is null || roi.Width < 1e-9 || roi.Height < 1e-9)
            return false;
        var aspect = roi.Width / roi.Height;
        return aspect >= FlatFeatureRoiAspect || aspect <= 1.0 / FlatFeatureRoiAspect;
    }

    /// <summary>
    /// 示教特征框（相对全图 0~1，与检测 ROI 同口径）；null = 示教时裁整个分割目标。
    /// 仅「示教模板」使用：TRIGGER 仍用配方检测 ROI 找目标，匹配在目标转正窗口内滑窗。
    /// 用于检测窗口很大、但头尾/齿脚等只在局部纹理上可判的场景。
    /// </summary>
    public Roi? Roi { get; set; }

    /// <summary>
    /// LineFit 可选「示教基准线」：用户在 ImageViewer 上画的有向线段（起点 P1 = 尾，终点 P2 = 头），
    /// 比例坐标 ∈ [0,1]（推理图像帧）。仅 <see cref="SegmentRefineMethod.LineFit"/> 使用：
    /// 给出后 LineFit 在原本无向精修角基础上，用「长轴两端明暗探针 vs 示教头尾签名」消 180° 歧义，
    /// 输出有向角 [0,360)。<b>null = 与旧版完全一致（无向 [0,180)）</b>，存量配方行为不变。
    /// </summary>
    public RefineLine? RefineLine { get; set; }

    /// <summary>模板匹配置信阈值 [0,1]：低于该值放弃精修，回退粗角度（[0,180) 无方向）。</summary>
    public double MatchThreshold { get; set; } = 0.6;

    /// <summary>粗角度基础上的精修搜索范围（度，(0,45]）：默认 ±5°。</summary>
    public double RefineRangeDeg { get; set; } = 5;

    /// <summary>
    /// true（默认）= 分割件先转正再在外扩窗内匹配；false = 不转正，在轴对齐包围盒内旋转模板搜索。
    /// 模板匹配的输出 XY 始终是匹配峰（映回原图），不是壳体中心。
    /// </summary>
    public bool UseUprightCrop { get; set; } = true;

    /// <summary>混合判决：边缘图定角度（更准，中纹理目标抖动 σ 1.6°→0.3°）+ 灰度图定头尾。
    /// 灰度直匹配在中等纹理上角度抖动较大；纯边缘匹配会丢头尾（Canny 抹掉不对称特征）。</summary>
    public bool UseEdgeMatch { get; set; }

    /// <summary>
    /// true = 目标永不翻转 180°（角度仅落在 -90°~+90° 区间，如分向限定工位）。
    /// 跳过 180° 分支搜索与翻转重试：省一半匹配计算量，并杜绝"180 支误选导致角度差 180°"。
    /// false（默认）= 允许任意姿态，保留 180° 头尾消歧。
    /// </summary>
    public bool NoFlipConstraint { get; set; }

    /// <summary>
    /// 运行时第二峰歧义门 (0,1]：最佳匹配峰同支次峰比值 ≤ 该值才接受。
    /// 目标纹理周期性/对称导致两个角度几乎同分（如齿距角、180° 近对称）时，
    /// 单一 NCC 峰不可靠——次峰/主峰 > 该值判歧义拒绝。默认 1 = 关闭。
    /// 建议 0.9~0.98；误拒时调大。
    /// </summary>
    public double MaxSecondPeakRatio { get; set; } = 1.0;

    /// <summary>
    /// true = 精修失败时仍输出无向粗角 [0,180)（旧行为，偏心工具可能差 180°）。
    /// false（默认）= 分割到了但精修不过门时该目标不可用，全部不可用则 TRIGGER 返回 1019。
    /// </summary>
    public bool AllowCoarseFallback { get; set; }

    /// <summary>示教时记下的模板峰值 NCC；0 = 未示教峰。采用推荐/示教会把 MatchThreshold 写成约 0.85×该值。</summary>
    public double TeachPeakScore { get; set; }

    /// <summary>壳体边缘极性；旧配方缺省 Auto（先亮场）。</summary>
    public HousingEdgePolarity HousingEdgePolarity { get; set; }

    /// <summary>凸起侧锁定；旧配方缺省 Auto（每帧估）。</summary>
    public TabPolarityLock TabPolarity { get; set; }

    /// <summary>期望件数；0 = 不检查（旧配方）。过门件数不符则全部不可用 → 1019。</summary>
    public int ExpectedCount { get; set; }

    /// <summary>
    /// 赛马政策序（空 = 默认孔槽&gt;卡尺&gt;模板&gt;形状&gt;SIFT&gt;直线）。只影响向导/示教推荐，不进 TRIGGER。
    /// </summary>
    public List<SegmentRefineMethod>? RefinePolicyOrder { get; set; }

    /// <summary>示教轮廓面积（px²）；0 = 不查面积门。</summary>
    public double TeachAreaPx { get; set; }

    /// <summary>示教壳体长/短轴比；0 = 不查轴比门。</summary>
    public double TeachAspect { get; set; }

    /// <summary>面积相对示教下限比。</summary>
    public double AreaRatioLo { get; set; } = InstanceGeometry.DefaultAreaRatioLo;

    /// <summary>面积相对示教上限比。</summary>
    public double AreaRatioHi { get; set; } = InstanceGeometry.DefaultAreaRatioHi;

    /// <summary>轴比相对示教下限比。</summary>
    public double AspectRatioLo { get; set; } = InstanceGeometry.DefaultAspectRatioLo;

    /// <summary>轴比相对示教上限比。</summary>
    public double AspectRatioHi { get; set; } = InstanceGeometry.DefaultAspectRatioHi;

    /// <summary>由示教峰得到建议匹配阈值（写入 MatchThreshold，运行时仍只读 MatchThreshold）。
    /// peakSharpness：同头尾支上次峰相对主峰的缺口 (best−second)/best；钝峰则把门抬高，避免次峰过门。</summary>
    public static double MatchThresholdFromTeachPeak(double teachPeak, double peakSharpness = 1)
    {
        var sharp = double.IsFinite(peakSharpness) ? Math.Clamp(peakSharpness, 0, 1) : 1;
        var factor = sharp < 0.04 ? 0.93 : sharp < 0.08 ? 0.89 : 0.85;
        return Math.Clamp(teachPeak * factor, 0.40, 0.92);
    }

    /// <summary>精修范围语义提示：范围即实际角度窗 ±range°。NoFlip 仅 0 支（不搜 180°±range）。</summary>
    public static string RefineRangeHintText(TemplateOptions t)
    {
        var range = t.RefineRangeDeg;
        if (t.NoFlipConstraint)
            return $"角度窗 = ±{range:0}°（仅 0 支，无 180° 翻转）。目标实际姿态差有多大，范围就设多大（如 ±40° 输入 40）。";
        return $"角度窗 = ±{range:0}°（另搜 180°±{range:0} 翻转支）。仅当产物可反放（需判头尾）才保留两支；" +
               "分向限定可勾选「目标永不翻转 180°」省一半计算并杜绝误判。";
    }

    public TemplateOptions Clone(bool includeTemplateImage = true)
    {
        var copy = new TemplateOptions();
        CopyTo(copy, includeTemplateImage);
        return copy;
    }

    /// <summary>把当前字段写入已有实例（取消弹窗恢复快照等场景用）。</summary>
    public void CopyTo(TemplateOptions target, bool includeTemplateImage = true)
    {
        target.RefineMethod = RefineMethod;
        target.TemplateImageBase64 = includeTemplateImage ? TemplateImageBase64 : "";
        target.Roi = Roi;
        target.RefineLine = RefineLine;
        target.MatchThreshold = MatchThreshold;
        target.RefineRangeDeg = RefineRangeDeg;
        target.UseUprightCrop = UseUprightCrop;
        target.UseEdgeMatch = UseEdgeMatch;
        target.NoFlipConstraint = NoFlipConstraint;
        target.MaxSecondPeakRatio = MaxSecondPeakRatio;
        target.AllowCoarseFallback = AllowCoarseFallback;
        target.TeachPeakScore = TeachPeakScore;
        target.HousingEdgePolarity = HousingEdgePolarity;
        target.TabPolarity = TabPolarity;
        target.ExpectedCount = ExpectedCount;
        target.RefinePolicyOrder = RefinePolicyOrder is { Count: > 0 } ? [..RefinePolicyOrder] : null;
        target.TeachAreaPx = TeachAreaPx;
        target.TeachAspect = TeachAspect;
        target.AreaRatioLo = AreaRatioLo;
        target.AreaRatioHi = AreaRatioHi;
        target.AspectRatioLo = AspectRatioLo;
        target.AspectRatioHi = AspectRatioHi;
    }
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
    public RecipeConfig Clone(bool includeTemplateImage = true) => new()
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
        Template = Template.Clone(includeTemplateImage),
        Blob = Blob.Clone(),
        Roi = Roi,
        RotationCompensation = RotationCompensation,
        OutputOffset = (OutputOffset ?? new()).Clone(),
        ModelSha256 = [.. ModelSha256 ?? []],
        StationSha256 = StationSha256,
        LightControllerId = LightControllerId,
        Lighting = Lighting?.Clone(),
    };
}
