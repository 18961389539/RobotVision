using RobotVision.Infrastructure.Inference;

namespace RobotVision.Hosting;

/// <summary>模型推理运行时（WPF/宿主稳定入口）。</summary>
public interface IModelRuntime
{
    string ModelsFolder { get; }
    string[] ModelFileNames { get; }
    int LoadedCount { get; }
    IReadOnlyList<(string Path, InferenceTask Task)> LoadedKeys { get; }

    string ResolvePath(string modelFile);
    bool ModelFileExists(string modelFile);
    string ComputeSha256(string modelFile);
    ModelSession Open(string modelFile, InferenceTask task);
    void UnloadAll(string? modelFile = null);
}

internal sealed class ModelRuntime(ModelManager inner) : IModelRuntime
{
    public string ModelsFolder => inner.ModelsFolder;
    public string[] ModelFileNames => inner.ModelFileNames;
    public int LoadedCount => inner.LoadedCount;
    public IReadOnlyList<(string Path, InferenceTask Task)> LoadedKeys => inner.LoadedKeys;

    public string ResolvePath(string modelFile) => inner.ResolvePath(modelFile);
    public bool ModelFileExists(string modelFile) => inner.ModelFileExists(modelFile);
    public string ComputeSha256(string modelFile) => inner.ComputeSha256(modelFile);
    public ModelSession Open(string modelFile, InferenceTask task) => inner.Open(modelFile, task);
    public void UnloadAll(string? modelFile = null)
    {
        if (modelFile is null)
            inner.UnloadAll();
        else
            inner.UnloadAll(modelFile);
    }
}
