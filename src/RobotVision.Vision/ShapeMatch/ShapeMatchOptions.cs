using RobotVision.Core.Recipe;

namespace RobotVision.Vision;

/// <summary>
/// 形状匹配可调参数（对标 HALCON create_shape_model / find_shape_model）。
/// 当前实现为分割转正窗内 Chamfer 精修；<see cref="NumLevels"/> 控制搜索金字塔层数。
/// </summary>
public sealed record ShapeMatchOptions
{
    /// <summary>搜索金字塔层数 [1,3]：1=全分辨率；2=½+全（默认）；3=¼+½+全。</summary>
    public int NumLevels { get; init; } = 2;

    /// <summary>精修角搜索起点（°，相对转正窗残差角）。</summary>
    public double AngleStartDeg { get; init; }

    /// <summary>精修角搜索范围（°），实际搜索 [<see cref="AngleStartDeg"/>, AngleStart+Extent]。</summary>
    public double AngleExtentDeg { get; init; } = 16;

    /// <summary>粗搜角步长（°）。</summary>
    public double AngleStepDeg { get; init; } = 1.0;

    /// <summary>极性 / 方向 Metric（见 <see cref="ShapeMatchMetric"/>）。</summary>
    public ShapeMatchMetric Metric { get; init; } = ShapeMatchMetric.UsePolarity;

    /// <summary>最小对比度 [0,255]：0=自适应 Canny；&gt;0 提高边缘阈值，抑制低对比噪声。</summary>
    public double MinContrast { get; init; }

    /// <summary>命中门：示教边点落在距离场内的最低比例。</summary>
    public double MinHitRate { get; init; } = 0.18;

    /// <summary>均距门（px）：Chamfer 平均距离上限。</summary>
    public double MaxMeanDistPx { get; init; } = 10.0;

    /// <summary>是否输出中间可视化（内点/拒点、距离直方图、金字塔层数）。</summary>
    public bool EnableVisualization { get; init; } = true;

    /// <summary>是否输出搜索网格诊断（粗/细评估次数、最优代价）。</summary>
    public bool EmitSearchDebug { get; init; }

    public static ShapeMatchOptions Default { get; } = new();

    /// <summary>从配方模板项映射（未配置字段用默认）。</summary>
    public static ShapeMatchOptions From(TemplateOptions? template)
    {
        if (template is null)
            return Default;
        var win = template.GetRefineAngleWindow();
        return new ShapeMatchOptions
        {
            AngleStartDeg = win.LoDeg,
            AngleExtentDeg = Math.Clamp(win.SpanDeg, 1, 90),
            MinContrast = Math.Max(0, template.ShapeMatchMinContrast),
            Metric = template.ShapeMatchMetric,
            NumLevels = Math.Clamp(template.ShapeMatchNumLevels, 1, 3),
        };
    }

    internal int ClampedNumLevels => Math.Clamp(NumLevels, 1, 3);

    internal double CoarseRotStep => Math.Clamp(AngleStepDeg, 0.25, 3.0);

    internal bool UseDirectionCheck => Metric != ShapeMatchMetric.IgnoreGlobalPolarity;

    internal double DirMismatchPx => Metric switch
    {
        ShapeMatchMetric.IgnoreLocalPolarity => 1.5,
        ShapeMatchMetric.IgnoreGlobalPolarity => 0,
        _ => 4.5,
    };
}
