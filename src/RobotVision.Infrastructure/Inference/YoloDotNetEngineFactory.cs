using RobotVision.Core;
using RobotVision.Core.Models;
using YoloDotNet;
using YoloDotNet.ExecutionProvider.Cpu;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// YoloDotNet（ONNX Runtime）引擎工厂：按模型路径创建引擎实例。
/// ExecutionProvider 由 <paramref name="provider"/> 决定（默认 CPU）：
/// - "Cpu"：CPU 推理（当前引用 YoloDotNet.ExecutionProvider.Cpu 包）；
/// - 换 GPU/加速器：NuGet 引用对应 ExecutionProvider 包后，在本工厂按
///   provider 名补充分支（Cuda/OpenVino/DirectML），或提供独立的
///   IInferenceEngineFactory 实现替换 DI 注册——ModelManager 与策略层零改动。
/// </summary>
public sealed class YoloDotNetEngineFactory(string provider = "Cpu") : IInferenceEngineFactory
{
    public string Provider { get; } = provider;

    public IInferenceEngine Create(string modelPath)
    {
        try
        {
            var yolo = Provider.ToUpperInvariant() switch
            {
                "CPU" => new Yolo(new YoloOptions
                {
                    ExecutionProvider = new CpuExecutionProvider(modelPath),
                }),
                // 更换 GPU：引用对应 NuGet 包（如 YoloDotNet.ExecutionProvider.Cuda）
                // 后在此补充分支，如 "CUDA" => new Yolo(new YoloOptions { ExecutionProvider = new CudaExecutionProvider(modelPath) })
                _ => throw new VisionException(VisionErrorCode.ModelNotAvailable,
                    $"不支持的推理 Provider: {Provider}（当前支持 Cpu；换 GPU 请引用对应 ExecutionProvider 包并在 YoloDotNetEngineFactory 补充）"),
            };
            return new YoloDotNetEngine(yolo);
        }
        catch (VisionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VisionException(VisionErrorCode.ModelNotAvailable,
                $"模型加载失败: {Path.GetFileName(modelPath)}: {ex.Message}", ex);
        }
    }
}
