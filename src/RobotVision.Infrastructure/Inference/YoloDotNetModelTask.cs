using YoloDotNet.Enums;

namespace RobotVision.Infrastructure.Inference;

/// <summary>YoloDotNet <see cref="ModelType"/> → 本程序 <see cref="InferenceTask"/> 映射。</summary>
internal static class YoloDotNetModelTask
{
    public static InferenceTask? Map(ModelType modelType)
    {
        // 用名称匹配，兼容 YoloDotNet 枚举命名微调
        var name = modelType.ToString();
        if (name.Contains("Segment", StringComparison.OrdinalIgnoreCase))
            return InferenceTask.Segmentation;
        if (name.Contains("Pose", StringComparison.OrdinalIgnoreCase))
            return InferenceTask.PoseEstimation;
        if (name.Contains("Obb", StringComparison.OrdinalIgnoreCase)
            || name.Contains("OBB", StringComparison.Ordinal))
            return null;
        if (name.Contains("Classification", StringComparison.OrdinalIgnoreCase))
            return null;
        if (name.Contains("Detection", StringComparison.OrdinalIgnoreCase))
            return InferenceTask.ObjectDetection;
        return null;
    }
}
