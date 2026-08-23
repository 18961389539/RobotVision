using System.Collections.Concurrent;
using RobotVision.Core;
using RobotVision.Core.Models;
using SkiaSharp;

namespace RobotVision.Infrastructure.Inference;

public enum InferenceTask
{
    ObjectDetection,
    Segmentation,
    PoseEstimation,
}

/// <summary>
/// 模型会话：持有推理引擎的访问句柄。同一模型的推理通过内部信号量串行化
/// （引擎实例非线程安全）。会话本身不拥有资源，无需 Dispose。
/// 等待信号量阶段响应取消（多工位共用模型排队时超时可中断）；推理本身不可取消。
/// 推理调用以 Func&lt;IInferenceEngine,T&gt; 委托传给调用方——策略层只依赖抽象，
/// 不感知具体推理框架（YoloDotNet/ONNX 等）。
/// </summary>
public sealed class ModelSession(IInferenceEngine engine, SemaphoreSlim gate)
{
    public T Run<T>(Func<IInferenceEngine, T> inference, CancellationToken ct = default)
    {
        gate.Wait(ct);
        try
        {
            return inference(engine);
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>
/// 推理模型管理类：按 (模型路径, 推理任务) 缓存引擎实例，加载后空跑一帧预热，
/// 避免首次请求耗时突增触发机器人超时。
/// 并发安全设计：
/// - 缓存值为 Lazy：GetOrAdd 的工厂委托可能被并发调用多次，但只有占据字典槽位的
///   Lazy 会真正执行加载，输家的包装对象未物化即被丢弃——不会泄漏 ONNX 会话
///   （数百 MB 级，泄漏一两个就可能 OOM）；
/// - 加载失败不缓存（Lazy 默认缓存异常，须显式移除），模型文件后补后可重试；
/// - 任务参与缓存键：同一文件被不同任务打开时行为确定（各自的预热各自成败），
///   而非"谁先加载谁的任务赢"。
/// 推理后端（ExecutionProvider/框架）由 <see cref="IInferenceEngineFactory"/> 决定——
/// 默认 YoloDotNet CPU，换 GPU/框架 = 替换工厂，本类与策略层零改动。
/// </summary>
public sealed class ModelManager(string modelsFolder, IInferenceEngineFactory? engineFactory = null) : IDisposable
{
    private readonly IInferenceEngineFactory _engineFactory = engineFactory ?? new YoloDotNetEngineFactory();

    private sealed record LoadedModel(IInferenceEngine Engine, SemaphoreSlim Gate) : IDisposable
    {
        public void Dispose()
        {
            Engine.Dispose();
            Gate.Dispose();
        }
    }

    private sealed class ModelKeyComparer : IEqualityComparer<(string Path, InferenceTask Task)>
    {
        public static readonly ModelKeyComparer Instance = new();

        public bool Equals((string Path, InferenceTask Task) x, (string Path, InferenceTask Task) y) =>
            string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase) && x.Task == y.Task;

        public int GetHashCode((string Path, InferenceTask Task) obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path) ^ ((int)obj.Task << 8);
    }

    private readonly ConcurrentDictionary<(string Path, InferenceTask Task), Lazy<LoadedModel>> _models =
        new(ModelKeyComparer.Instance);

    private volatile bool _disposed;

    public int LoadedCount => _models.Values.Count(l => l.IsValueCreated);

    public string ModelsFolder => modelsFolder;

    /// <summary>models 目录下所有 .onnx 文件名（供配方编辑下拉选择）。</summary>
    public string[] ModelFileNames =>
        Directory.Exists(modelsFolder)
            ? Directory.EnumerateFiles(modelsFolder, "*.onnx", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .Cast<string>()
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

    /// <summary>模型文件是否存在（相对路径按 models 目录解析；绝对路径直接判断）。供配方引用校验。</summary>
    public bool ModelFileExists(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        File.Exists(Path.IsPathRooted(fileName) ? fileName : Path.Combine(modelsFolder, fileName));

    /// <summary>已物化的缓存键（供管理界面显示加载状态）。</summary>
    public IReadOnlyList<(string Path, InferenceTask Task)> LoadedKeys =>
        _models.Where(kv => kv.Value.IsValueCreated)
            .Select(kv => (kv.Key.Path, kv.Key.Task))
            .ToList();

    public string ResolvePath(string modelFile) =>
        Path.IsPathRooted(modelFile) ? modelFile : Path.Combine(modelsFolder, modelFile);

    public ModelSession Open(string modelFile, InferenceTask task)
    {
        var path = ResolvePath(modelFile);
        var key = (path, task);

        var lazy = _models.GetOrAdd(key, k => new Lazy<LoadedModel>(() => Load(k.Path, k.Task)));

        LoadedModel loaded;
        try
        {
            loaded = lazy.Value;
        }
        catch
        {
            // 原子移除"仍属于本失败实例"的槽位：并发下其他线程可能已重试并放入新 Lazy，
            // KVP 版 TryRemove 仅在键与值（引用）都匹配时才删除，不会误删他人的成功项
            _models.TryRemove(KeyValuePair.Create(key, lazy));
            throw;
        }

        // 缓存命中路径同样复查 _disposed：Dispose 可能发生在 GetOrAdd 与取 Value 之间，
        // 此时返回指向已释放 ONNX 会话的句柄会让后续推理崩溃（会话释放后不可用）
        if (_disposed)
            throw new VisionException(VisionErrorCode.ModelNotAvailable, "模型管理器已释放，无法打开会话");

        return new ModelSession(loaded.Engine, loaded.Gate);
    }

    /// <summary>
    /// 按 (模型路径, 推理任务) 卸载缓存并 Dispose 对应会话，供配方重配/模型文件替换时调用。
    /// 未物化的条目直接移除缓存（未持有 ONNX 会话，无资源可释放）。
    /// 并发安全：ConcurrentDictionary 的 TryRemove 原子，Dispose 与该条目的后续 Open 互不干扰
    /// （Open 已物化时拿到旧实例后由 _disposed 复查或本方法释放兜底）。
    /// </summary>
    public void Unload(string modelFile, InferenceTask task)
    {
        var key = (ResolvePath(modelFile), task);
        if (_models.TryRemove(key, out var lazy) && lazy.IsValueCreated)
            lazy.Value.Dispose();
    }

    private LoadedModel Load(string path, InferenceTask task)
    {
        if (!File.Exists(path))
            throw new VisionException(VisionErrorCode.ModelNotAvailable, $"模型文件不存在: {path}");

        var engine = _engineFactory.Create(path);

        try
        {
            WarmUp(engine, task);
        }
        catch (Exception ex)
        {
            engine.Dispose(); // 预热失败也要释放已创建的 ONNX 会话
            throw new VisionException(VisionErrorCode.ModelNotAvailable,
                $"模型预热失败: {Path.GetFileName(path)}（{task}）: {ex.Message}", ex);
        }

        var loaded = new LoadedModel(engine, new SemaphoreSlim(1, 1));
        // Dispose 与加载竞态闭环：Dispose 遍历时该 Lazy 尚未物化（跳过），
        // 加载完成后发现已 Dispose 则立即释放会话，避免加载中的 ONNX 泄漏
        if (_disposed)
        {
            loaded.Dispose();
            throw new ObjectDisposedException(nameof(ModelManager));
        }
        return loaded;
    }

    /// <summary>
    /// 空跑一帧触发 ONNX 会话初始化（算子注册/内存分配）。
    /// 用 640×640（常见推理输入）而非最小空白图，与真实输入差异更小，
    /// 避免动态 shape 模型在首次真实推理时仍有额外开销。
    /// </summary>
    private static void WarmUp(IInferenceEngine engine, InferenceTask task)
    {
        using var dummy = new SKBitmap(new SKImageInfo(640, 640, SKColorType.Bgra8888, SKAlphaType.Opaque));
        switch (task)
        {
            case InferenceTask.ObjectDetection:
                engine.RunObjectDetection(dummy, 0.99);
                break;
            case InferenceTask.Segmentation:
                engine.RunSegmentation(dummy, 0.99);
                break;
            case InferenceTask.PoseEstimation:
                engine.RunPoseEstimation(dummy, 0.99);
                break;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var lazy in _models.Values)
            if (lazy.IsValueCreated) // 未物化的 Lazy 访问 Value 反而会触发加载
                lazy.Value.Dispose();
        _models.Clear();
    }
}
