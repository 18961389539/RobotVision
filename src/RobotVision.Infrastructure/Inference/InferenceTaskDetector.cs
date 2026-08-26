namespace RobotVision.Infrastructure.Inference;

/// <summary>根据文件名启发式或加载 ONNX 元数据推断推理任务。</summary>
public static class InferenceTaskDetector
{
    /// <summary>从常见 YOLO 导出文件名推断任务（即时，不加载模型）。</summary>
    public static InferenceTask? GuessFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        if (name.Contains("-seg") || name.Contains("_seg") || name.EndsWith("seg"))
            return InferenceTask.Segmentation;
        if (name.Contains("-pose") || name.Contains("_pose") || name.Contains("kpt"))
            return InferenceTask.PoseEstimation;
        if (name.Contains("-cls") || name.Contains("_cls"))
            return null;
        if (name.Contains("-obb") || name.Contains("_obb"))
            return null;
        return null;
    }

    /// <summary>加载模型并读取 <see cref="IInferenceEngine.DetectedTask"/>；未识别时回退文件名启发式。</summary>
    public static InferenceTask? Detect(IInferenceEngineFactory factory, string modelPath, string? fileName = null)
    {
        using var engine = factory.Create(modelPath);
        return engine.DetectedTask ?? GuessFromFileName(fileName ?? Path.GetFileName(modelPath));
    }
}
