using System.Collections.Concurrent;
using RobotVision.Core;
using RobotVision.Core.Assets;
using RobotVision.Core.Models;

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
/// 会话可能因模型卸载/文件替换/裁剪而失效：失效后的 Run 抛 ModelNotAvailable（可重试，
/// 重试会加载新会话），绝不触碰已释放的引擎——"卸载与推理竞态"由此闭环。
/// </summary>
public sealed class ModelSession
{
    private readonly ModelManager.LoadedModel _owner;

    internal ModelSession(ModelManager.LoadedModel owner) => _owner = owner;

    public T Run<T>(Func<IInferenceEngine, T> inference, CancellationToken ct = default)
    {
        _owner.Gate.Wait(ct);
        try
        {
            if (_owner.Unloaded)
                throw new VisionException(VisionErrorCode.ModelNotAvailable,
                    "模型会话已被卸载（模型文件可能刚被替换），请重试");
            return inference(_owner.Engine);
        }
        finally
        {
            _owner.Gate.Release();
        }
    }
}

/// <summary>
/// 推理模型管理类：按 (模型路径, 文件版本, 推理任务) 缓存引擎实例，加载后空跑一帧预热，
/// 避免首次请求耗时突增触发机器人超时。
/// 缓存键含文件版本（LastWriteTimeUtc + Length）：替换 .onnx 后 Open 自动加载新版本
/// 并清理旧版本会话——不再依赖人工"先卸载再推理"，杜绝旧模型静默服务。
/// 并发安全设计：
/// - 缓存值为 Lazy：GetOrAdd 的工厂委托可能被并发调用多次，但只有占据字典槽位的
///   Lazy 会真正执行加载，输家的包装对象未物化即被丢弃——不会泄漏 ONNX 会话
///   （数百 MB 级，泄漏一两个就可能 OOM）；
/// - 加载失败不缓存（Lazy 默认缓存异常，须显式移除），模型文件后补后可重试；
/// - 任务参与缓存键：同一文件被不同任务打开时行为确定（各自的预热各自成败），
///   而非"谁先加载谁的任务赢"；
/// - 字典变更统一经 _sync 短锁；加载/预热在锁外执行（不阻塞其他模型的打开）；
/// - 安全卸载：先置卸载标记，再等在途推理离开临界区，最后释放引擎（幂等）——
///   卸载/裁剪/退出永不 Dispose 正在推理的会话。
/// 推理后端（ExecutionProvider/框架）由 <see cref="IInferenceEngineFactory"/> 决定——
/// 默认 YoloDotNet OpenVINO GPU（appsettings 的 Inference:Provider 可配），换框架 =
/// 替换工厂，本类与策略层零改动。
/// </summary>
public sealed class ModelManager(
    string modelsFolder,
    IInferenceEngineFactory? engineFactory = null,
    int maxSessions = 8) : IDisposable
{
    /// <summary>缓存键：路径 + 任务 + 文件版本（mtime/大小），路径大小写不敏感。</summary>
    private readonly record struct ModelKey(string Path, InferenceTask Task, DateTime StampUtc, long Size);

    private sealed class ModelKeyComparer : IEqualityComparer<ModelKey>
    {
        public static readonly ModelKeyComparer Instance = new();

        public bool Equals(ModelKey x, ModelKey y) =>
            string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase)
            && x.Task == y.Task && x.StampUtc == y.StampUtc && x.Size == y.Size;

        // 版本字段不参与哈希（同路径同任务的不同版本落到相近桶，正确性由 Equals 保证）
        public int GetHashCode(ModelKey obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path), (int)obj.Task);
    }

    /// <summary>
    /// 已加载的模型会话：引擎 + 串行化信号量 + 卸载标记。
    /// Dispose 幂等且安全：置 Unloaded → 等待在途推理退出临界区 → 释放引擎。
    /// Gate 故意不 Dispose：可能有并发 Run 正排队等信号量，
    /// SemaphoreSlim.Dispose 对仍有等待者的实例行为未定义（信号量本身无非托管资源）。
    /// </summary>
    internal sealed class LoadedModel(IInferenceEngine engine, SemaphoreSlim gate)
    {
        public IInferenceEngine Engine { get; } = engine;

        public SemaphoreSlim Gate { get; } = gate;

        /// <summary>已卸载标记：已取得 session 的在途/后续 Run 进入临界区后据此失败。</summary>
        public volatile bool Unloaded;

        /// <summary>最近一次被 Open 的时间（TickCount64），LRU 裁剪依据（Volatile/Interlocked 访问）。</summary>
        public long LastAccessTicks = Environment.TickCount64;

        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            Unloaded = true;      // 后续 Run 在临界区内立即失败，不再触碰引擎
            Gate.Wait();          // 等待在途推理完成（推理有限时，必然结束）
            try
            {
                Engine.Dispose();
            }
            finally
            {
                Gate.Release();
            }
        }
    }

    private readonly IInferenceEngineFactory _engineFactory = engineFactory ?? new YoloDotNetEngineFactory();

    /// <summary>LRU 会话上限（0 或负数 = 不限制）。超过上限时卸载最久未使用的会话，防内存持续膨胀。</summary>
    public int MaxSessions { get; } = maxSessions;

    private readonly ConcurrentDictionary<ModelKey, Lazy<LoadedModel>> _models =
        new(ModelKeyComparer.Instance);

    /// <summary>字典变更（增删）短锁：加载/预热/等待推理均在锁外，锁内无耗时操作。</summary>
    private readonly object _sync = new();

    private volatile bool _disposed;

    public int LoadedCount
    {
        get { lock (_sync) return _models.Values.Count(l => l.IsValueCreated); }
    }

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

    /// <summary>模型文件是否存在且非空（相对路径按 models 目录解析；绝对路径直接判断）。供配方引用校验。</summary>
    public bool ModelFileExists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        var path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(modelsFolder, fileName);
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>计算模型文件 SHA-256。每次读文件内容（不用 mtime+size 缓存：
    /// 同大小且保留时间戳的覆盖会漏检，而这正是钉扎要拦住的场景）。</summary>
    public string ComputeSha256(string modelFile)
    {
        var path = ResolvePath(modelFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"模型文件不存在: {modelFile}", path);

        return FileSha256.ComputeFile(path);
    }

    /// <summary>已物化的缓存键（供管理界面显示加载状态；按 (路径, 任务) 去重，版本不外露）。</summary>
    public IReadOnlyList<(string Path, InferenceTask Task)> LoadedKeys
    {
        get
        {
            lock (_sync)
            {
                return _models
                    .Where(kv => kv.Value.IsValueCreated)
                    .Select(kv => (kv.Key.Path, kv.Key.Task))
                    .Distinct()
                    .ToList();
            }
        }
    }

    public string ResolvePath(string modelFile) =>
        Path.IsPathRooted(modelFile) ? modelFile : Path.Combine(modelsFolder, modelFile);

    public ModelSession Open(string modelFile, InferenceTask task)
    {
        var path = ResolvePath(modelFile);
        // 文件版本进入缓存键：文件不存在时 LastWriteTimeUtc 为 1601-01-01、Size 记 -1，
        // 由 Load 统一报"文件不存在"
        var info = new FileInfo(path);
        var key = new ModelKey(path, task, info.LastWriteTimeUtc, info.Exists ? info.Length : -1L);

        Lazy<LoadedModel> lazy;
        lock (_sync)
            lazy = _models.GetOrAdd(key, k => new Lazy<LoadedModel>(() => Load(k.Path, k.Task)));

        LoadedModel loaded;
        try
        {
            loaded = lazy.Value; // 加载 + 预热在锁外，不阻塞其他模型的打开
        }
        catch
        {
            // 原子移除"仍属于本失败实例"的槽位：并发下其他线程可能已重试并放入新 Lazy，
            // KVP 版 TryRemove 仅在键与值（引用）都匹配时才删除，不会误删他人的成功项
            lock (_sync)
                _models.TryRemove(KeyValuePair.Create(key, lazy));
            throw;
        }

        // 物化后复查仍在缓存：物化期间条目可能已被卸载/裁剪/整表清空。
        // 不在则本线程自行释放（幂等），杜绝"无人接管的新鲜 ONNX 会话"泄漏
        bool orphaned;
        lock (_sync)
        {
            orphaned = _disposed
                       || !_models.TryGetValue(key, out var current)
                       || !ReferenceEquals(current, lazy);
        }
        if (orphaned)
        {
            loaded.Dispose();
            throw new VisionException(VisionErrorCode.ModelNotAvailable,
                "模型会话已被卸载（模型文件可能刚被替换或触发裁剪），请重试");
        }

        // 记录最近访问（LRU 裁剪依据）、回收超出上限的最旧会话、清理同路径旧版本
        Interlocked.Exchange(ref loaded.LastAccessTicks, Environment.TickCount64);
        TrimToCapacity();
        EvictStaleVersions(key);

        return new ModelSession(loaded);
    }

    /// <summary>
    /// 会话数超过 <see cref="MaxSessions"/> 时，卸载最久未使用的会话（LRU）。
    /// 仅统计已物化条目（未物化的 Lazy 不持有 ONNX 会话，无回收意义）。
    /// 卸载经 LoadedModel.Dispose 安全路径：等待在途推理完成，绝不释放使用中的会话。
    /// </summary>
    private void TrimToCapacity()
    {
        if (MaxSessions <= 0)
            return;

        while (true)
        {
            ModelKey oldest = default;
            var oldestTicks = long.MaxValue;
            int created;
            lock (_sync)
            {
                created = 0;
                foreach (var kv in _models)
                {
                    if (!kv.Value.IsValueCreated)
                        continue;
                    created++;
                    var ticks = Volatile.Read(ref kv.Value.Value.LastAccessTicks);
                    if (ticks < oldestTicks)
                    {
                        oldestTicks = ticks;
                        oldest = kv.Key;
                    }
                }
                if (created <= MaxSessions)
                    return;
            }
            // 锁外移除并等待在途推理；失败（被并发移除）则重算
            if (!TryRemoveEntry(oldest))
                continue;
        }
    }

    /// <summary>
    /// 清理同 (路径, 任务) 的旧版本条目（文件被替换后的孤儿会话）。
    /// 未物化的旧版本 Lazy 一并移除（物化中的线程由 Open 的复查路径自行释放）。
    /// </summary>
    private void EvictStaleVersions(ModelKey keep)
    {
        var comparer = ModelKeyComparer.Instance;
        List<ModelKey> stale;
        lock (_sync)
        {
            stale = _models.Keys
                .Where(k => k.Task == keep.Task
                            && string.Equals(k.Path, keep.Path, StringComparison.OrdinalIgnoreCase)
                            // 必须用字典比较器判断版本差异：record 默认相等对路径大小写敏感，
                            // 会把仅大小写不同的同键条目误判为旧版本
                            && !comparer.Equals(k, keep))
                .ToList();
        }
        foreach (var key in stale)
            TryRemoveEntry(key);
    }

    /// <summary>按 (模型路径, 推理任务) 卸载当前版本及所有历史版本缓存，供配方重配/模型文件替换时调用。</summary>
    public void Unload(string modelFile, InferenceTask task)
    {
        var path = ResolvePath(modelFile);
        List<ModelKey> keys;
        lock (_sync)
        {
            keys = _models.Keys
                .Where(k => k.Task == task
                            && string.Equals(k.Path, path, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        foreach (var key in keys)
            TryRemoveEntry(key);
    }

    /// <summary>按模型文件卸载其全部任务会话（模型文件被替换时用；文件名大小写不敏感）。</summary>
    public void UnloadAll(string modelFile)
    {
        var path = ResolvePath(modelFile);
        List<ModelKey> keys;
        lock (_sync)
        {
            keys = _models.Keys
                .Where(k => string.Equals(k.Path, path, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        foreach (var key in keys)
            TryRemoveEntry(key);
    }

    /// <summary>卸载全部会话并释放（模型目录整体更换/退出前清理）。</summary>
    public void UnloadAll()
    {
        List<ModelKey> keys;
        lock (_sync)
            keys = _models.Keys.ToList();
        foreach (var key in keys)
            TryRemoveEntry(key);
    }

    /// <summary>
    /// 原子移除缓存条目；已物化的经安全路径释放（等待在途推理，幂等）。
    /// 返回是否由本调用移除（false = 已被并发移除）。
    /// </summary>
    private bool TryRemoveEntry(ModelKey key)
    {
        Lazy<LoadedModel>? lazy = null;
        lock (_sync)
        {
            if (_models.TryGetValue(key, out var current)
                && _models.TryRemove(KeyValuePair.Create(key, current)))
                lazy = current;
        }
        if (lazy is not null && lazy.IsValueCreated)
            lazy.Value.Dispose(); // 锁外等待在途推理，不阻塞其他 Open
        return lazy is not null;
    }

    private LoadedModel Load(string path, InferenceTask task)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new VisionException(VisionErrorCode.ModelNotAvailable, $"模型文件不存在或为空: {path}");

        var engine = _engineFactory.Create(path);

        try
        {
            InferenceTaskValidation.EnsureSupported(engine, task);
            WarmUp(engine, task);
        }
        catch (Exception ex)
        {
            engine.Dispose();
            if (ex is VisionException)
                throw;
            if (ex is InvalidCastException)
                throw new VisionException(VisionErrorCode.ModelNotAvailable,
                    $"模型任务与所选「{InferenceTaskValidation.Label(task)}」不匹配（{ex.Message}）", ex);
            throw new VisionException(VisionErrorCode.ModelNotAvailable,
                $"模型预热失败: {Path.GetFileName(path)}（{task}）: {ex.Message}", ex);
        }

        return new LoadedModel(engine, new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// 空跑一帧触发 ONNX 会话初始化（算子注册/内存分配）。
    /// 用 640×640（常见推理输入）而非最小空白图，与真实输入差异更小，
    /// 避免动态 shape 模型在首次真实推理时仍有额外开销。
    /// </summary>
    private static void WarmUp(IInferenceEngine engine, InferenceTask task)
    {
        using var dummy = VisionImage.AllocateZero(640, 640, 3);
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
        List<Lazy<LoadedModel>> snapshot;
        lock (_sync)
        {
            snapshot = _models.Values.ToList();
            _models.Clear();
        }
        // 锁外逐个安全释放（等待在途推理退出）；物化中的线程由 Open 复查路径自行释放。
        // 幂等保护下与 Open 路径的双重释放不会发生
        foreach (var lazy in snapshot)
            if (lazy.IsValueCreated)
                lazy.Value.Dispose();
    }
}
