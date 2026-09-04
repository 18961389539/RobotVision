using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Teach;

// ScenePlaybook 的词汇表（枚举 + 记录）。从原单一文件抽出，集中放此，
// 使 ScenePlaybook.*.cs 各分文件只承载"逻辑"，职责更清。命名空间级类型，引用方无需改动。

/// <summary>分割实例的可迁移场景类（推荐器用，不进 TRIGGER）。</summary>
public enum SceneKind
{
    Unknown = 0,
    HousingWithHole,
    HousingWithTab,
    Silhouette,
    PrintedTexture,
    WeakTextureBar,
    NearCircular,
}

/// <summary>粗分打光：目标相对背景更亮视为暗场剪影。</summary>
public enum LightingClass
{
    Unknown = 0,
    DarkField,
    BrightField,
}

/// <summary>任务约束：决定角度模式与精修资格，而不是只比 NCC/卡尺分。</summary>
public sealed record TaskConstraints(
    bool NeedDirectedAngle = true,
    bool TeachAllowed = true,
    bool AppearanceVaries = false,
    bool HasTwoLandmarks = false,
    bool UseBlobsWithoutModel = false,
    int ExpectedCount = 1);

/// <summary>从轮廓/掩码抽出的场景描述。</summary>
public sealed record SceneDescriptor(
    SceneKind Kind,
    LightingClass Lighting,
    double Aspect,
    double Circularity,
    double TextureEntropy,
    double Separability,
    bool HoleOk,
    double ProtrusionPx,
    double TeachAreaPx,
    string Why)
{
    /// <summary>软分类最高票相对次高票的把握；硬编码测试场景默认为 1。</summary>
    public double KindConfidence { get; init; } = 1;

    public SceneKind? RivalKind { get; init; }

    public IReadOnlyList<string> Conflicts { get; init; } = [];

    /// <summary>目标内熵 − 背景熵；正值表示件上比背景更有纹理。</summary>
    public double RelativeEntropy { get; init; }

    public double HoleQuality { get; init; }

    /// <summary>壳体短边长度（px）。供凸起判定按件尺寸缩放，避免用固定常量当短边。退化轮廓默认 0。</summary>
    public double ShortLenPx { get; init; }
}

/// <summary>一条可落地的配方建议（角度模式 + 可选精修）。</summary>
public sealed record PlaybookCandidate(
    AngleMode AngleMode,
    SegmentRefineMethod? Refine,
    bool EdgeMatch,
    string Title,
    string Why,
    bool IsPrimary);

/// <summary>结果库 / 配方政策对推荐的先验（只进向导与示教，不进 TRIGGER）。</summary>
public sealed record RecipePrior(
    IReadOnlyList<SegmentRefineMethod>? PolicyOrder = null,
    SegmentRefineMethod? Downrank = null,
    string Reason = "");

/// <summary>软分类一张票。</summary>
public readonly record struct SceneKindScore(SceneKind Kind, double Score);

/// <summary>场景 + 任务 + 可选赛马后的完整建议。</summary>
public sealed record PlaybookAdvice(
    PlaybookCandidate Primary,
    IReadOnlyList<PlaybookCandidate> Alternatives,
    SceneKind Scene,
    string SceneLabel,
    string Summary)
{
    public double Confidence { get; init; } = 1;

    public string ConfidenceNote { get; init; } = "";

    public bool IsUncertain => Confidence < 0.5;
}
