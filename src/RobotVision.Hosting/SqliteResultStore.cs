using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

/// <summary>分析页/追溯查询条件。Limit 上限 10000，避免一次把整库拉进 UI。</summary>
public sealed class ResultDbQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Recipe { get; init; }
    public int? Code { get; init; }
    /// <summary>
    /// true = 仅合格（code=0）；false = 仅失败（code≠0）；null = 不按成败筛。
    /// 与 <see cref="Code"/> 同时设置时以 Code 为准。
    /// </summary>
    public bool? OkOnly { get; init; }
    public int Limit { get; init; } = 500;
    public int Offset { get; init; }
}

/// <summary>SQLite 一行结果（含全部目标位姿；X/Y/Angle 仍是首个目标，与 JSONL 对齐）。</summary>
public sealed record ResultDbRow(
    long Id,
    string T,
    string Recipe,
    string Station,
    string Camera,
    double? X,
    double? Y,
    double? Angle,
    double? Confidence,
    int Count,
    double ElapsedMs,
    int Code,
    string Message,
    IReadOnlyList<ResultPoseLog> Poses);

/// <summary>按筛选条件聚合，供分析页卡片（合格率/平均位姿/平均耗时）。</summary>
public sealed record ResultDbSummary(
    long Total,
    long Ok,
    long Failed,
    double? AvgX,
    double? AvgY,
    double? AvgAngle,
    double? AvgMs);

/// <summary>错误码出现次数（分析页分布）。</summary>
public sealed record ResultCodeCount(int Code, long Count);

