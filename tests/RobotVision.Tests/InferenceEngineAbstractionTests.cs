using Microsoft.Extensions.Logging;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 推理引擎抽象测试：
/// - YoloDotNetEngineFactory provider 校验（Cpu 支持、未知 provider 拒绝）；
/// - ModelManager 通过注入的 IInferenceEngineFactory 创建引擎——换后端 =
///   换工厂，ModelManager/策略层不感知具体框架；
/// - ModelSession.Run 把调用委托给抽象引擎（返回类型兼容）。
/// </summary>
public class InferenceEngineAbstractionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rv_ie_" + Guid.NewGuid().ToString("N"));

    public InferenceEngineAbstractionTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void YoloDotNetEngineFactory_DefaultProvider_IsCpu()
    {
        var factory = new YoloDotNetEngineFactory();
        Assert.Equal("Cpu", factory.Provider);
    }

    [Fact]
    public void YoloDotNetEngineFactory_UnsupportedProvider_ThrowsModelNotAvailable()
    {
        var factory = new YoloDotNetEngineFactory("Cuda");
        var ex = Assert.Throws<VisionException>(() => factory.Create(Path.Combine(_folder, "x.onnx")));
        Assert.Equal(VisionErrorCode.ModelNotAvailable, ex.ErrorCode);
        Assert.Contains("Cuda", ex.Message);
    }

    /// <summary>记录型假引擎：验证 ModelManager 经抽象调用引擎方法。</summary>
    private sealed class FakeEngine : IInferenceEngine
    {
        public InferenceTask? DetectedTask => null;

        public int DetectionCalls;
        public int SegmentationCalls;
        public int PoseCalls;
        public int DisposedCount;

        public IReadOnlyList<ObjectDetectionResult> RunObjectDetection(VisionImage image, double confidence = 0.25, double iou = 0.45)
        {
            DetectionCalls++;
            return [];
        }

        public IReadOnlyList<InstanceSegmentation> RunSegmentation(VisionImage image, double confidence = 0.25, double pixelConfidence = 0.5, double iou = 0.45)
        {
            SegmentationCalls++;
            return [];
        }

        public IReadOnlyList<PoseDetectionResult> RunPoseEstimation(VisionImage image, double confidence = 0.25, double iou = 0.45)
        {
            PoseCalls++;
            return [];
        }

        public void Dispose() => DisposedCount++;
    }

    private sealed class FakeEngineFactory : IInferenceEngineFactory
    {
        public FakeEngine Engine { get; } = new();

        public IInferenceEngine Create(string modelPath) => Engine;
    }

    [Fact]
    public void ModelManager_Open_CreatesEngineViaFactory_AndSessionDelegatesToIt()
    {
        var factory = new FakeEngineFactory();
        var modelPath = Path.Combine(_folder, "fake.onnx");
        File.WriteAllText(modelPath, "fake"); // 文件存在校验通过

        using var manager = new ModelManager(_folder, factory);
        var session = manager.Open("fake.onnx", InferenceTask.PoseEstimation);

        using var dummy = VisionImage.AllocateZero(64, 64, 3);
        var results = session.Run(e => e.RunPoseEstimation(dummy));
        Assert.Empty(results);
        Assert.Equal(2, factory.Engine.PoseCalls); // 预热(1) + 本次推理(1)
        Assert.Equal(0, factory.Engine.DetectionCalls);
    }

    [Fact]
    public void ModelManager_WarmUp_RunsOneFrame_OnEngineCreation()
    {
        var factory = new FakeEngineFactory();
        var modelPath = Path.Combine(_folder, "fake.onnx");
        File.WriteAllText(modelPath, "fake");

        using var manager = new ModelManager(_folder, factory);
        manager.Open("fake.onnx", InferenceTask.Segmentation);

        // 预热空跑一帧 → SegmentationCalls == 1（640×640 dummy）
        Assert.Equal(1, factory.Engine.SegmentationCalls);
    }

    [Fact]
    public void ModelManager_Dispose_DisposesEngine()
    {
        var factory = new FakeEngineFactory();
        var modelPath = Path.Combine(_folder, "fake.onnx");
        File.WriteAllText(modelPath, "fake");

        var manager = new ModelManager(_folder, factory);
        manager.Open("fake.onnx", InferenceTask.ObjectDetection);
        manager.Dispose();

        Assert.Equal(1, factory.Engine.DisposedCount);
    }

    [Fact]
    public void ModelManager_MissingFile_FailsBeforeFactory()
    {
        var factory = new FakeEngineFactory();
        using var manager = new ModelManager(_folder, factory);

        Assert.Throws<VisionException>(() => manager.Open("ghost.onnx", InferenceTask.ObjectDetection));
        Assert.Equal(0, factory.Engine.DetectionCalls);
        Assert.Equal(0, factory.Engine.DisposedCount);
    }
}
