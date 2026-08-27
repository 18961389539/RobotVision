using System.Collections.Concurrent;
using System.Linq;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// ModelManager 并发安全测试：
/// - Lazy 物化：同 key 并发 Open 只加载一次（无泄漏的重复 ONNX 会话）；
/// - 失败不缓存：文件缺失抛 ModelNotAvailable，且不留在缓存里（后补模型可重试）；
/// - 任务参与缓存键：同一文件不同任务互不串扰；
/// - 安全卸载：卸载等待在途推理完成，绝不释放使用中的会话（FAKE 引擎确定性验证）；
/// - 文件版本缓存键：替换 .onnx 后自动加载新版本并清理旧会话。
/// REAL 系列依赖仓库内真实 ONNX 模型（加载+预热约 1s），模型缺失时跳过。
/// </summary>
public class ModelManagerRaceTests : IDisposable
{
    /// <summary>仓库内真实 ONNX 模型（多路径探测，兼容不同检出位置；不存在时 REAL 测试跳过）。</summary>
    private static readonly string RealModel = new[]
    {
        @"D:\projects\公司项目\光模块\RobotVision\models\a01_kpt.onnx",
        @"d:\Code\RobotVision\models\a01_kpt.onnx",
    }.FirstOrDefault(File.Exists) ?? "";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rv_mm_" + Guid.NewGuid().ToString("N"));

    public ModelManagerRaceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Open_MissingFile_ThrowsModelNotAvailable()
    {
        using var manager = new ModelManager(_folder);

        var ex = Assert.Throws<VisionException>(
            () => manager.Open("ghost.onnx", InferenceTask.PoseEstimation));

        Assert.Equal(VisionErrorCode.ModelNotAvailable, ex.ErrorCode);
        Assert.Equal(0, manager.LoadedCount);
    }

    [Fact]
    public void Open_MissingFile_ConcurrentCallers_AllSeeFailure_NothingCached()
    {
        using var manager = new ModelManager(_folder);
        var failures = new ConcurrentQueue<VisionException>();
        var unexpected = new ConcurrentQueue<Exception>();

        Parallel.For(0, 8, _ =>
        {
            try
            {
                manager.Open("ghost.onnx", InferenceTask.PoseEstimation);
            }
            catch (VisionException ex)
            {
                failures.Enqueue(ex);
            }
            catch (Exception ex)
            {
                unexpected.Enqueue(ex);
            }
        });

        Assert.Empty(unexpected);
        Assert.Equal(8, failures.Count);
        Assert.All(failures, ex => Assert.Equal(VisionErrorCode.ModelNotAvailable, ex.ErrorCode));
        Assert.Equal(0, manager.LoadedCount);
    }

    [Fact]
    public void Open_MissingFile_FailureNotCached_RetrySucceedsAfterFileArrives()
    {
        if (!File.Exists(RealModel))
            return; // 运维场景：启动时模型未就位，后补模型文件后应能重试成功

        using var manager = new ModelManager(_folder);

        Assert.Throws<VisionException>(() => manager.Open("late.onnx", InferenceTask.PoseEstimation));
        Assert.Equal(0, manager.LoadedCount);

        File.Copy(RealModel, Path.Combine(_folder, "late.onnx"));
        var session = manager.Open("late.onnx", InferenceTask.PoseEstimation);

        Assert.Equal(1, manager.LoadedCount);
    }