/// <summary>
/// 本机 SQLite 结果库（WAL）：检测线程不碰连接，由 <see cref="ResultLogStore"/> 在后台插入。
/// 查询可与写入并发（WAL）；所有连接操作在 _sync 下串行，避免跨线程共用 SqliteConnection。
/// </summary>
public sealed class SqliteResultStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly ILogger<SqliteResultStore> _log;
    private readonly object _sync = new();
    private SqliteConnection? _conn;
    private bool _disposed;
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public SqliteResultStore(ResultLogConfig cfg, ILogger<SqliteResultStore> log)
    {
        Folder = AppConfigExtensions.ResolveFolder(cfg.Folder);
        DatabasePath = ResolveDbPath(Folder, cfg.SqliteFile);
        _log = log;
        Enabled = cfg.Sqlite;
        RetainedDays = cfg.RetainedDays;
    }

    public string Folder { get; }

    public string DatabasePath { get; }

    /// <summary>是否写入（查询不受此开关影响，已有库仍可读）。</summary>
    public bool Enabled { get; set; }

    /// <summary>保留天数；≤0 不删行。清理最多每小时一次，避免高速节拍下反复 DELETE。</summary>
    public int RetainedDays { get; set; }

    /// <summary>插入一行（调用方已在后台线程）。任何异常抛给调用方记录，不在此处吞掉。</summary>
    public void Insert(ResultLogEntry entry, long tUnixMs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enabled || entry is null)
            return;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO results
                  (t, t_unix, recipe, station, camera, x, y, angle, confidence,
                   count, elapsed_ms, code, message, poses_json)
                VALUES
                  ($t, $t_unix, $recipe, $station, $camera, $x, $y, $angle, $confidence,
                   $count, $elapsed_ms, $code, $message, $poses_json);
                """;
            cmd.Parameters.AddWithValue("$t", entry.T);
            cmd.Parameters.AddWithValue("$t_unix", tUnixMs);
            cmd.Parameters.AddWithValue("$recipe", entry.Recipe);
            cmd.Parameters.AddWithValue("$station", entry.Station);
            cmd.Parameters.AddWithValue("$camera", entry.Camera);
            cmd.Parameters.AddWithValue("$x", (object?)entry.X ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$y", (object?)entry.Y ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$angle", (object?)entry.Angle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$confidence", (object?)entry.Confidence ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$count", entry.Count);
            cmd.Parameters.AddWithValue("$elapsed_ms", entry.ElapsedMs);
            cmd.Parameters.AddWithValue("$code", entry.Code);
            cmd.Parameters.AddWithValue("$message", entry.Message ?? "");
            var posesJson = entry.Poses is { Count: > 0 }
                ? JsonSerializer.Serialize(entry.Poses, JsonOptions)
                : (object)DBNull.Value;
            cmd.Parameters.AddWithValue("$poses_json", posesJson);
            cmd.ExecuteNonQuery();

            CleanupIfDue();
        }
    }

    public IReadOnlyList<ResultDbRow> Query(ResultDbQuery? query = null)
    {
        query ??= new ResultDbQuery();
        lock (_sync)
        {
            if (_disposed)
                return [];
            if (_conn is null && !File.Exists(DatabasePath))
                return [];
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            var where = new StringBuilder("WHERE 1=1");
            ApplyFilters(cmd, query, where);
            var limit = Math.Clamp(query.Limit <= 0 ? 500 : query.Limit, 1, 10_000);
            var offset = Math.Max(0, query.Offset);
            cmd.CommandText =
                $"""
                 SELECT id, t, recipe, station, camera, x, y, angle, confidence,
                        count, elapsed_ms, code, message, poses_json
                 FROM results
                 {where}
                 ORDER BY t_unix DESC, id DESC
                 LIMIT {limit} OFFSET {offset};
                 """;
            using var reader = cmd.ExecuteReader();
            var rows = new List<ResultDbRow>();
            while (reader.Read())
                rows.Add(ReadRow(reader));
            return rows;
        }
    }

    public long Count(ResultDbQuery? query = null)
    {
        query ??= new ResultDbQuery();
        lock (_sync)
        {
            if (_disposed)
                return 0;
            if (_conn is null && !File.Exists(DatabasePath))
                return 0;
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            var where = new StringBuilder("WHERE 1=1");
            ApplyFilters(cmd, query, where);
            cmd.CommandText = $"SELECT COUNT(*) FROM results {where};";
            var value = cmd.ExecuteScalar();
            return value is long n ? n : Convert.ToInt64(value);
        }
    }

    public ResultDbSummary Summarize(ResultDbQuery? query = null)
    {
        query ??= new ResultDbQuery();
        lock (_sync)
        {
            if (_disposed || (_conn is null && !File.Exists(DatabasePath)))
                return new ResultDbSummary(0, 0, 0, null, null, null, null);
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            var where = new StringBuilder("WHERE 1=1");
            ApplyFilters(cmd, query, where);
            cmd.CommandText =
                $"""
                 SELECT
                   COUNT(*),
                   SUM(CASE WHEN code = 0 THEN 1 ELSE 0 END),
                   AVG(x), AVG(y), AVG(angle), AVG(elapsed_ms)
                 FROM results
                 {where};
                 """;
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return new ResultDbSummary(0, 0, 0, null, null, null, null);
            var total = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
            var ok = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1));
            return new ResultDbSummary(
                total, ok, total - ok,
                ReadNullableDouble(reader, 2),
                ReadNullableDouble(reader, 3),
                ReadNullableDouble(reader, 4),
                ReadNullableDouble(reader, 5));
        }
    }

    /// <summary>库中出现过的配方名（分析页下拉）。无库文件时不创建空库。</summary>
    public IReadOnlyList<string> ListRecipes()
    {
        lock (_sync)
        {
            if (_disposed || (_conn is null && !File.Exists(DatabasePath)))
                return [];
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            cmd.CommandText =
                """
                SELECT DISTINCT recipe FROM results
                WHERE recipe <> ''
                ORDER BY recipe COLLATE NOCASE
                LIMIT 200;
                """;
            using var reader = cmd.ExecuteReader();
            var names = new List<string>();
            while (reader.Read())
                names.Add(reader.GetString(0));
            return names;
        }
    }

    /// <summary>筛选范围内有角度的样本（上限 10000），供分析页直方图。</summary>
    public IReadOnlyList<double> QueryAngles(ResultDbQuery? query = null)
    {
        query ??= new ResultDbQuery();
        lock (_sync)
        {
            if (_disposed || (_conn is null && !File.Exists(DatabasePath)))
                return [];
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            var where = new StringBuilder("WHERE 1=1");
            ApplyFilters(cmd, query, where);
            where.Append(" AND angle IS NOT NULL");
            cmd.CommandText = $"SELECT angle FROM results {where} LIMIT 10000;";
            using var reader = cmd.ExecuteReader();
            var values = new List<double>();
            while (reader.Read())
                values.Add(reader.GetDouble(0));
            return values;
        }
    }

    /// <summary>筛选范围内按错误码计数（分析页失败分布）。</summary>
    public IReadOnlyList<ResultCodeCount> CountByCode(ResultDbQuery? query = null)
    {
        query ??= new ResultDbQuery();
        lock (_sync)
        {
            if (_disposed || (_conn is null && !File.Exists(DatabasePath)))
                return [];
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            var where = new StringBuilder("WHERE 1=1");
            ApplyFilters(cmd, query, where);
            cmd.CommandText =
                $"""
                 SELECT code, COUNT(*) AS n FROM results
                 {where}
                 GROUP BY code
                 ORDER BY n DESC
                 LIMIT 20;
                 """;
            using var reader = cmd.ExecuteReader();
            var rows = new List<ResultCodeCount>();
            while (reader.Read())
                rows.Add(new ResultCodeCount(reader.GetInt32(0), Convert.ToInt64(reader.GetValue(1))));
            return rows;
        }
    }

    /// <summary>删除早于 cutoff 的行。返回删除条数。</summary>
    public int DeleteOlderThan(DateTimeOffset cutoff)
    {
        lock (_sync)
        {
            if (_disposed)
                return 0;
            if (_conn is null && !File.Exists(DatabasePath))
                return 0;
            EnsureOpen();
            using var cmd = _conn!.CreateCommand();
            cmd.CommandText = "DELETE FROM results WHERE t_unix < $cutoff;";
            cmd.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeMilliseconds());
            return cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            try { _conn?.Dispose(); }
            catch (Exception ex) { _log.LogDebug(ex, "关闭结果库连接"); }
            _conn = null;
        }
    }

    private void EnsureOpen()
    {
        if (_conn is not null)
            return;

        Directory.CreateDirectory(Folder);
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString());
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText =
                """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA busy_timeout=5000;
                PRAGMA temp_store=MEMORY;
                """;
            pragma.ExecuteNonQuery();
        }
        using (var schema = conn.CreateCommand())
        {
            schema.CommandText =
                """
                CREATE TABLE IF NOT EXISTS results (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  t TEXT NOT NULL,
                  t_unix INTEGER NOT NULL,
                  recipe TEXT NOT NULL,
                  station TEXT NOT NULL,
                  camera TEXT NOT NULL,
                  x REAL,
                  y REAL,
                  angle REAL,
                  confidence REAL,
                  count INTEGER NOT NULL,
                  elapsed_ms REAL NOT NULL,
                  code INTEGER NOT NULL,
                  message TEXT NOT NULL,
                  poses_json TEXT
                );
                CREATE INDEX IF NOT EXISTS ix_results_t_unix ON results(t_unix);
                CREATE INDEX IF NOT EXISTS ix_results_recipe_t ON results(recipe, t_unix);
                CREATE INDEX IF NOT EXISTS ix_results_code_t ON results(code, t_unix);
                """;
            schema.ExecuteNonQuery();
        }
        _conn = conn;
    }

    private void CleanupIfDue()
    {
        if (RetainedDays <= 0)
            return;
        var now = DateTime.UtcNow;
        if (now - _lastCleanupUtc < TimeSpan.FromHours(1) && _lastCleanupUtc != DateTime.MinValue)
            return;
        _lastCleanupUtc = now;
        try
        {
            using var cmd = _conn!.CreateCommand();
            cmd.CommandText = "DELETE FROM results WHERE t_unix < $cutoff;";
            cmd.Parameters.AddWithValue("$cutoff",
                DateTimeOffset.Now.AddDays(-RetainedDays).ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "清理结果库超期行失败（不影响管线）");
        }
    }

    private static void ApplyFilters(SqliteCommand cmd, ResultDbQuery query, StringBuilder where)
    {
        if (query.From is { } from)
        {
            where.Append(" AND t_unix >= $from");
            cmd.Parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds());
        }
        if (query.To is { } to)
        {
            where.Append(" AND t_unix <= $to");
            cmd.Parameters.AddWithValue("$to", to.ToUnixTimeMilliseconds());
        }
        if (!string.IsNullOrWhiteSpace(query.Recipe))
        {
            where.Append(" AND recipe = $recipe COLLATE NOCASE");
            cmd.Parameters.AddWithValue("$recipe", query.Recipe.Trim());
        }
        if (query.Code is { } code)
        {
            where.Append(" AND code = $code");
            cmd.Parameters.AddWithValue("$code", code);
        }
        else if (query.OkOnly == true)
        {
            where.Append(" AND code = 0");
        }
        else if (query.OkOnly == false)
        {
            where.Append(" AND code <> 0");
        }
    }

    private static ResultDbRow ReadRow(SqliteDataReader reader)
    {
        var x = ReadNullableDouble(reader, 5);
        var y = ReadNullableDouble(reader, 6);
        var angle = ReadNullableDouble(reader, 7);
        var confidence = ReadNullableDouble(reader, 8);
        var posesJson = reader.IsDBNull(13) ? null : reader.GetString(13);
        return new ResultDbRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            x, y, angle, confidence,
            reader.GetInt32(9),
            reader.GetDouble(10),
            reader.GetInt32(11),
            reader.GetString(12),
            ParsePoses(posesJson, x, y, angle, confidence));
    }

    private static IReadOnlyList<ResultPoseLog> ParsePoses(
        string? json, double? x, double? y, double? angle, double? confidence)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ResultPoseLog[]>(json, JsonOptions);
                if (parsed is { Length: > 0 })
                    return parsed;
            }
            catch (JsonException)
            {
                // 损坏行回退到首个位姿列
            }
        }

        if (x is null && y is null && angle is null)
            return [];
        return [new ResultPoseLog(x ?? 0, y ?? 0, angle ?? 0, confidence)];
    }

    private static double? ReadNullableDouble(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    internal static string ResolveDbPath(string folder, string? fileName)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "results.db" : fileName.Trim();
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(name)
            || name.Contains('/') || name.Contains('\\'))
            name = "results.db";
        return Path.Combine(folder, name);
    }
}
