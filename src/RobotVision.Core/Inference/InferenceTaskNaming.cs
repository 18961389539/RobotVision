namespace RobotVision.Core.Inference;

/// <summary>从常见 YOLO 导出文件名推断推理任务（即时，不加载模型）。</summary>
public static class InferenceTaskNaming
{
    public static InferenceTask? GuessFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        if (name.Contains("-seg", StringComparison.Ordinal) || name.Contains("_seg", StringComparison.Ordinal) || name.EndsWith("seg", StringComparison.Ordinal))
            return InferenceTask.Segmentation;
        if (name.Contains("-pose", StringComparison.Ordinal) || name.Contains("_pose", StringComparison.Ordinal) || name.Contains("kpt", StringComparison.Ordinal))
            return InferenceTask.PoseEstimation;
        if (name.Contains("-cls", StringComparison.Ordinal) || name.Contains("_cls", StringComparison.Ordinal))
            return null;
        if (name.Contains("-obb", StringComparison.Ordinal) || name.Contains("_obb", StringComparison.Ordinal))
            return null;
        return null;
    }
}
