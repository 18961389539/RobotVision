namespace RobotVision.Core.Recipe;

/// <summary>形状匹配极性 Metric（对标 HALCON find_shape_model）。</summary>
public enum ShapeMatchMetric
{
    UsePolarity = 0,
    IgnoreLocalPolarity = 1,
    IgnoreGlobalPolarity = 2,
}
