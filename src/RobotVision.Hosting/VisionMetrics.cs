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
    DateTime? LastAt)
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
                        s.Total == 0 ? 0 : s.TotalMs / s.Total, s.LastMs, s.LastAt);
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
}
