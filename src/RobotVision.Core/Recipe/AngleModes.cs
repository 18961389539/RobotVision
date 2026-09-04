namespace RobotVision.Core.Recipe;

/// <summary>角度模式分组：免模型连线、双区搜索、模型槽等，避免各层重复 switch。</summary>
public static class AngleModes
{
    /// <summary>不加载 ONNX：双 BLOB / 双模板。</summary>
    public static bool IsModelFree(AngleMode mode) =>
        mode is AngleMode.DualBlobCenterLine or AngleMode.DualTemplateCenterLine;

    /// <summary>TRIGGER / 引用校验 / 钉扎 / 示教分割需要 ONNX。</summary>
    public static bool RequiresOnnx(AngleMode mode) => !IsModelFree(mode);

    /// <summary>可用 ROI1+ROI2 互斥搜索（双 BLOB / 双模板）。</summary>
    public static bool UsesSecondarySearchRoi(AngleMode mode) => IsModelFree(mode);

    /// <summary>单模型角度模式只使用 <c>Models[0]</c>。</summary>
    public static bool UsesSingleModelSlot(AngleMode mode) =>
        mode is AngleMode.MaskMinAreaRect or AngleMode.KeyPointLine or AngleMode.MaskTemplate;

    /// <summary>双检测模型：恰好 2 个 ONNX。</summary>
    public static bool UsesDualModelSlots(AngleMode mode) =>
        mode is AngleMode.DualCenterLine;
}
