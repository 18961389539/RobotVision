using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RobotVision.Core;
using RobotVision.Core.Models;
using YoloDotNet;
using YoloDotNet.ExecutionProvider.OpenVino;
using YoloDotNet.Models;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// YoloDotNet 引擎工厂。YoloDotNet 每个进程只能引用一种 Execution Provider 包，
/// 产线按工控机对比结果优先 OpenVINO 核显 GPU，不再混编 Microsoft CPU EP。
/// <paramref name="provider"/>（appsettings Inference:Provider）：
/// - OpenVinoGpu / Gpu / OpenVino：Intel 核显（FP16）；GPU 创建失败且 CPU 成功才记「GPU 不可用」；
/// - OpenVinoCpu / Cpu：直接 OpenVINO CPU（无核显或对照）。
/// 坏文件 / 两边都失败不置位，避免空 ONNX 把后续好模型永久打到 CPU。
/// 同一模型仍由 ModelManager 一把锁串行，不开 CPU+GPU 混池、不默认第二会话。
/// </summary>
public sealed class YoloDotNetEngineFactory(
    string provider = "OpenVinoGpu",
    ILogger<YoloDotNetEngineFactory>? log = null) : IInferenceEngineFactory
{
    private int _gpuFailed;
    private string _activeDevice = "";

    public string Provider { get; } = provider;

    public string ActiveDevice => Volatile.Read(ref _activeDevice) ?? "";

    public bool GpuUnavailable => Volatile.Read(ref _gpuFailed) != 0;

    public InferenceTask? DetectTask(string modelPath) =>
        InferenceTaskDetector.Detect(this, modelPath);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Yolo ownership transfers to YoloDotNetEngine.")]
    public IInferenceEngine Create(string modelPath)
    {
        Yolo? yolo = null;
        try
        {
            var skipGpu = GpuUnavailable;
            yolo = CreateYolo(modelPath, Provider, log, skipGpu, OnGpuFallback, OnDevice);
            var engine = new YoloDotNetEngine(yolo);
            yolo = null;
            return engine;
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
        finally
        {
            yolo?.Dispose();
        }
    }

    private void OnGpuFallback() => Interlocked.Exchange(ref _gpuFailed, 1);

    private void OnDevice(string device) => Volatile.Write(ref _activeDevice, device);

    /// <summary>供诊断工具与工厂共用同一套 OpenVINO 会话参数；GPU 失败且 CPU 成功时才回退并粘性。</summary>
    public static Yolo CreateYolo(
        string modelPath,
        string provider,
        ILogger? log = null,
        bool skipGpu = false,
        Action? onGpuFallback = null,
        Action<string>? onDevice = null)
    {
        var device = ResolveDevice(provider);
        if (device == "GPU" && skipGpu)
            device = "CPU";

        if (device != "GPU")
        {
            var cpu = CreateOnDevice(modelPath, device);
            onDevice?.Invoke(device);
            return cpu;
        }

        try
        {
            var gpu = CreateOnDevice(modelPath, "GPU");
            onDevice?.Invoke("GPU");
            return gpu;
        }
        catch (Exception ex) when (ex is not VisionException)
        {
            var file = Path.GetFileName(modelPath);
            try
            {
                var cpu = CreateOnDevice(modelPath, "CPU");
                onGpuFallback?.Invoke();
                onDevice?.Invoke("CPU");
                if (log is { } logger)
                    YoloDotNetEngineFactoryLog.GpuFallback(logger, ex, ex.Message, file);
                if (log is null)
                {
                    Console.Error.WriteLine(
                        $"警告: OpenVINO GPU 不可用（{ex.Message}），模型 {file} 已回退 OpenVINO CPU");
                }

                return cpu;
            }
            catch (Exception cpuEx)
            {
                throw new VisionException(VisionErrorCode.ModelNotAvailable,
                    $"模型加载失败: {file}: GPU 创建失败（{ex.Message}），CPU 回退也失败: {cpuEx.Message}",
                    cpuEx);
            }
        }
    }

    private static Yolo CreateOnDevice(string modelPath, string device)
    {
        var cacheDir = Path.Combine(AppContext.BaseDirectory, "ovcache");
        Directory.CreateDirectory(cacheDir);

        var ov = new OpenVino
        {
            DeviceType = device,
            Precision = device.StartsWith("GPU", StringComparison.OrdinalIgnoreCase)
                ? Precision.FP16
                : Precision.FP32,
            CachePath = cacheDir,
            ModelPriority = ModelPriority.HIGH,
        };

        return new Yolo(new YoloOptions
        {
            ExecutionProvider = new OpenVinoExecutionProvider(modelPath, ov),
        });
    }

    private static string ResolveDevice(string provider)
    {
        var key = provider.Trim().ToUpperInvariant()
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        return key switch
        {
            "OPENVINOGPU" or "GPU" or "OPENVINO" => "GPU",
            "OPENVINOCPU" or "CPU" => "CPU",
            _ => throw new VisionException(VisionErrorCode.ModelNotAvailable,
                $"不支持的推理 Provider: {provider}（当前支持 OpenVinoGpu / OpenVinoCpu）"),
        };
    }
}
