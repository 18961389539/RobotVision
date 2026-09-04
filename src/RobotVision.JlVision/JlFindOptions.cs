using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>形状/NCC 局部搜索选项（180° 支与找图金字塔）。</summary>
public readonly record struct JlFindOptions
{
    /// <summary>只搜粗角支，跳过 +180°。</summary>
    public bool NoFlip { get; init; }

    /// <summary>
    /// 两支都命中时，取有向角更接近 0° 的一支。
    /// 用于粗角被 MinAreaRect 打到 ±180°、但工位上零件朝向接近示教（~0°）的产线。
    /// </summary>
    public bool PreferUpright { get; init; }

    /// <summary>+180° 支必须比 0° 支高出的分数；PreferUpright 时忽略。</summary>
    public double FlipScoreMargin { get; init; }

    /// <summary>find_shape_model NumLevels；0 = auto。</summary>
    public int NumLevels { get; init; }

    public double Greediness { get; init; }

    public static JlFindOptions ProductDefault { get; } = new()
    {
        PreferUpright = true,
        FlipScoreMargin = 0.08,
        NumLevels = 0,
        Greediness = 0.9,
    };

    public static JlFindOptions ForRecipe(TemplateOptions template) =>
        ProductDefault with
        {
            NoFlip = template.NoFlipConstraint,
            NumLevels = Math.Clamp(template.ShapeMatchNumLevels, 0, 5),
        };
}
