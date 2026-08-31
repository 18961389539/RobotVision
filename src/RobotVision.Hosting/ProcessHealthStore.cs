using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RobotVision.Core.IO;
using RobotVision.Core.Models;

namespace RobotVision.Hosting;

/// <summary>过程能力落盘：累计 JSON + 按日 TSV；连续失败联锁判定。</summary>
public sealed class ProcessHealthStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<ProcessHealthStore> _log;
    private readonly object _io = new();
    private ProcessHealthConfig _cfg;
    private readonly string _folder;

    public ProcessHealthStore(ProcessHealthConfig cfg, string folder, ILogger<ProcessHealthStore> log)
    {
        _cfg = cfg;
        _folder = folder;
        _log = log;
    }

    public string Folder => _folder;

    public string StatePath => Path.Combine(_folder, "health.json");

    public void ApplyConfig(ProcessHealthConfig cfg) => _cfg = cfg;

    public bool IsEnabled => _cfg.Enabled;

    public int ConsecutiveFailLimit => Math.Max(0, _cfg.ConsecutiveFailLimit);

    public bool InhibitOnLimit => _cfg.InhibitOnLimit;

    public bool IsInhibited(VisionMetrics metrics, string recipe)
    {
        if (!_cfg.Enabled || !_cfg.InhibitOnLimit || _cfg.ConsecutiveFailLimit <= 0)
            return false;
        return metrics.GetConsecutiveFails(recipe) >= _cfg.ConsecutiveFailLimit;
    }

    public bool AnyInhibited(VisionMetrics metrics)
    {
        if (!_cfg.Enabled || !_cfg.InhibitOnLimit || _cfg.ConsecutiveFailLimit <= 0)
            return false;
        return metrics.MaxConsecutiveFails >= _cfg.ConsecutiveFailLimit;
    }

    public void RestoreInto(VisionMetrics metrics)
    {
        // 无论当前是否启用联锁都恢复累计：启动时关闭再打开不得用空统计覆盖 health.json。
        try
        {
            if (!File.Exists(StatePath))
                return;
            var dto = JsonSerializer.Deserialize<HealthFile>(File.ReadAllText(StatePath), JsonOptions);
            if (dto?.Recipes is null)
                return;
            metrics.Restore(dto.Recipes.Select(r => new RecipeStatsSnapshot(
                r.Recipe, r.Total, r.Ok, r.Failed, r.AvgMs, r.LastMs, r.LastAt, r.ConsecutiveFails)));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "恢复过程能力统计失败（将从零开始）");
        }
    }

    /// <summary>只写累计 JSON（解除联锁后不追加 TSV 行）。</summary>
    public void PersistState(VisionMetrics metrics)
    {
        if (!_cfg.Enabled)
            return;
        try
        {
            lock (_io)
            {
                Directory.CreateDirectory(_folder);
                WriteState(metrics);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "过程能力状态落盘失败");
        }
    }

    public void OnCompleted(VisionResult result, VisionMetrics metrics)
    {
        if (!_cfg.Enabled)
            return;

        try
        {
            lock (_io)
            {
                Directory.CreateDirectory(_folder);
                AppendTsv(result);
                WriteState(metrics);
                CleanupOldTsv();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "过程能力落盘失败（不影响管线）");
        }
    }

    private void AppendTsv(VisionResult result)
    {
        var path = Path.Combine(_folder, DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".tsv");
        var headerNeeded = !File.Exists(path);
        using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
        if (headerNeeded)
            writer.WriteLine("time\trecipe\tok\terror\telapsedMs");
        writer.WriteLine(string.Join('\t',
            DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            Sanitize(result.RecipeName),
            result.Ok ? "1" : "0",
            result.Ok ? "0" : ((int)result.ErrorCode).ToString(CultureInfo.InvariantCulture),
            result.ElapsedMs.ToString("0.###", CultureInfo.InvariantCulture)));
    }

    private void WriteState(VisionMetrics metrics)
    {
        var dto = new HealthFile
        {
            SavedAt = DateTime.Now,
            Recipes = [.. metrics.GetRecipeStats().Select(s => new HealthRecipeRow
            {
                Recipe = s.Recipe,
                Total = s.Total,
                Ok = s.Ok,
                Failed = s.Failed,
                AvgMs = s.AvgMs,
                LastMs = s.LastMs,
                LastAt = s.LastAt,
                ConsecutiveFails = s.ConsecutiveFails,
            })],
        };
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        // 原子落盘统一走 AtomicFile：原实现无 finally，写失败会残留 .tmp 文件
        AtomicFile.WriteAllText(StatePath, json);
    }

    private void CleanupOldTsv()
    {
        if (_cfg.RetainedDays <= 0 || !Directory.Exists(_folder))
            return;
        var cutoff = DateTime.Now.Date.AddDays(-_cfg.RetainedDays);
        foreach (var file in Directory.EnumerateFiles(_folder, "????????.tsv"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Length == 8 &&
                DateTime.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day) &&
                day < cutoff)
            {
                try { File.Delete(file); }
                catch (IOException) { }
            }
        }
    }

    private static string Sanitize(string value) =>
        value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

    private sealed class HealthFile
    {
        public DateTime SavedAt { get; set; }
        public List<HealthRecipeRow> Recipes { get; set; } = [];
    }

    private sealed class HealthRecipeRow
    {
        public string Recipe { get; set; } = "";
        public long Total { get; set; }
        public long Ok { get; set; }
        public long Failed { get; set; }
        public double AvgMs { get; set; }
        public double LastMs { get; set; }
        public DateTime? LastAt { get; set; }
        public int ConsecutiveFails { get; set; }
    }
}
