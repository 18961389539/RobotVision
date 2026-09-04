using RobotVision.Core.Recipe;

namespace RobotVision.Vision;

/// <summary>亚像素取边模式（对标 HALCON measure_pos / fuzzy_measure_pos）。</summary>
public enum RectEdgeMeasureMode
{
    /// <summary>锐边：最强梯度峰 + 抛物线插值（默认）。</summary>
    Sharp = 0,

    /// <summary>模糊边：剖面平滑 + 梯度模糊隶属度加权重心（弱纹理/柔过渡更稳）。</summary>
    Fuzzy = 1,
}

/// <summary>
/// 旋转矩形拟合参数约束（对标 metrology 对象固定参数）。
/// 非 null 字段在拟合结束后强制写入结果。
/// </summary>
public sealed record RectFitConstraints(
    double? FixedAngleDeg = null,
    double? FixedLongLenPx = null,
    double? FixedShortLenPx = null)
{
    public static RectFitConstraints None { get; } = new();

    public RotatedRectFitResult Apply(RotatedRectFitResult fit) =>
        !fit.Ok ? fit : fit with
        {
            AngleDeg = FixedAngleDeg ?? fit.AngleDeg,
            LongLen = FixedLongLenPx ?? fit.LongLen,
            ShortLen = FixedShortLenPx ?? fit.ShortLen,
        };

    internal RotatedRectSubpixel.Result Apply(RotatedRectSubpixel.Result fit) =>
        fit with
        {
            AngleDeg = FixedAngleDeg ?? fit.AngleDeg,
            LongLen = FixedLongLenPx ?? fit.LongLen,
            ShortLen = FixedShortLenPx ?? fit.ShortLen,
        };
}

/// <summary>rectangle2 全链路选项：轮廓算法 + 亚像素极性/模糊 + 参数约束。</summary>
public sealed record RectFitOptions
{
    public RectFitAlgorithm ContourAlgorithm { get; init; } = RectFitAlgorithm.Tukey;
    public RectEdgePolarity EdgePolarity { get; init; } = RectEdgePolarity.Any;
    public RectEdgeMeasureMode EdgeMeasureMode { get; init; } = RectEdgeMeasureMode.Sharp;
    public RectFitConstraints Constraints { get; init; } = RectFitConstraints.None;
    public int ClipEndPoints { get; init; }

    /// <summary>拟合前是否剔凸起（<see cref="MaskHousing.CorePoints"/>）；合成矩形对标时关闭。</summary>
    public bool StripTabProtrusion { get; init; } = true;

    public static RectFitOptions Default { get; } = new();

    /// <summary>从 LineFit 配方项构建全链路选项（极性 + 模糊边 + 端点裁剪；约束由调用方按需追加）。</summary>
    public static RectFitOptions ForLineFit(TemplateOptions template)
    {
        var constraints = RectFitConstraints.None;
        if (template.LineFitConstrainTeachSize &&
            template.TeachAreaPx > 1 &&
            template.TeachAspect > 1e-3)
        {
            var (longLen, shortLen) = InstanceGeometry.DeriveRectangleSides(
                template.TeachAreaPx, template.TeachAspect);
            if (longLen > 8 && shortLen > 4)
            {
                constraints = constraints with
                {
                    FixedLongLenPx = longLen,
                    FixedShortLenPx = shortLen,
                };
            }
        }

        return new()
        {
            EdgePolarity = template.HousingEdgePolarity switch
            {
                HousingEdgePolarity.BrightToDark => RectEdgePolarity.BrightToDark,
                HousingEdgePolarity.DarkToBright => RectEdgePolarity.DarkToBright,
                _ => RectEdgePolarity.Any,
            },
            EdgeMeasureMode = template.LineFitFuzzyMeasure
                ? RectEdgeMeasureMode.Fuzzy
                : RectEdgeMeasureMode.Sharp,
            ClipEndPoints = 2,
            Constraints = constraints,
        };
    }
}
