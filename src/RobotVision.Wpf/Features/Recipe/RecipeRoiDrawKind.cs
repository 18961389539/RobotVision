namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方页框选目标：检测区（双BLOB 时为 ROI1）/ 示教特征 / ROI2。</summary>
internal enum RecipeRoiDrawKind
{
    Detection,
    Template,
    SecondaryBlob,
}
