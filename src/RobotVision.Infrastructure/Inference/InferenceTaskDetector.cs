namespace RobotVision.Infrastructure.Inference;

/// <summary>根据文件名启发式或加载 ONNX 元数据推断推理任务。</summary>
public static class InferenceTaskDetector
{
    /// <summary>加载模型并读取 <see cref="IInferenceEngine.DetectedTask"/>；未识别时回退文件名启发式。</summary>
    public static InferenceTask? Detect(IInferenceEngineFactory factory, string modelPath, string? fileName = null)
    {
        using var engine = factory.Create(modelPath);
        return engine.DetectedTask ?? InferenceTaskNaming.GuessFromFileName(fileName ?? Path.GetFileName(modelPath));
    }
}
