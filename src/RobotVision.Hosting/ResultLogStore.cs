using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Hosting;

/// <summary>
/// 一次检测结果的原始留档条目（JSON Lines 一行 = 一次触发）。
/// 字段固定（含首个位姿坐标/角度/置信度与结果码），供：
/// - 产线追溯："这批料 14:00 检的角度是多少？"直接按时间查文件；
/// - 合格率 / 角度位置分布 / 长期趋势：读本机 SQLite 或导入 Excel；
/// 成功与失败共用同一格式（失败行无坐标，Code 为错误码），与 data/failures 现场图互相对照。
/// Poses 为全部目标（JSONL 与 SQLite 同步）；X/Y/Angle 仍取第一个，兼容旧解析。
/// </summary>
public sealed record ResultLogEntry(
    string T, string Recipe, string Station, string Camera,
    double? X, double? Y, double? Angle, double? Confidence,
    int Count, double ElapsedMs, int Code, string Message,
    IReadOnlyList<ResultPoseLog>? Poses = null);

/// <summary>单个目标的机器人位姿（与 <see cref="RobotPose"/> 对应，另附该目标置信度）。</summary>
public sealed record ResultPoseLog(double X, double Y, double Angle, double? Confidence);

/// <summary>
/// 结果日志存储：JSON Lines 按天滚动 + 本机 SQLite（results.db）。
/// 写入为后台异步（管线线程只组装条目），JSONL 与 SQLite 在 _sync 下串行；
/// 任一失败只记日志，不影响检测节拍。
/// </summary>
public sealed class ResultLogStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly string _folder;
    private readonly ILogger<ResultLogStore> _log;
    private readonly SqliteResultStore _sqlite;
    private readonly bool _ownsSqlite;
    private readonly object _sync = new();
    private DateTime _currentDay;
    private string? _currentFile;
    private bool _disposed;

    public ResultLogStore(
        ResultLogConfig cfg,
        ILogger<ResultLogStore> log,
        SqliteResultStore? sqlite = null)
    {
        _folder = AppConfigExtensions.ResolveFolder(cfg.Folder);
        _log = log;
        Enabled = cfg.Enabled;
        JsonlEnabled = cfg.Jsonl;
        RetainedDays = cfg.RetainedDays;
        if (sqlite is null)
        {
            _sqlite = new SqliteResultStore(cfg, NullLogger<SqliteResultStore>.Instance);
            _ownsSqlite = true;
        }
        else
        {
            _sqlite = sqlite;
            _ownsSqlite = false;
        }
        _sqlite.Enabled = cfg.Sqlite;
        _sqlite.RetainedDays = cfg.RetainedDays;
    }

    /// <summary>总开关（热属性）。false 时 JSONL 与 SQLite 都不写。</summary>
    public bool Enabled { get; set; }

    /// <summary>是否追加 JSON Lines。</summary>
    public bool JsonlEnabled { get; set; }

    /// <summary>是否写入 SQLite。</summary>
    public bool SqliteEnabled
    {
        get => _sqlite.Enabled;
        set => _sqlite.Enabled = value;
    }

    /// <summary>按天保留天数；≤0 不清理。</summary>
    public int RetainedDays { get; set; }

    public SqliteResultStore Sqlite => _sqlite;

    /// <summary>保存配置后热应用开关与保留天数（目录/库路径启动时锚定，不热切换）。</summary>
    public void ApplyConfig(ResultLogConfig cfg)
    {
        Enabled = cfg.Enabled;
        JsonlEnabled = cfg.Jsonl;
        RetainedDays = cfg.RetainedDays;
        _sqlite.Enabled = cfg.Sqlite;
        _sqlite.RetainedDays = cfg.RetainedDays;
    }

    /// <summary>
    /// 提交一条结果记录（尽力而为）：序列化在调用线程完成，写盘移到后台线程池，
    /// 任何异常只记日志，绝不影响产线管线。
    /// </summary>
    public void Record(VisionResult result, (string CameraId, string StationId)? context = null)
    {
        if (!Enabled || result is null || _disposed)
            return;
        if (!JsonlEnabled && !SqliteEnabled)
            return;

        try
        {
            var now = DateTimeOffset.Now;
            var poses = ToPoseLogs(result);
            var first = poses.Count > 0 ? poses[0] : null;
            var entry = new ResultLogEntry(
                T: now.ToString("O"),
                Recipe: result.RecipeName,
                Station: context?.StationId ?? "",
                Camera: context?.CameraId ?? "",
                X: first?.X,
                Y: first?.Y,
                Angle: first?.Angle,
                Confidence: first?.Confidence,
                Count: result.Poses.Count,
                ElapsedMs: result.ElapsedMs,
                Code: (int)result.ErrorCode,
                Message: result.Message,
                Poses: poses);
            var line = JsonlEnabled ? JsonSerializer.Serialize(entry, JsonOptions) : null;
            var tUnix = now.ToUnixTimeMilliseconds();

            _ = Task.Run(() => Persist(entry, line, tUnix));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "提交结果日志失败（不影响管线）");
        }
    }

    private void Persist(ResultLogEntry entry, string? line, long tUnix)
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            if (JsonlEnabled && line is not null)
            {
                try { AppendJsonl(line); }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "写结果 JSONL 失败（不影响管线）");
                }
            }

            if (SqliteEnabled)
            {
                try { _sqlite.Insert(entry, tUnix); }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "写结果 SQLite 失败（不影响管线）");
                }
            }
        }
    }

    private void AppendJsonl(string line)
    {
        var now = DateTime.Now;
        if (_currentFile is null || now.Date != _currentDay)
        {
            _currentDay = now.Date;
            _currentFile = Path.Combine(_folder, $"results-{_currentDay:yyyy-MM-dd}.jsonl");
        }
        Directory.CreateDirectory(_folder);
        File.AppendAllText(_currentFile, line + Environment.NewLine);

        if (RetainedDays > 0)
            CleanupJsonl(now);
    }

    /// <summary>删除超过保留天数的 results-*.jsonl（按文件名时间戳判断，与写入时钟一致）。</summary>
    private void CleanupJsonl(DateTime now)
    {
        var cutoff = now.Date.AddDays(-RetainedDays);
        foreach (var file in Directory.EnumerateFiles(_folder, "results-*.jsonl"))
        {
            var name = Path.GetFileNameWithoutExtension(file)["results-".Length..];
            if (DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day) && day < cutoff)
            {
                try { File.Delete(file); }
                catch (Exception ex) { _log.LogWarning(ex, "清理结果日志失败: {File}", file); }
            }
        }
    }

    internal static IReadOnlyList<ResultPoseLog> ToPoseLogs(VisionResult result)
    {
        if (result.Poses.Count == 0)
            return [];
        var list = new ResultPoseLog[result.Poses.Count];
        for (var i = 0; i < result.Poses.Count; i++)
        {
            var pose = result.Poses[i];
            double? confidence = i < result.Confidences.Count ? result.Confidences[i] : null;
            list[i] = new ResultPoseLog(pose.X, pose.Y, pose.AngleDeg, confidence);
        }
        return list;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_ownsSqlite)
                _sqlite.Dispose();
        }
    }
}
