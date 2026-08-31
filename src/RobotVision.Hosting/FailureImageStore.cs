using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;

namespace RobotVision.Hosting;

/// <summary>失败现场留存的诊断上下文（由管线组装，写进 JSON 元数据供排障还原）。</summary>
public sealed record FailureContext(
    string? CameraId = null,
    string? StationId = null,
    string? Models = null,
    string? AngleMode = null,
    double? Confidence = null,
    double? Iou = null,
    string? Source = null);

/// <summary>
/// 失败现场图像留存：推理失败的请求把"模型实际看到的去畸变图"落盘 + JSON 元数据，
/// 供远程排障还原当时画面。
/// 分级留存策略（防 1007 风暴冲刷掉真正有价值的失败）：
/// - 1007 未检出：限流（同配方窗口内只存 1 张）+ 降采样缩图（默认宽 640）；
/// - 其余错误（1003/1005/1099 等）：全量留存、原分辨率；
/// - 滚动清理优先保留非 1007（先删 1007 最旧，再删非 1007 最旧），支持数量与天数双配额。
/// 文件名 {时间戳}_{配方}_{错误码}.png；孤儿 JSON（无对应 PNG）一并清理。
/// 留存是尽力而为：任何 I/O 异常只记日志，绝不影响产线管线；克隆留调用线程完成，
/// PNG 编码/元数据/清理移到后台线程池（_sync 串行），管线不阻塞。
/// </summary>
public sealed class FailureImageStore
{
    private sealed record FailureMeta(
        string Recipe, int ErrorCode, string Message, double ElapsedMs,
        DateTime SavedAt, int Width, int Height,
        string? CameraId, string? StationId, string? Models, string? AngleMode,
        double? Confidence, double? Iou, string? Source);