    [Fact]
    public void Open_SameModelConcurrent_LoadsExactlyOnce()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder);
        var sessions = new ConcurrentQueue<ModelSession>();

        Parallel.For(0, 8, _ => sessions.Enqueue(manager.Open(RealModel, InferenceTask.PoseEstimation)));

        Assert.Equal(8, sessions.Count);
        Assert.Equal(1, manager.LoadedCount); // Lazy 保证单一物化，并发 GetOrAdd 不产生输家泄漏
    }

    [Fact]
    public void Open_TaskIsPartOfCacheKey_FailedTaskDoesNotEvictWorkingOne()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder);

        // 正确任务先加载成功
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        Assert.Equal(1, manager.LoadedCount);

        // 同一文件 + 错误任务（pose 模型跑分割）：预热失败，失败项不进缓存
        Assert.ThrowsAny<Exception>(() => manager.Open(RealModel, InferenceTask.Segmentation));
        Assert.Equal(1, manager.LoadedCount);

        // 原会话不受干扰，且再次打开不再重复加载
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        Assert.Equal(1, manager.LoadedCount);
    }

    [Fact]
    public void Open_PathCaseInsensitive_SharesCache()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder);

        manager.Open(RealModel, InferenceTask.PoseEstimation);
        var upper = RealModel.ToUpperInvariant();
        manager.Open(upper, InferenceTask.PoseEstimation);

        Assert.Equal(1, manager.LoadedCount);
    }

    [Fact]
    public void Dispose_AfterFailedLoad_DoesNotThrow()
    {
        using var manager = new ModelManager(_folder);
        Assert.Throws<VisionException>(() => manager.Open("ghost.onnx", InferenceTask.ObjectDetection));
        manager.Dispose(); // 缓存为空/仅含未物化项，Dispose 不应触发加载或抛异常
    }

    [Fact]
    public void Unload_RemovesSingleTask_AndReloadsOnNextOpen()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder);
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        Assert.Equal(1, manager.LoadedCount);

        manager.Unload(RealModel, InferenceTask.PoseEstimation);
        Assert.Equal(0, manager.LoadedCount);

        // 卸载后再次打开重新加载（模型文件被替换场景的核心行为）
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        Assert.Equal(1, manager.LoadedCount);
    }

    [Fact]
    public void UnloadAll_File_RemovesEveryTaskOfThatModel()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder);

        // 同一文件加载两个任务（pose 成功；分割对关键点模型预热失败，故用文件拷贝验证多任务）
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        var copy = Path.Combine(_folder, "multi_copy.onnx");
        File.Copy(RealModel, copy);
        manager.Open(copy, InferenceTask.PoseEstimation);
        Assert.Equal(2, manager.LoadedCount);

        manager.UnloadAll(copy);
        Assert.Equal(1, manager.LoadedCount); // 只卸载 copy，原文件会话保留

        manager.UnloadAll(RealModel);
        Assert.Equal(0, manager.LoadedCount);
    }

    [Fact]
    public void UnloadAll_Everything_ClearsCache()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder);
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        var copy = Path.Combine(_folder, "all_copy.onnx");
        File.Copy(RealModel, copy);
        manager.Open(copy, InferenceTask.PoseEstimation);
        Assert.Equal(2, manager.LoadedCount);

        manager.UnloadAll();
        Assert.Equal(0, manager.LoadedCount);
    }

    [Fact]
    public void MaxSessions_TrimsOldestUnusedSession()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder, maxSessions: 2);

        // 三个不同的模型文件依次打开：第三个触发 LRU 裁剪，最旧的第一个被卸载
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        var copy2 = Path.Combine(_folder, "trim_2.onnx");
        var copy3 = Path.Combine(_folder, "trim_3.onnx");
        File.Copy(RealModel, copy2);
        File.Copy(RealModel, copy3);
        manager.Open(copy2, InferenceTask.PoseEstimation);
        manager.Open(copy3, InferenceTask.PoseEstimation);

        Assert.Equal(2, manager.LoadedCount); // 超出上限后自动回收最旧会话
        Assert.DoesNotContain(RealModel, manager.LoadedKeys.Select(k => k.Path),
            StringComparer.OrdinalIgnoreCase); // 最旧的 RealModel 会话被裁剪
    }

    [Fact]
    public void MaxSessions_Zero_MeansUnlimited()
    {
        if (!File.Exists(RealModel))
            return;

        using var manager = new ModelManager(_folder, maxSessions: 0);
        manager.Open(RealModel, InferenceTask.PoseEstimation);
        var copy = Path.Combine(_folder, "unlimited_copy.onnx");
        File.Copy(RealModel, copy);
        manager.Open(copy, InferenceTask.PoseEstimation);

        Assert.Equal(2, manager.LoadedCount); // 0 = 不限制，全部保留
    }

    [Fact]
    public async Task Unload_DuringInference_WaitsForCompletion_NextRunThrowsButNoCrash()
    {
        var file = Path.Combine(_folder, "race.onnx");
        File.WriteAllText(file, "fake-bytes");
        var factory = new FakeEngineFactory();
        using var manager = new ModelManager(_folder, factory);

        var session = manager.Open(file, InferenceTask.ObjectDetection);
        Assert.Equal(1, factory.CreateCount);

        var entered = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var engine = factory.LastEngine!;
        engine.OnRun = () =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
        };

        using var image = VisionImage.AllocateZero(8, 8, 3);
        var runTask = Task.Run(() => session.Run<int>(e =>
        {
            e.RunObjectDetection(image, 0.5, 0.5);
            return 0;
        }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "推理未启动");

        var unloadTask = Task.Run(() => manager.Unload(file, InferenceTask.ObjectDetection));
        await Task.Delay(300);
        Assert.False(unloadTask.IsCompleted);

        release.Set();
        await unloadTask.WaitAsync(TimeSpan.FromSeconds(10));
        await runTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(runTask.IsFaulted);
        Assert.Equal(1, engine.DisposedCount);

        var ex = Assert.Throws<VisionException>(() => session.Run<int>(e => 0));
        Assert.Equal(VisionErrorCode.ModelNotAvailable, ex.ErrorCode);

        manager.Open(file, InferenceTask.ObjectDetection);
        Assert.Equal(2, factory.CreateCount);
        Assert.Equal(1, manager.LoadedCount);
    }

    [Fact]
    public void Open_AfterFileReplaced_LoadsNewVersion_AndEvictsOld()
    {
        var file = Path.Combine(_folder, "ver.onnx");
        File.WriteAllText(file, "v1");
        var factory = new FakeEngineFactory();
        using var manager = new ModelManager(_folder, factory);

        manager.Open(file, InferenceTask.ObjectDetection);
        Assert.Equal(1, factory.CreateCount);

        // 替换文件：内容（大小）与 mtime 均变化 → 新版本键
        File.WriteAllText(file, "v2-with-different-length");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(10));

        manager.Open(file, InferenceTask.ObjectDetection);
        Assert.Equal(2, factory.CreateCount);  // 新版本重新加载，绝不静默服务旧模型
        Assert.Equal(1, manager.LoadedCount);  // 旧版本会话被清理，不占双份内存
        Assert.Equal(1, factory.FirstEngine!.DisposedCount); // 旧引擎已释放
    }

    /// <summary>可控时序的假引擎：OnRun 钩子模拟推理临界区，DisposedCount 记录释放。</summary>
    private sealed class FakeEngine : IInferenceEngine
    {
        public InferenceTask? DetectedTask => null;

        public Action? OnRun;

        public int DisposedCount;

        public IReadOnlyList<ObjectDetectionResult> RunObjectDetection(VisionImage image, double confidence = 0.25, double iou = 0.45)
        {
            OnRun?.Invoke();
            return [];
        }

        public IReadOnlyList<InstanceSegmentation> RunSegmentation(VisionImage image, double confidence = 0.25, double pixelConfidence = 0.5, double iou = 0.45)
        {
            OnRun?.Invoke();
            return [];
        }

        public IReadOnlyList<PoseDetectionResult> RunPoseEstimation(VisionImage image, double confidence = 0.25, double iou = 0.45)
        {
            OnRun?.Invoke();
            return [];
        }

        public void Dispose() => DisposedCount++;
    }

    private sealed class FakeEngineFactory : IInferenceEngineFactory
    {
        public int CreateCount;

        public FakeEngine? FirstEngine { get; private set; }

        public FakeEngine? LastEngine { get; private set; }

        public IInferenceEngine Create(string modelPath)
        {
            Interlocked.Increment(ref CreateCount);
            var engine = new FakeEngine();
            FirstEngine ??= engine;
            LastEngine = engine;
            return engine;
        }
    }
}
