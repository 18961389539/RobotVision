namespace RobotVision.WpfHost.Features.Recipe;

public enum DualTemplateTeachSlot
{
    A,
    B,
}

/// <summary>配方页框选目标：检测区（双BLOB/双模板 时为 ROI1）/ 示教特征 / ROI2。</summary>
internal enum RecipeRoiDrawKind
{
    Detection,
    Template,
    SecondaryBlob,
}
