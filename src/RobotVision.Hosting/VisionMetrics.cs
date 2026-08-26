using System.Collections.Concurrent;
using RobotVision.Core.Models;

namespace RobotVision.Hosting;

/// <summary>按配方聚合的运行统计（成功率/平均耗时），供 UI 看板展示。</summary>
public sealed record RecipeStatsSnapshot(
    string Recipe,
    long Total,
    long Ok,
    long Failed,
    double AvgMs,
    double LastMs,
    DateTime? LastAt,
    int ConsecutiveFails = 0)
{
    public double SuccessRate => Total == 0 ? 0 : (double)Ok / Total;
}

/// <summary>单个配方的累计计数（内部可变，快照时投影为不可变记录）。</summary>
internal sealed class RecipeStat
{
    public long Total;
    public long Ok;
    public long Failed;
    public double TotalMs;
    public double LastMs;
    public DateTime? LastAt;
    public int ConsecutiveFails;
}

/// <summary>
/// 运行指标：按配方聚合统计 + 全局滚动健康窗口。
/// 从 VisionService 拆出（原上帝类职责之一），线程安全，供 UI 看板与 STATUS 命令消费。
/// </summary>
public sealed class VisionMetrics
{
    /// <summary>全局滚动健康窗口大小（最近 N 次的成功率/P95）。</summary>
    private const int HealthWindow = 128;

    private readonly ConcurrentDictionary<string, RecipeStat> _stats = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _healthLock = new();
    private readonly double[] _healthLatency = new double[HealthWindow];
    private readonly byte[] _healthOutcome = new byte[HealthWindow]; // 0 成功 / 1 业务失败 / 2 超时
    private int _healthIndex;

    /// <summary>最近窗口健康指标：总次数 / 业务失败数 / 超时数（含 1008/1010）/ 平均耗时 / P95。</summary>
    public (int Total, int Failed, int TimedOut, double AvgMs, double P95Ms) Health
    {
        get
        {
            lock (_healthLock)
            {
                var count = Math.Min(_healthIndex, HealthWindow);
                if (count == 0)
                    return (0, 0, 0, 0, 0);

                var latencies = new double[count];
                var failed = 0;
                var timedOut = 0;
                for (var i = 0; i < count; i++)
                {
                    var idx = (_healthIndex - count + i) % HealthWindow;
                    latencies[i] = _healthLatency[idx];
                    if (_healthOutcome[idx] == 1) failed++;
                    else if (_healthOutcome[idx] == 2) timedOut++;
                }
                Array.Sort(latencies);
                var p95 = latencies[(int)Math.Ceiling(count * 0.95) - 1];
                return (count, failed, timedOut, latencies.Average(), p95);
            }
        }
    }

    /// <summary>记录一次请求结果（最终应答口径）。</summary>
    public void Record(VisionResult result)
    {
        RecordStats(result);
        RecordHealth(result);
    }

    /// <summary>按配方聚合的运行统计快照（最近触发优先）。含手动触发与 TCP 触发。</summary>
    public IReadOnlyList<RecipeStatsSnapshot> GetRecipeStats() =>
        _stats.Select(kv =>
            {
                var s = kv.Value;
                // 单条计数在条目锁下读取，避免读到"总数已加、耗时/最近时间未更新"的不一致快照
                lock (s)
                {
                    return new RecipeStatsSnapshot(
                        kv.Key, s.Total, s.Ok, s.Failed,
                        s.Total == 0 ? 0 : s.TotalMs / s.Total, s.LastMs, s.LastAt,
                        s.ConsecutiveFails);
                }
            })
            .OrderByDescending(s => s.LastAt ?? DateTime.MinValue)
            .ToList();

    /// <summary>成功计入 Ok；业务失败（含 1007 未检出、1009 排队超限、超时等）计入 Failed。进程内存统计，重启归零。</summary>
    private void RecordStats(VisionResult result)
    {
        var s = _stats.GetOrAdd(result.RecipeName, _ => new RecipeStat());
        lock (s)
        {
            s.Total++;
            if (result.Ok)
                s.Ok++;
            else
                s.Failed++;
            s.TotalMs += result.ElapsedMs;
            s.LastMs = result.ElapsedMs;
            s.LastAt = DateTime.Now;
            if (result.Ok)
                s.ConsecutiveFails = 0;
            else if (ProcessFailureCodes.CountsTowardStreak(result.ErrorCode))
                s.ConsecutiveFails++;
        }
    }

    /// <summary>滚动记录最近一次请求结果（健康指标）。</summary>
    private void RecordHealth(VisionResult result)
    {
        var outcome = result.Ok ? (byte)0
            : result.ErrorCode is VisionErrorCode.Timeout or VisionErrorCode.QueueTimeout ? (byte)2
            : (byte)1;
        lock (_healthLock)
        {
            _healthLatency[_healthIndex % HealthWindow] = result.ElapsedMs;
            _healthOutcome[_healthIndex % HealthWindow] = outcome;
            _healthIndex++;
        }
    }

    /// <summary>指定配方的连续过程失败次数（成功清零；配置类错误不计入）。</summary>
    public int GetConsecutiveFails(string recipe)
    {
        if (!_stats.TryGetValue(recipe, out var s))
            return 0;
        lock (s)
            return s.ConsecutiveFails;
    }

    /// <summary>所有配方中最大的连续失败次数（供 STATUS 总览）。</summary>
    public int MaxConsecutiveFails
    {
        get
        {
            var max = 0;
            foreach (var s in _stats.Values)
            {
                lock (s)
                    max = Math.Max(max, s.ConsecutiveFails);
            }
            return max;
        }
    }

    /// <summary>解除联锁：清空指定配方或全部配方的连续失败计数。</summary>
    public void ResetConsecutive(string? recipe = null)
    {
        if (string.IsNullOrWhiteSpace(recipe))
        {
            foreach (var s in _stats.Values)
            {
                lock (s)
                    s.ConsecutiveFails = 0;
            }
            return;
        }

        if (_stats.TryGetValue(recipe, out var one))
        {
            lock (one)
                one.ConsecutiveFails = 0;
        }
    }

    /// <summary>从落盘快照恢复累计统计（进程重启后连续失败/良率不丢）。健康窗口仍从空开始。</summary>
    public void Restore(IEnumerable<RecipeStatsSnapshot> snapshots)
    {
        foreach (var snap in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snap.Recipe))
                continue;
            var s = _stats.GetOrAdd(snap.Recipe, _ => new RecipeStat());
            lock (s)
            {
                s.Total = snap.Total;
                s.Ok = snap.Ok;
                s.Failed = snap.Failed;
                s.TotalMs = snap.AvgMs * snap.Total;
                s.LastMs = snap.LastMs;
                s.LastAt = snap.LastAt;
                s.ConsecutiveFails = Math.Max(0, snap.ConsecutiveFails);
            }
        }
    }
}
