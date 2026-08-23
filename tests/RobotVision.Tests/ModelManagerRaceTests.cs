using System.Collections.Concurrent;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// ModelManager 并发安全测试：
/// - Lazy 物化：同 key 并发 Open 只加载一次（无泄漏的重复 ONNX 会话）；
/// - 失败不缓存：文件缺失抛 ModelNotAvailable，且不留在缓存里（后补模型可重试）；
/// - 任务参与缓存键：同一文件不同任务互不串扰。
/// REAL 系列依赖仓库内真实 ONNX 模型（加载+预热约 1s），模型缺失时跳过。
/// </summary>
public class ModelManagerRaceTests : IDisposable
{
    private static readonly string RealModel = @"d:\Code\RobotVision\models\a01_kpt.onnx";

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
}
