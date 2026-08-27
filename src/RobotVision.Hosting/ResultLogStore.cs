using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Models;

namespace RobotVision.Hosting;

/// <summary>
/// 一次检测结果的原始留档条目（JSON Lines 一行 = 一次触发）。
/// 字段固定（含首个位姿坐标/角度/置信度与结果码），供：
/// - 产线追溯："这批料 14:00 检的角度是多少？"直接按时间查文件；
/// - 合格率 / 角度位置分布 / 长期趋势：原始数据可导入 SQLite/Excel 分析；
/// 成功与失败共用同一格式（失败行无坐标，Code 为错误码），与 data/failures 现场图互相对照。
/// </summary>
public sealed record ResultLogEntry(
    string T, string Recipe, string Station, string Camera,
    double? X, double? Y, double? Angle, double? Confidence,
    int Count, double ElapsedMs, int Code, string Message);

/// <summary>
/// 结果日志存储：JSON Lines 按天滚动文件（data/results/results-yyyy-MM-dd.jsonl）。
/// 写入为后台异步追加（管线线程只序列化一行 JSON，绝不阻塞检测节拍），
/// 同文件写盘在 _sync 下串行；超期文件自动清理。
/// </summary>
public sealed class ResultLogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly string _folder;
    private readonly ILogger<ResultLogStore> _log;
    private readonly object _sync = new();
    private DateTime _currentDay;
    private string? _currentFile;

    public ResultLogStore(ResultLogConfig cfg, ILogger<ResultLogStore> log)
    {
        _folder = AppConfigExtensions.ResolveFolder(cfg.Folder);
        _log = log;
        Enabled = cfg.Enabled;
        RetainedDays = cfg.RetainedDays;
    }

    /// <summary>运行时开关（热属性；管理界面可切换）。</summary>
    public bool Enabled { get; set; }

    /// <summary>按天保留天数；≤0 不清理。</summary>
    public int RetainedDays { get; set; }

    /// <summary>
    /// 提交一条结果记录（尽力而为）：序列化在调用线程完成，写盘移到后台线程池，
    /// 任何异常只记日志，绝不影响产线管线。
    /// </summary>
    public void Record(VisionResult result, (string CameraId, string StationId)? context = null)
    {
        if (!Enabled || result is null)
            return;

        try
        {
            var first = result.Poses.Count > 0 ? result.Poses[0] : null;
            var entry = new ResultLogEntry(
                T: DateTime.Now.ToString("O"),
                Recipe: result.RecipeName,
                Station: context?.StationId ?? "",
                Camera: context?.CameraId ?? "",
                X: first?.X,
                Y: first?.Y,
                Angle: first?.AngleDeg,
                Confidence: result.Confidences.Count > 0 ? result.Confidences[0] : null,
                Count: result.Poses.Count,
                ElapsedMs: result.ElapsedMs,
                Code: (int)result.ErrorCode,
                Message: result.Message);
            var line = JsonSerializer.Serialize(entry, JsonOptions);

            // fire-and-forget：写盘失败由 Append 内捕获，不污染管线
            _ = Task.Run(() => Append(line));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "提交结果日志失败（不影响管线）");
        }
    }

    /// <summary>后台线程实际追加（_sync 串行，同文件按天切换）。</summary>
    private void Append(string line)
    {
        try
        {
            lock (_sync)
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
                    Cleanup(now);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "写结果日志失败（不影响管线）");
        }
    }

    /// <summary>删除超过保留天数的 results-*.jsonl（按文件名时间戳判断，与写入时钟一致）。</summary>
    private void Cleanup(DateTime now)
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
}