    private static readonly JsonSerializerOptions MetaJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 中文消息不转义为 \u 序列
    };

    private readonly string _folder;
    private readonly ILogger<FailureImageStore> _log;
    private readonly Func<DateTime> _now;
    private readonly object _sync = new();

    /// <summary>限流状态：键 = (配方, 错误码)，值 = 最近一次留存时间（_sync 内访问）。</summary>
    private readonly Dictionary<(string Recipe, int Code), DateTime> _lastSavedAt = [];

    // 留存统计（_sync 内更新）
    private int _totalSaved;
    private long _totalBytes;
    private DateTime? _lastSaveTime;

    public FailureImageStore(FailureImageConfig cfg, ILogger<FailureImageStore> log, Func<DateTime>? clock = null)
    {
        _folder = AppConfigExtensions.ResolveFolder(cfg.Folder);
        RetainedCount = cfg.RetainedCount;
        RetainedDays = cfg.RetainedDays;
        _log = log;
        _now = clock ?? (() => DateTime.Now);
        Enabled = cfg.Enabled;
    }

    /// <summary>运行时开关（热属性：管理界面可切换）。</summary>
    public bool Enabled { get; set; }

    /// <summary>滚动保留数量（热属性），≤0 表示不按数量清理。</summary>
    public int RetainedCount { get; set; }

    /// <summary>按时间保留天数（热属性），≤0 表示不按时间清理。与数量配额取更严格者。</summary>
    public int RetainedDays { get; set; }

    /// <summary>1007 限流窗口（热属性），同配方同码在此窗口内只存 1 张。</summary>
    public TimeSpan NoTargetThrottle { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>1007 缩图最大宽度（热属性），0 = 不缩图。</summary>
    public int MaxNoTargetWidth { get; set; } = 640;

    /// <summary>留存目录的绝对路径（供管理界面扫描展示）。</summary>
    public string Folder => _folder;

    /// <summary>累计留存文件数（进程内）。</summary>
    public int TotalSaved { get { lock (_sync) return _totalSaved; } }

    /// <summary>累计留存字节数（进程内）。</summary>
    public long TotalBytes { get { lock (_sync) return _totalBytes; } }

    /// <summary>最近一次留存时间（进程内）。</summary>
    public DateTime? LastSavedAt { get { lock (_sync) return _lastSaveTime; } }

    public void Save(string recipeName, VisionImage image, VisionResult failure, FailureContext? context = null)
    {
        if (image.IsEmpty)
            return;
        using var mat = VisionImageCv.AsMat(image);
        Save(recipeName, mat, failure, context);
    }

    /// <summary>
    /// 提交一次失败留存（尽力而为）：克隆（或 1007 缩图）在调用线程完成并立即返回，
    /// PNG 编码/元数据/滚动清理移到后台线程池执行，绝不在管线线程同步落盘拖累产线。
    /// </summary>
    public void Save(string recipeName, Mat image, VisionResult failure, FailureContext? context = null)
    {
        if (!Enabled || failure.Ok || image.Empty())
            return;

        try
        {
            var isNoTarget = failure.ErrorCode == VisionErrorCode.NoTargetFound;

            // 1007 限流：同一 (配方, 1007) 在窗口内只存一张，防未检出风暴冲刷配额
            if (isNoTarget)
            {
                lock (_sync)
                {
                    var key = (recipeName, (int)failure.ErrorCode);
                    var now = _now();
                    if (_lastSavedAt.TryGetValue(key, out var last) && now - last < NoTargetThrottle)
                        return;
                    _lastSavedAt[key] = now;
                }
            }

            // 1007 降采样（省磁盘与内存峰值）；其余错误原分辨率克隆
            Mat clone;
            if (isNoTarget && MaxNoTargetWidth > 0 && image.Width > MaxNoTargetWidth)
                clone = Downscale(image, MaxNoTargetWidth);
            else
                clone = image.Clone();

            var meta = new FailureMeta(
                recipeName, (int)failure.ErrorCode, failure.Message, failure.ElapsedMs,
                _now(), clone.Width, clone.Height,
                context?.CameraId, context?.StationId, context?.Models, context?.AngleMode,
                context?.Confidence, context?.Iou, context?.Source);

            // fire-and-forget：留存是尽力而为，异步异常在 WriteCore 内捕获，绝不污染产线管线
            _ = Task.Run(() => WriteCore(clone, recipeName, failure, meta));
        }
        catch (Exception ex)
        {
            // 克隆/入队阶段异常同样尽力而为
            FailureImageStoreLog.EnqueueFailed(_log, ex);
        }
    }

    /// <summary>后台线程实际落盘：PNG + JSON 元数据 + 统计 + 滚动清理（_sync 串行）。</summary>
    private void WriteCore(Mat image, string recipeName, VisionResult failure, FailureMeta meta)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_folder);

                var baseName = $"{meta.SavedAt:yyyyMMdd_HHmmssfff}_{recipeName}_{meta.ErrorCode}";
                var png = Path.Combine(_folder, baseName + ".png");
                for (var i = 1; File.Exists(png); i++)
                    png = Path.Combine(_folder, $"{baseName}_{i}.png");

                Cv2.ImWrite(png, image);
                var jsonPath = Path.ChangeExtension(png, ".json");
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(meta, MetaJsonOptions));

                _totalSaved++;
                _totalBytes += new FileInfo(png).Length + new FileInfo(jsonPath).Length;
                _lastSaveTime = meta.SavedAt;

                if (RetainedCount > 0 || RetainedDays > 0)
                    Cleanup();

                FailureImageStoreLog.Saved(_log, png, meta.ErrorCode, meta.Message);
            }
        }
        catch (Exception ex)
        {
            FailureImageStoreLog.SaveFailed(_log, ex);
        }
        finally
        {
            // 后台线程持有克隆副本的所有权，落盘后释放
            image.Dispose();
        }
    }

    /// <summary>
    /// 滚动清理：1) 删除孤儿 JSON（无对应 PNG）；2) 超过 RetainedDays 的删除
    /// （按文件名时间戳判断，与留存时钟一致，不依赖文件系统时间）；
    /// 3) 超过 RetainedCount 时按优先级删除——1007 优先（最旧先删），非 1007 仅当 1007 删完仍超配额才删。
    /// </summary>
    private void Cleanup()
    {
        // 孤儿 JSON 清理
        var pngs = Directory.GetFiles(_folder, "*.png").ToList();
        var jsonFiles = Directory.GetFiles(_folder, "*.json");
        foreach (var json in jsonFiles)
        {
            if (!pngs.Any(p => string.Equals(Path.ChangeExtension(p, ".json"), json, StringComparison.OrdinalIgnoreCase)))
                TryDelete(json);
        }

        if (RetainedDays > 0)
        {
            var cutoff = _now().AddDays(-RetainedDays);
            foreach (var png in pngs.ToList())
            {
                if (TryParseFileTimestamp(png, out var savedAt) && savedAt < cutoff)
                {
                    TryDelete(png);
                    TryDelete(Path.ChangeExtension(png, ".json"));
                    pngs.Remove(png);
                }
            }
        }

        if (RetainedCount <= 0 || pngs.Count <= RetainedCount)
            return;

        // 保留优先级：非 1007 优先；组内文件名（时间戳前缀）新→旧
        var ordered = pngs
            .OrderBy(p => IsNoTargetFile(p) ? 1 : 0)
            .ThenByDescending(p => p, StringComparer.Ordinal)
            .ToList();

        while (ordered.Count > RetainedCount)
        {
            var victim = ordered[^1];
            ordered.RemoveAt(ordered.Count - 1);
            TryDelete(victim);
            TryDelete(Path.ChangeExtension(victim, ".json"));
        }
    }

    /// <summary>从文件名前缀解析留存时间戳（yyyyMMdd_HHmmssfff），解析失败视为无法判断（不删）。</summary>
    private static bool TryParseFileTimestamp(string png, out DateTime savedAt)
    {
        var name = Path.GetFileNameWithoutExtension(png);
        var firstUnderscore = name.IndexOf('_', StringComparison.Ordinal);
        var secondUnderscore = firstUnderscore > 0 ? name.IndexOf('_', firstUnderscore + 1) : -1;
        if (secondUnderscore <= 0)
        {
            savedAt = default;
            return false;
        }
        var timestamp = name[..secondUnderscore]; // yyyyMMdd_HHmmssfff
        return DateTime.TryParseExact(timestamp, "yyyyMMdd_HHmmssfff",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out savedAt);
    }

    private static bool IsNoTargetFile(string png) =>
        Path.GetFileName(png).EndsWith("_1007.png", StringComparison.OrdinalIgnoreCase);

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            FailureImageStoreLog.CleanupFailed(_log, ex, path);
        }
    }

    private static Mat Downscale(Mat src, int maxWidth)
    {
        var ratio = (double)maxWidth / src.Width;
        var size = new Size(maxWidth, (int)Math.Round(src.Height * ratio));
        var dst = new Mat();
        Cv2.Resize(src, dst, size, 0, 0, InterpolationFlags.Area);
        return dst;
    }
}
