using RobotVision.Core.Inference;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Hosting;

/// <summary>推理引擎探测与设备状态（WPF 不直接引用 Infrastructure 工厂）。</summary>
public interface IInferenceRuntime
{
    string ActiveDevice { get; }
    bool GpuUnavailable { get; }
    InferenceTask? DetectTask(string modelPath);
}

internal sealed class InferenceRuntime(IInferenceEngineFactory inner) : IInferenceRuntime
{
    public string ActiveDevice => inner.ActiveDevice;
    public bool GpuUnavailable => inner.GpuUnavailable;
    public InferenceTask? DetectTask(string modelPath) => inner.DetectTask(modelPath);
}
